using Xmip.Operations.Configuration;
using Xmip.Surface;

namespace Xmip.Operations.Test;

/// <summary>
/// Saving the cluster's <c>xmip.toml</c> slices it into each node's document
/// and ships each where it can go (ADR-0031, amendment 2026-10-05). Every
/// slice is the runtime's one slicing; the node this desktop starts is
/// shipped by being written where it starts from, and another node's slice
/// is written and said not shipped, since no Xmip path puts a file on
/// another node.
/// </summary>
public sealed class SliceDeliveryTest : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("xmip-slice-delivery-").FullName;

    [Fact]
    public void EachNodeIsSlicedTheNodeStartedHereShippedAndAnotherNot()
    {
        ClusterConfiguration cluster = Estate.Written(_directory);
        string here = Estate.Cluster.Nodes[0];
        string there = Estate.Cluster.Nodes[1];
        SliceDelivery delivery = new(cluster.Path, Path.Combine(_directory, "slices"), here);

        IReadOnlyList<NodeDelivery> delivered = delivery.Deliver(cluster);

        Assert.Equal([here, there], delivered.Select(node => node.Node));

        NodeDelivery shipped = delivered[0];
        Assert.Equal(NodeDelivery.Standing.Shipped, shipped.Outcome);
        Assert.Equal(delivery.PathOf(here), shipped.Path);
        Assert.True(NodeSlice.TrySlice(
            cluster.Text, here, out IReadOnlyList<NodeSlice> slice, out string why), why);
        Assert.Equal(slice[0].Text, File.ReadAllText(shipped.Path));

        NodeDelivery written = delivered[1];
        Assert.Equal(NodeDelivery.Standing.NotShipped, written.Outcome);
        Assert.True(File.Exists(written.Path));
        Assert.Contains("not shipped", written.Said, StringComparison.Ordinal);
    }

    [Fact]
    public void ANodeTheRuntimeWillNotSliceIsRefusedInItsWordsAndStopsNoOther()
    {
        string refused = Estate.Cluster.Nodes[1];

        // A node whose own [service] names another than its key does not
        // slice, and the runtime refuses an edit that would write one; a
        // file written by hand can hold it. The other node still slices.
        string path = Estate.Written(_directory).Path;
        File.AppendAllText(path, $"[nodes.{refused}.service]\nnode_name = \"elsewhere\"\n");
        ClusterConfiguration cluster = ClusterConfiguration.Open(path);
        SliceDelivery delivery = new(cluster.Path, Path.Combine(_directory, "slices"), null);

        IReadOnlyList<NodeDelivery> delivered = delivery.Deliver(cluster);

        NodeDelivery bad = Assert.Single(delivered, node => node.Node == refused);
        Assert.Equal(NodeDelivery.Standing.Refused, bad.Outcome);
        Assert.False(File.Exists(delivery.PathOf(refused)));
        Assert.NotEmpty(bad.Said);
        Assert.Contains(delivered, node => node.Outcome == NodeDelivery.Standing.NotShipped);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }
}
