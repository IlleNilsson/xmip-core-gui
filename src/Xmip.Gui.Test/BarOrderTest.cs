using Xmip.Gui.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// The one rule for the bar's order, wherever it is kept (ADR-0052,
/// amendment 2026-11-12): a word it does not know is dropped, a view named
/// twice counts once, a view it does not name follows in the default order;
/// the Web host keeps an order only for a path on itself; the Desktop's
/// file keeps what else it holds.
/// </summary>
public sealed class BarOrderTest : IDisposable
{
    private readonly string folder =
        Path.Combine(Path.GetTempPath(), $"xmip-bar-order-{Guid.NewGuid():n}");

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AnOrderIsReadByOneRuleAndNothingIsLost()
    {
        Assert.Equal(BarOrder.Default, BarOrder.Read(null));
        Assert.Equal(BarOrder.Default, BarOrder.Read("unknown, ,"));

        IReadOnlyList<BarTab> read = BarOrder.Read("secrets, nonsense,audit,secrets");

        Assert.Equal(["secrets", "audit", "monitor", "dead-messages"], read.Take(4).Select(Word));
        Assert.Equal(BarOrder.Default.Count, read.Count);
        Assert.Equal(read, BarOrder.Read(BarOrder.Say(read)));
        Assert.Equal("/secrets", BarOrder.Start(read));
        Assert.Equal("/monitor", BarOrder.Start(BarOrder.Default));
    }

    [Fact]
    public void AViewMovesWithinTheBarAndNoFurther()
    {
        IReadOnlyList<BarTab> order = BarOrder.Default;

        Assert.Equal("topology", Word(BarOrder.Moved(order, "topology", 0)[0]));
        Assert.Equal("monitor", Word(BarOrder.Moved(order, "monitor", 99)[^1]));
        Assert.Equal("monitor", Word(BarOrder.Moved(order, "monitor", -3)[0]));
        Assert.Same(order, BarOrder.Moved(order, "nowhere", 1));
    }

    [Fact]
    public void TheWebHostKeepsAChoiceAndSendsBackOnlyToAPathOnItself()
    {
        // Every view is kept, the ones the order named first; the default
        // and the system's theme delete their cookies.
        Assert.Equal(BarOrder.Say(BarOrder.Read("audit,monitor")), BarOrder.Kept("audit,monitor"));
        Assert.Null(BarOrder.Kept(BarOrder.Say(BarOrder.Default)));
        Assert.Null(BarOrder.Kept(string.Empty));
        Assert.Equal("dark", ThemeChoice.Kept("dark"));
        Assert.Null(ThemeChoice.Kept("system"));
        Assert.Null(ThemeChoice.Kept("purple"));

        Assert.Equal("/topology?cluster=C1", PreferenceCookie.Back("/topology?cluster=C1"));
        string?[] elsewhere = ["https://elsewhere.example", "//elsewhere", "/\\x", "", null];

        foreach (string? back in elsewhere)
        {
            Assert.Equal("/", PreferenceCookie.Back(back));
        }
    }

    [Fact]
    public void AThemeIsReadByOneRuleAndTheButtonGoesRound()
    {
        Assert.Equal(
            [Theme.System, Theme.Light, Theme.Dark, Theme.System],
            [ThemeChoice.Read(null), ThemeChoice.Read("light"), ThemeChoice.Read(" dark "),
                ThemeChoice.Read("unknown")]);
        Assert.Equal(Theme.Light, ThemeChoice.Next(Theme.System));
        Assert.Equal(Theme.Dark, ThemeChoice.Next(Theme.Light));
        Assert.Equal(Theme.System, ThemeChoice.Next(Theme.Dark));
        Assert.All(Enum.GetValues<Theme>(), theme =>
            Assert.Equal(theme, ThemeChoice.Read(ThemeChoice.Word(theme))));
    }

    [Fact]
    public void TheDesktopsFileKeepsAnOrderAndAThemeBesideWhatElseItHolds()
    {
        string path = Path.Combine(folder, PreferenceFile.Name);
        Directory.CreateDirectory(folder);
        File.WriteAllText(path, "# the operator's own\n[other]\nkept = \"yes\"\n");

        new PreferenceFile(path).Keep(BarOrder.Moved(BarOrder.Default, "audit", 0));

        Assert.Equal("audit", Word(new PreferenceFile(path).Order[0]));
        string written = File.ReadAllText(path);
        Assert.Contains("kept = \"yes\"", written, StringComparison.Ordinal);
        Assert.Contains("# the operator's own", written, StringComparison.Ordinal);

        new PreferenceFile(path).Keep(Theme.Dark);
        Assert.Equal(Theme.Dark, new PreferenceFile(path).Theme);
        Assert.Equal("audit", Word(new PreferenceFile(path).Order[0]));

        new PreferenceFile(path).Keep(null);
        new PreferenceFile(path).Keep(Theme.System);
        Assert.Equal(BarOrder.Default, new PreferenceFile(path).Order);
        Assert.Equal(Theme.System, new PreferenceFile(path).Theme);

        File.WriteAllText(path, "this is [not toml");
        Assert.Equal(BarOrder.Default, new PreferenceFile(path).Order);
        Assert.Equal(Theme.System, new PreferenceFile(path).Theme);
    }

    private static string Word(BarTab tab)
    {
        return tab.Word;
    }
}
