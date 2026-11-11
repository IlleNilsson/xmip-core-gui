using System.Text.RegularExpressions;

namespace Xmip.Gui.Test;

/// <summary>
/// Where movement is paused or the reader asked for reduced motion, the
/// Topology still shows where traffic is (ADR-0052, amendment 2026-11-10):
/// the pulses stand along the line and a node that carried traffic keeps its
/// ring. bUnit applies no stylesheet, so the rules are held here — each one
/// placed after and at least as specific as the rule it overrides, or the
/// browser keeps the motion — and so is the outline a pulse stands off its
/// line by. The Desktop and the Web load the one sheet, so both move, and
/// stand still, alike.
/// </summary>
public sealed partial class StillMotionStylesheetTest
{
    private const string Reduced = "@media (prefers-reduced-motion: reduce)";

    // The sheet without its comments, so a rule is read by its selector
    // alone and a brace in a comment counts for nothing.
    private static readonly string Sheet = Comment().Replace(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixture", "xmip.css")),
        string.Empty);

    [Fact]
    public void StillPulsesAreHiddenWhileMovingAndShownWherePaused()
    {
        int hidden = Declared(Sheet, ".topology-link .pulses-still", "display: none");
        int paused = Declared(
            Sheet, ".topology-canvas.paused .topology-link .pulses-still", "display: inline");
        int ring = Declared(
            Sheet, ".topology-canvas.paused .topology-node.active .node-pulse", "animation: none");

        Assert.True(hidden < paused, "shown paused after it is hidden");
        Assert.True(Declared(Sheet, ".topology-node.active .node-pulse", "animation:") < ring);
    }

    [Fact]
    public void ReducedMotionStopsEveryPulseAndKeepsWhereTrafficIs()
    {
        int at = Sheet.IndexOf(Reduced, StringComparison.Ordinal);
        Assert.True(at >= 0, "the sheet answers a reader who asked for reduced motion");
        string block = Block(at);

        Declared(block, ".topology-link .pulses-moving", "display: none");
        Declared(block, ".topology-link .pulses-still", "display: inline");
        // As specific as the rule that animates a busy node's ring, so it
        // wins by coming later: `.topology-node .node-pulse` would lose.
        int ring = Declared(block, ".topology-node.active .node-pulse", "animation: none");
        Assert.Contains("var(--activity)", Body(block, ring), StringComparison.Ordinal);
        Assert.True(
            Declared(Sheet, ".topology-link .pulses-still", "display: none") < at
                && Declared(Sheet, ".topology-node.active .node-pulse", "animation:") < at,
            "the block comes after the rules it overrides");
    }

    [Fact]
    public void APulseIsOutlinedInThePaperSoItStandsOffTheLineItRides()
    {
        // The review of the first plan: a pulse of the line's own color,
        // outlined in the paper at 2.5, so it shows on a streaming line five
        // wide (its radius is Motion.Radius, held by MotionTest).
        int pulse = Declared(Sheet, ".topology-link .flow-particle", "stroke: var(--paper)");
        Assert.Contains("stroke-width: 2.5", Body(Sheet, pulse), StringComparison.Ordinal);
    }

    [Fact]
    public void TheDesktopAndTheWebLoadTheOneStylesheet()
    {
        string hosts = Path.Combine(Module(), "src");
        string desktop = File.ReadAllText(
            Path.Combine(hosts, "Xmip.Operations", "wwwroot", "index.html"));
        string web = File.ReadAllText(
            Path.Combine(hosts, "Xmip.Gui.Web", "Components", "App.razor"));

        Assert.Contains("\"_content/Xmip.Gui/xmip.css\"", desktop, StringComparison.Ordinal);
        Assert.Contains("\"_content/Xmip.Gui/xmip.css\"", web, StringComparison.Ordinal);
    }

    // Where in `text` the rule for exactly `selector` is, holding
    // `declaration`; fails where there is none.
    private static int Declared(string text, string selector, string declaration)
    {
        Match rule = Rule().Matches(text).FirstOrDefault(rule =>
            rule.Groups["selector"].Value.Trim() == selector
            && rule.Groups["body"].Value.Contains(declaration, StringComparison.Ordinal))
            ?? throw new Xunit.Sdk.XunitException($"no rule {selector} {{ {declaration} }}");

        return rule.Index;
    }

    private static string Body(string text, int at) => Rule().Match(text, at).Groups["body"].Value;

    // The at-rule beginning at `at`, to its closing brace.
    private static string Block(int at)
    {
        int open = Sheet.IndexOf('{', at);
        int depth = 0;

        for (int place = open; place < Sheet.Length; place++)
        {
            depth += Sheet[place] switch { '{' => 1, '}' => -1, _ => 0 };

            if (depth == 0)
            {
                return Sheet[(open + 1)..place];
            }
        }

        throw new Xunit.Sdk.XunitException($"{Reduced} is not closed");
    }

    // The GUI module's root, above this test's build.
    private static string Module()
    {
        for (DirectoryInfo? at = new(AppContext.BaseDirectory); at is not null; at = at.Parent)
        {
            if (File.Exists(Path.Combine(at.FullName, "src", "Xmip.Gui", "Xmip.Gui.csproj")))
            {
                return at.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            $"{AppContext.BaseDirectory} is not inside the GUI module");
    }

    [GeneratedRegex(@"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}")]
    private static partial Regex Rule();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comment();
}
