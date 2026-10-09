using Xmip.Surface;

namespace Xmip.Operations.Configuration;

/// <summary>
/// Where the desktop's cluster lives and where its nodes' slices go: the
/// cluster's <c>xmip.toml</c>, the directory each node's slice is written
/// to as <c>&lt;node&gt;/xmip-node.toml</c> — the name desired state writes
/// it by (<c>deployment-model.md</c> section 8) — and the node of it this
/// desktop starts, as <c>xmip.gui.toml</c> declares them. Every slice is the
/// runtime's one slicing (<c>xmip_cluster_slices_v1</c>,
/// <c>configure::slice</c>); nothing here takes the file apart.
/// </summary>
/// <remarks>
/// Shipping puts a slice where its node reads it. The node this desktop
/// starts reads its slice here, so writing it is shipping it. Xmip has no
/// path yet that puts a file on another node: desired state slices the
/// cluster's file on each node as it deploys it (the Ansible role
/// <c>xmip_node</c>), and the node's
/// operate listener (ADR-0067) takes no configuration. Another node's slice
/// is written here and said to be not shipped, never sent by a transport
/// invented for it.
/// </remarks>
/// <param name="clusterPath">The cluster's <c>xmip.toml</c>.</param>
/// <param name="directory">Where each node's slice is written.</param>
/// <param name="node">The node this desktop starts; null where it starts
/// none.</param>
public sealed class SliceDelivery(string clusterPath, string directory, string? node)
{
    /// <summary>The file name a node reads its slice from.</summary>
    public const string FileName = "xmip-node.toml";

    /// <summary>The cluster's <c>xmip.toml</c>.</summary>
    public string ClusterPath => clusterPath;

    /// <summary>Where each node's slice is written.</summary>
    public string Directory => directory;

    /// <summary>The node this desktop starts; null where it starts none.</summary>
    public string? Node => node;

    /// <summary>Where <paramref name="name"/>'s slice is written.</summary>
    public string PathOf(string name)
    {
        return Path.Combine(directory, name, FileName);
    }

    /// <summary>Slice <paramref name="cluster"/> for every node it declares
    /// and put each slice where it can go, node by node: one node's refusal
    /// stops no other's.</summary>
    public IReadOnlyList<NodeDelivery> Deliver(ClusterConfiguration cluster)
    {
        ArgumentNullException.ThrowIfNull(cluster);

        return (NodeDelivery[])[.. cluster.Nodes.Select(name => Deliver(cluster.Text, name))];
    }

    /// <summary>Slice <paramref name="text"/>, a cluster's file, for
    /// <paramref name="name"/> and put the slice where it can go.</summary>
    public NodeDelivery Deliver(string text, string name)
    {
        if (!NodeSlice.TrySlice(
            text, name, out IReadOnlyList<NodeSlice> slices, out string refusal))
        {
            return new NodeDelivery(name, NodeDelivery.Standing.Refused, string.Empty, refusal);
        }

        string path = PathOf(name);

        try
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temp = path + ".writing";
            File.WriteAllText(temp, slices[0].Text);
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new NodeDelivery(
                name, NodeDelivery.Standing.Refused, string.Empty,
                $"sliced, and not written to {path}: {exception.Message}");
        }

        return name == node
            ? new NodeDelivery(
                name, NodeDelivery.Standing.Shipped, path,
                $"shipped to {path}, where this desktop starts it; it takes effect when the " +
                "node starts again")
            : new NodeDelivery(
                name, NodeDelivery.Standing.NotShipped, path,
                $"sliced to {path} and not shipped: Xmip has no path yet that puts a file on " +
                "another node; desired state slices the cluster's xmip.toml on the node as it " +
                "deploys it");
    }
}
