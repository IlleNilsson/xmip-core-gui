using System.Reflection;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Gui.Components;
using Xmip.Gui.Pages;
using Xmip.Gui.Surface;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// The status bar at the window's foot (ADR-0052, amendment 2026-11-25; the
/// owner, the same day: "that should be on a status bar at the bottom of the
/// page"): every view, drawn in the one frame both hosts draw, stands over one
/// bar that says who is signed in, the role, the cluster, where the records
/// come from and what the run was started with, and no view's top nor the bar
/// above says any of it. Until 2026-11-25 the run line above every view said
/// the run and the source, and the bar above said the role and who held it.
/// Over the surface library's cluster fixture.
/// </summary>
public sealed partial class StatusBarTest : BunitContext
{
    private static readonly TestCluster Names = TestCluster.Read();
    private static readonly string Receiver = Names.WithRole("receiving");
    private static readonly string Processor = Names.WithRole("processing");
    private static readonly string Sender = Names.WithRole("sending");

    private static readonly string Run =
        $"RoundTrip · {Names.Name} · nodes {Receiver}=receiving "
        + $"{Processor}=processing+sending {Sender}=sending · Node capabilities "
        + $"{Receiver}=online · realistic";

    // Every view a host routes to.
    private static readonly Type[] Views =
    [
        .. typeof(Cluster).Assembly.GetTypes().Where(type =>
            type.IsSubclassOf(typeof(ClusterView))
            && type.GetCustomAttribute<RouteAttribute>() is not null),
    ];

    // Made here; the store's file beneath it is the store's to make.
    private readonly TestFolder folder = new("status");

    private readonly RoleContext role = new(Role.Observer, RoleAssignment.ProcessCaller());

    public StatusBarTest()
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "Fixture", "cluster.xmip");
        Services.AddSingleton(ClusterSurfaces.Over(new SnapshotOperator(fixture)));
        Services.AddSingleton(role);
        Services.AddSingleton(new ProgramAudit("Xmip.Gui.Test", Unacted.Audit));
        Services.AddSingleton<IPreferenceStore>(
            new PreferenceFile(folder.Inside(PreferenceFile.Name)));
    }

    [Fact]
    public void EveryViewStandsOverOneStatusBarThatSaysWhatItStandsOn()
    {
        // The ten views of the bar, each in the frame both hosts draw.
        Assert.Equal(BarOrder.Default.Count, Views.Length);

        foreach (Type view in Views)
        {
            IRenderedComponent<Frame> page = Render<Frame>(parameters => parameters
                .AddChildContent(Alone(view)));
            IElement bar = Assert.Single(page.FindAll("footer.status-bar"));

            Assert.Equal("contentinfo", bar.GetAttribute("role"));
            Assert.Equal(
                ["signed in", "role", "cluster", "source", "run"],
                bar.QuerySelectorAll("dt").Select(term => term.TextContent.Trim()));
            Assert.Equal(role.Who, Fact(bar, "who"));
            Assert.Equal("Observer", Fact(bar, "role"));
            Assert.Equal(
                Role.Observer.Describe(), bar.QuerySelector(".status-role")!.GetAttribute("title"));
            Assert.Equal(Names.Name, Fact(bar, "cluster"));
            Assert.StartsWith("SNAPSHOT — ", Fact(bar, "source"), StringComparison.Ordinal);
            Assert.Equal(Run, Fact(bar, "run"));
        }
    }

    [Fact]
    public void NoViewNorTheBarAboveSaysWhatTheStatusBarSays()
    {
        string source = Render<StatusBar>().Find(".status-source").TextContent.Trim();
        Assert.NotNull(role.Who);

        foreach (Type view in Views)
        {
            string markup = Render(Alone(view)).Markup;

            Assert.DoesNotContain("status-bar", markup, StringComparison.Ordinal);
            Assert.DoesNotContain(Run, markup, StringComparison.Ordinal);
            Assert.DoesNotContain(source, markup, StringComparison.Ordinal);
        }

        string nav = Render<TopNav>().Markup;
        Assert.DoesNotContain(role.Who, nav, StringComparison.Ordinal);
        Assert.DoesNotContain("Observer", nav, StringComparison.Ordinal);
    }

    [Fact]
    public void TheRoleIsDescribedAndACallerNothingProvedIsSaidSo()
    {
        using BunitContext unproven = new();
        unproven.Services.AddSingleton(Services.GetRequiredService<ClusterSurfaces>());
        unproven.Services.AddSingleton(new RoleContext(Role.Operator, null));

        IElement bar = unproven.Render<StatusBar>().Find("footer.status-bar");

        Assert.Equal("nobody proven", Fact(bar, "who"));
        Assert.Equal("Operator", Fact(bar, "role"));
        Assert.Equal(
            "also acts and configures", bar.QuerySelector(".status-role")!.GetAttribute("title"));
    }

    [Fact]
    public void TheBarIsFixedAtTheFootAndTheFrameKeepsItsHeightClearBeneathTheView()
    {
        string sheet =
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixture", "xmip.css"));
        IEnumerable<Match> rules = Rule().Matches(sheet);
        const string Height = "calc(var(--status-bar) + env(safe-area-inset-bottom))";

        // The bar's one height is what the frame leaves clear at its foot, so
        // the end of a page is never behind the bar; painted by the tokens
        // alone, never a written color and never the brand.
        Match bar = Assert.Single(rules, rule =>
            rule.Groups["selector"].Value.Trim() == ".status-bar"
            && rule.Groups["body"].Value.Contains("position: fixed", StringComparison.Ordinal));
        string body = bar.Groups["body"].Value;
        Assert.Contains("bottom: 0", body, StringComparison.Ordinal);
        Assert.Contains($"height: {Height}", body, StringComparison.Ordinal);
        Assert.DoesNotMatch("#[0-9a-fA-F]{3,8}\\b", body);
        Assert.DoesNotContain("--brand", body, StringComparison.Ordinal);
        Assert.Contains(rules, rule =>
            rule.Groups["selector"].Value.Trim() == ".themed"
            && rule.Groups["body"].Value.Contains(
                $"padding-bottom: {Height}", StringComparison.Ordinal));
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        await base.DisposeAsyncCore();
        role.Dispose();
        folder.Dispose();
    }

    // A view by its type, with nothing around it.
    private static RenderFragment Alone(Type view)
    {
        return builder =>
        {
            builder.OpenComponent(0, view);
            builder.CloseComponent();
        };
    }

    private static string Fact(IElement bar, string name)
    {
        return bar.QuerySelector($".status-{name}")!.TextContent.Trim();
    }

    [GeneratedRegex(@"(?<selector>[^{}]+)\{(?<body>[^}]*)\}")]
    private static partial Regex Rule();
}
