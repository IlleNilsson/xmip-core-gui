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

    [GeneratedRegex(@"(?<selector>[^{}]+)\{(?<body>[^}]*)\}")]
    private static partial Regex Rule();
}
