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
    public void TheWebHostKeepsAnOrderOnlyForAPathOnItself()
    {
        // Every view is kept, the ones the order named first.
        Assert.Equal((BarOrder.Say(BarOrder.Read("audit,monitor")), "/topology?cluster=C1"),
            Kept(BarOrder.Asked("audit,monitor", "/topology?cluster=C1")));
        Assert.Null(BarOrder.Asked(BarOrder.Say(BarOrder.Default), "/").Kept);
        Assert.Null(BarOrder.Asked(string.Empty, "/").Kept);

        string?[] elsewhere = ["https://elsewhere.example", "//elsewhere", "/\\x", "", null];

        foreach (string? back in elsewhere)
        {
            Assert.Equal("/", BarOrder.Asked("audit", back).Back);
        }
    }

    [Fact]
    public void TheDesktopsFileKeepsAnOrderBesideWhatElseItHolds()
    {
        string path = Path.Combine(folder, BarOrderFile.Name);
        Directory.CreateDirectory(folder);
        File.WriteAllText(path, "# the operator's own\n[other]\nkept = \"yes\"\n");

        new BarOrderFile(path).Keep(BarOrder.Moved(BarOrder.Default, "audit", 0));

        Assert.Equal("audit", Word(new BarOrderFile(path).Order[0]));
        string written = File.ReadAllText(path);
        Assert.Contains("kept = \"yes\"", written, StringComparison.Ordinal);
        Assert.Contains("# the operator's own", written, StringComparison.Ordinal);

        new BarOrderFile(path).Keep(null);
        Assert.Equal(BarOrder.Default, new BarOrderFile(path).Order);

        File.WriteAllText(path, "this is [not toml");
        Assert.Equal(BarOrder.Default, new BarOrderFile(path).Order);
    }

    private static string Word(BarTab tab)
    {
        return tab.Word;
    }

    // The two parts of what the host answers, the cookie's value non-null.
    private static (string Kept, string Back) Kept((string? Kept, string Back) asked)
    {
        return (asked.Kept ?? "(deleted)", asked.Back);
    }
}
