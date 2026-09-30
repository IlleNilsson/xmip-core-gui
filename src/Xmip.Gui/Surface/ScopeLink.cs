using Xmip.Surface;

namespace Xmip.Gui.Surface;

/// <summary>
/// Where a scope is reached in each of the views. The web GUI is four points
/// to drill from — Configuration, Monitor, Topology and, since 2026-09-29,
/// Audit — and from any of them a scope leads to the same scope in the others
/// (the owner, 2026-09-18; ADR-0052, amendment 2026-09-14, ruling 4: a link
/// from any view ends at the view of the actual configuration). One place
/// writes the
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
    /// <summary>The scope's row in the Configuration tree, on this cluster. The
    /// root is no row — the tree begins beneath it — so the root is the tree
    /// itself, from its top: until 2026-09-25 the Monitor's crumb at the root
    /// linked to <c>#s-xmip----</c>, an anchor nothing carries.</summary>
    public static string Configuration(string scope, string? cluster = null)
    {
        string page = "configuration" + Asking(cluster);

        return ScopeTree.Parts(scope).Length == 0 ? page : page + "#" + Anchor(scope);
    }

    /// <summary>The Monitor's drill at the scope, on this cluster.</summary>
    public static string Monitor(string scope, string? cluster = null)
    {
        return "/?scope=" + Uri.EscapeDataString(scope) + Also(cluster);
    }

    /// <summary>The name the address gives the Topology's open node.</summary>
    public const string Focus = "focus";

    /// <summary>The Topology, on this cluster.</summary>
    public static string Topology(string? cluster = null)
    {
        return "/topology" + Asking(cluster);
    }

    /// <summary>
    /// The Topology opened at a node — what is beneath it drawn, and what
    /// runs between those — on this cluster. The drill is in the address
    /// (ADR-0052, amendment 2026-09-25), so a node on the canvas is a link, a
    /// reload keeps where the operator was, and cluster to node to stage is
    /// three addresses rather than three clicks nothing remembers.
    /// </summary>
    public static string TopologyAt(string node, string? cluster = null)
    {
        return "/topology?" + Focus + "=" + Uri.EscapeDataString(node) + Also(cluster);
    }

    /// <summary>
    /// The Audit view asking <paramref name="query"/>, on this cluster: every
    /// word the query carries is in the address by the name the audit
    /// capability gives it, so a link reproduces the view — who, the filters,
    /// the sort and the page (ADR-0062, amendment 2026-09-29).
    /// </summary>
    public static string Audit(AuditQuery query, string? cluster = null)
    {
        ArgumentNullException.ThrowIfNull(query);

        List<string> said =
        [
            .. query.Pairs().Select(pair => pair.Key + "=" + Uri.EscapeDataString(pair.Value)),
        ];

        if (!string.IsNullOrEmpty(cluster))
        {
            said.Add(ClusterView.Query + "=" + Uri.EscapeDataString(cluster));
        }

        return said.Count == 0 ? "/audit" : "/audit?" + string.Join('&', said);
    }

    /// <summary>
    /// The Subscriptions view asking <paramref name="query"/>, on this
    /// cluster: where the drill stands, the one subscription, the pattern and
    /// the order are all in the address, by the names
    /// <see cref="SubscriptionQuery"/> gives them, so a link reproduces the
    /// view (ADR-0065, amendment 2026-09-29).
    /// </summary>
    public static string Subscriptions(SubscriptionQuery query, string? cluster = null)
    {
        ArgumentNullException.ThrowIfNull(query);

        List<string> said = [];

        void Say(string key, string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                said.Add(key + "=" + Uri.EscapeDataString(value));
            }
        }

        Say("location", query.Location);
        Say("id", query.Id?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Say("pattern", query.Pattern);
        Say("sort", query.Sort);
        Say("order", query.Order);
        Say(ClusterView.Query, cluster);

        return said.Count == 0 ? "/event-subscriptions" : "/event-subscriptions?" + string.Join('&', said);
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
