using Xmip.Surface;

namespace Xmip.Gui.Surface;

/// <summary>
/// What every link out of a view carries beside its own words: the cluster it
/// was written on, where the host lists more than one (ADR-0052, amendment
/// 2026-09-20), and whether the view shows test clusters — the clusters whose
/// run declared itself hidden (the owner, 2026-09-29; ADR-0052, amendment
/// 2026-09-30). Both are in the address, so a link, a reload and a second tab
/// reproduce the view; a host that lists one cluster and shows no test
/// clusters writes the addresses it always wrote.
/// </summary>
/// <param name="Cluster">The cluster the link was written on, or null where
/// the host lists one.</param>
/// <param name="IncludeHidden">Whether the view shows test clusters.</param>
public sealed record Carry(string? Cluster, bool IncludeHidden = false)
{
    /// <summary>The word the address carries the box under:
    /// <c>hidden=include</c>, the audit capability's own words, so the Audit
    /// view's query and every other view's address say it one way.</summary>
    public const string Query = AuditQuery.HiddenKey;

    /// <summary>What <see cref="Query"/> says when test clusters are
    /// shown.</summary>
    public const string Included = AuditQuery.Included;

    /// <summary>A link that carries a cluster and nothing else — what every
    /// link carried before test clusters could be hidden.</summary>
    public static implicit operator Carry(string? cluster)
    {
        return FromString(cluster);
    }

    /// <summary>The named alternate of the conversion from a cluster's
    /// name.</summary>
    public static Carry FromString(string? cluster)
    {
        return new Carry(cluster);
    }

    /// <summary>The address's words for this carry, in order, those that say
    /// something: <c>cluster</c>, then <c>hidden</c>.</summary>
    public IEnumerable<(string Key, string Value)> Words()
    {
        if (!string.IsNullOrEmpty(Cluster))
        {
            yield return (ClusterView.Query, Cluster);
        }

        if (IncludeHidden)
        {
            yield return (Query, Included);
        }
    }

    /// <summary>Whether an address's word says test clusters are
    /// shown.</summary>
    public static bool Says(string? word)
    {
        return string.Equals(word, Included, StringComparison.Ordinal);
    }
}
