namespace Xmip.Gui.Surface;

/// <summary>A view on the bar: the word an order names it by, where it is,
/// and the name it is shown by.</summary>
public sealed record BarTab(string Word, string Href, string Name);

/// <summary>
/// The bar's nine views and the one rule for their order (ADR-0052,
/// amendment 2026-11-12; the owner, the same day: "An operator has to be
/// able to rearrange the major tabs"). The default is most wanted first; an
/// operator moves a view anywhere, and the first view is where the bare
/// address opens. What a store kept is read by the same rule wherever it is
/// kept: a word it does not know is dropped, a view named twice counts
/// once, and a view it does not name follows in the default order, so a
/// view added later is never lost.
/// </summary>
public static class BarOrder
{
    /// <summary>The cookie a browser keeps the order in.</summary>
    public const string Cookie = "xmip-bar";

    /// <summary>The views, most wanted first.</summary>
    public static IReadOnlyList<BarTab> Default { get; } =
    [
        new("monitor", "/monitor", "Monitor"),
        new("dead-messages", "/dead-messages", "Dead Message Queue"),
        new("subscriptions", "/subscriptions", "Subscriptions"),
        new("topology", "/topology", "Topology"),
        new("configuration", "/configuration", "Configuration"),
        new("event-subscriptions", "/event-subscriptions", "Event subscriptions"),
        new("audit", "/audit", "Audit"),
        new("reports", "/reports", "Reports"),
        new("secrets", "/secrets", "Secrets"),
    ];

    /// <summary>The order <paramref name="said"/> names, its words separated
    /// by commas, by the rule above; the default where it names
    /// nothing.</summary>
    public static IReadOnlyList<BarTab> Read(string? said)
    {
        List<BarTab> order = [];

        foreach (string word in (said ?? string.Empty).Split(
            ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Default.FirstOrDefault(tab => tab.Word == word) is { } tab
                && !order.Contains(tab))
            {
                order.Add(tab);
            }
        }

        order.AddRange(Default.Where(tab => !order.Contains(tab)));

        return order;
    }

    /// <summary>What a store keeps for <paramref name="order"/>: its words,
    /// separated by commas.</summary>
    public static string Say(IEnumerable<BarTab> order)
    {
        return string.Join(',', order.Select(tab => tab.Word));
    }

    /// <summary><paramref name="order"/> with the view
    /// <paramref name="word"/> names moved to <paramref name="to"/>, held
    /// within the bar; unchanged where no view is called so.</summary>
    public static IReadOnlyList<BarTab> Moved(IReadOnlyList<BarTab> order, string word, int to)
    {
        List<BarTab> moved = [.. order];
        int from = moved.FindIndex(tab => tab.Word == word);

        if (from < 0)
        {
            return order;
        }

        BarTab tab = moved[from];
        moved.RemoveAt(from);
        moved.Insert(Math.Clamp(to, 0, moved.Count), tab);

        return moved;
    }

    /// <summary>Where the bare address opens: the first view.</summary>
    public static string Start(IReadOnlyList<BarTab> order)
    {
        return order.Count > 0 ? order[0].Href : Default[0].Href;
    }

    /// <summary>What the Web host keeps for an order a browser asked for:
    /// null, to delete the cookie, where it is the default.</summary>
    public static string? Kept(string? asked)
    {
        string said = Say(Read(asked));

        return said == Say(Default) ? null : said;
    }
}
