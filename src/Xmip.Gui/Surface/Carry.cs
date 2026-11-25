namespace Xmip.Gui.Surface;

/// <summary>
/// What every link out of a view carries beside its own words: the cluster it
/// was written on, where the host lists more than one (ADR-0052, amendment
/// 2026-09-20). It is in the address, so a link, a reload and a second tab
/// reproduce the view; a host that lists one cluster writes the addresses it
/// always wrote.
/// </summary>
/// <param name="Cluster">The cluster the link was written on, or null where
/// the host lists one.</param>
public sealed record Carry(string? Cluster)
{
    /// <summary>A link that carries a cluster.</summary>
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

    /// <summary>The address's words for this carry, those that say something:
    /// <c>cluster</c>.</summary>
    public IEnumerable<(string Key, string Value)> Words()
    {
        if (!string.IsNullOrEmpty(Cluster))
        {
            yield return (ClusterView.Query, Cluster);
        }
    }
}
