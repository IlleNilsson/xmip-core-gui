namespace Xmip.Gui.Surface;

/// <summary>
/// Where a scope is reached in each of the three views. The web GUI is three
/// points to drill from — Configuration, Monitor and Topology — and from any
/// of them a scope leads to the same scope in the others (the owner,
/// 2026-09-18; ADR-0052, amendment 2026-09-14, ruling 4: a link from any view
/// ends at the view of the actual configuration). One place writes the
/// links, so a row, a node and a crumb cannot disagree about where a scope is.
/// </summary>
/// <remarks>
/// A host may hold more than one cluster (ADR-0052, amendment 2026-09-20), and
/// a link that dropped the cluster would land on another cluster's tree at a
/// scope that is not in it. So every link carries the cluster it was written
/// on, and a host holding one omits it — the addresses a single-cluster host
/// writes are the ones it always wrote.
/// </remarks>
public static class ScopeLink
{
    /// <summary>The scope's row in the Configuration tree, on this cluster.</summary>
    public static string Configuration(string scope, string? cluster = null)
    {
        return "configuration" + Asking(cluster) + "#" + Anchor(scope);
    }

    /// <summary>The Monitor's drill at the scope, on this cluster.</summary>
    public static string Monitor(string scope, string? cluster = null)
    {
        return "/?scope=" + Uri.EscapeDataString(scope) + Also(cluster);
    }

    /// <summary>The Topology, on this cluster.</summary>
    public static string Topology(string? cluster = null)
    {
        return "/topology" + Asking(cluster);
    }

    /// <summary>
    /// This same view, on another cluster: the path the operator is on and
    /// nothing else of the address. A scope of the cluster being left names
    /// nothing in the one being entered, so the drill and the filter start
    /// over rather than pointing at something that is not there.
    /// </summary>
    public static string Cluster(string relative, string cluster)
    {
        int query = relative.IndexOf('?', StringComparison.Ordinal);
        string path = (query < 0 ? relative : relative[..query]).TrimStart('/');

        return "/" + path + "?" + ClusterView.Query + "=" + Uri.EscapeDataString(cluster);
    }

    /// <summary>A scope as an element id: its letters and digits, the rest
    /// as dashes. What the Configuration tree gives each row.</summary>
    public static string Anchor(string scope)
    {
        return "s-" + string.Concat(
            scope.Select(letter => char.IsLetterOrDigit(letter) ? letter : '-'));
    }

    private static string Asking(string? cluster)
    {
        return string.IsNullOrEmpty(cluster)
            ? string.Empty
            : "?" + ClusterView.Query + "=" + Uri.EscapeDataString(cluster);
    }

    private static string Also(string? cluster)
    {
        return string.IsNullOrEmpty(cluster)
            ? string.Empty
            : "&" + ClusterView.Query + "=" + Uri.EscapeDataString(cluster);
    }
}
