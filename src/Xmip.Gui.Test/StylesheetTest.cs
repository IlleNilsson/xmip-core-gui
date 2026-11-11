using System.Text.RegularExpressions;
using Xmip.Abi.Operate;
using Xmip.Gui.Surface;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// The stylesheet paints a color's name and never decides which mood is which
/// color: that is the runtime's (<c>observe::Health::color</c>, ADR-0041), and
/// until 2026-09-24 the sheet held a table of its own beside it.
/// </summary>
public sealed partial class StylesheetTest
{
    private static readonly string Sheet =
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixture", "xmip.css"));

    [Fact]
    public void EveryColorTheRuntimeNamesIsPaintedByItsOwnToken()
    {
        foreach (HealthState mood in Enum.GetValues<HealthState>())
        {
            string color = English.Color(mood);

            Assert.Contains(
                $".{color} {{ --mood: var(--{color}); }}", Sheet, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void NoRuleGivesAMoodAColorToken()
    {
        string[] colors = [.. Enum.GetValues<HealthState>().Select(English.Color).Distinct()];

        foreach (Match rule in Rule().Matches(Sheet))
        {
            string selector = rule.Groups["selector"].Value;
            string body = rule.Groups["body"].Value;
            string[] moods =
            [
                .. Enum.GetValues<HealthState>()
                    .Select(English.Mood)
                    .Where(word => Regex.IsMatch(selector, $@"\.{word}\b")),
            ];

            if (moods.Length == 0)
            {
                continue;
            }

            Assert.DoesNotContain(
                colors,
                color => body.Contains($"var(--{color})", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void AnElementInAMoodCarriesTheMoodAndItsColorsName()
    {
        Assert.Equal(
            $"{English.Mood(HealthState.Done)} {English.Color(HealthState.Done)}",
            MoodClass.Of(HealthState.Done));
        Assert.Equal("none", MoodClass.Of([]));
    }

    [Fact]
    public void AReportsKindIsDrawnInTheTextsOwnColorNeverTheBrandOrAMood()
    {
        // The icon and the words beside it (ADR-0072, amendment 2026-11-10):
        // painted by tokens and the text's own color, never a written
        // color, the brand or a mood's color.
        string[] colors = [.. Enum.GetValues<HealthState>().Select(English.Color).Distinct()];
        Match[] reports =
        [
            .. Rule().Matches(Sheet).Where(rule =>
                rule.Groups["selector"].Value.Contains(".report-", StringComparison.Ordinal)),
        ];

        Assert.Contains(reports, rule =>
            rule.Groups["selector"].Value.Trim() == ".report-kind"
            && rule.Groups["body"].Value.Contains("var(--muted)", StringComparison.Ordinal));
        Assert.Contains(reports, rule =>
            rule.Groups["selector"].Value.Trim() == ".report-icon"
            && rule.Groups["body"].Value.Contains("stroke: currentColor", StringComparison.Ordinal)
            && rule.Groups["body"].Value.Contains("fill: none", StringComparison.Ordinal));
        Assert.All(reports, rule =>
        {
            string body = rule.Groups["body"].Value;

            Assert.DoesNotMatch(Hex(), body);
            Assert.DoesNotContain(
                colors, color => body.Contains($"var(--{color})", StringComparison.Ordinal));

            if (rule.Groups["selector"].Value.Contains("report-kind", StringComparison.Ordinal)
                || rule.Groups["selector"].Value.Contains("report-icon", StringComparison.Ordinal))
            {
                Assert.DoesNotContain("--brand", body, StringComparison.Ordinal);
            }
        });
    }

    [GeneratedRegex(@"(?<selector>[^{}]+)\{(?<body>[^}]*)\}")]
    private static partial Regex Rule();

    [GeneratedRegex("#[0-9a-fA-F]{3,8}\\b")]
    private static partial Regex Hex();
}
