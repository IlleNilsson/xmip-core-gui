namespace Xmip.Gui.Test;

/// <summary>
/// The orders a surface left for a node, as <c>observe::Order</c> writes them
/// — <c>&lt;orders&gt;/&lt;node name&gt;/&lt;nanos&gt;-&lt;sequence&gt;-&lt;act&gt;.toml</c>,
/// one flat table of strings — read oldest first, for a test of the views that
/// leave them: which noun, which one, and which act.
/// </summary>
internal static class OrdersLeft
{
    /// <summary>Each order left for <paramref name="node"/>, oldest first, as
    /// <c>noun target act</c>.</summary>
    public static string[] For(string orders, string node)
    {
        string place = Path.Combine(orders, node);

        return !Directory.Exists(place)
            ? []
            :
            [
                .. Directory.GetFiles(place, "*.toml")
                    .Order(StringComparer.Ordinal)
                    .Select(File.ReadAllLines)
                    .Select(lines => $"{Value(lines, "noun")} {Value(lines, "target")} "
                        + Value(lines, "act")),
            ];
    }

    private static string Value(string[] lines, string key)
    {
        string line = lines.First(
            line => line.StartsWith(key + " = ", StringComparison.Ordinal));

        return line[(key.Length + 3)..].Trim('"');
    }
}
