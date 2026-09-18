namespace Xmip.Gui.Surface;

/// <summary>
/// Where a scope is reached in each of the three views. The web GUI is three
/// points to drill from — Configuration, Monitor and Topology — and from any
/// of them a scope leads to the same scope in the others (the owner,
/// 2026-09-18; ADR-0052, amendment 2026-09-14, ruling 4: a link from any view
/// ends at the view of the actual configuration). One place writes the
/// links, so a row, a node and a crumb cannot disagree about where a scope is.
/// </summary>
public static class ScopeLink
{
    /// <summary>The scope's row in the Configuration tree.</summary>
    public static string Configuration(string scope)
    {
        return "configuration#" + Anchor(scope);
    }

    /// <summary>The Monitor's drill at the scope.</summary>
    public static string Monitor(string scope)
    {
        return "/?scope=" + Uri.EscapeDataString(scope);
    }

    /// <summary>A scope as an element id: its letters and digits, the rest
    /// as dashes. What the Configuration tree gives each row.</summary>
    public static string Anchor(string scope)
    {
        return "s-" + string.Concat(
            scope.Select(letter => char.IsLetterOrDigit(letter) ? letter : '-'));
    }
}
