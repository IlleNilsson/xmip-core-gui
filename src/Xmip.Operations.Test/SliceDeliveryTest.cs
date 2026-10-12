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

    [Fact]
    public async Task ConcurrentDeliveriesOfOneNodeDoNotCollide()
    {
        string path = Estate.Written(_directory).Path;
        string here = Estate.Cluster.Nodes[0];
        SliceDelivery delivery = new(path, Path.Combine(_directory, "slices"), here);
        const int Writers = 8;
        const string NotWritten = "REFUSED: sliced, and not written";

        // Each writer delivers a text of its own, so a slice written whole is
        // told from one half one writer's and half another's.
        string[] texts = [.. Enumerable.Range(0, Writers).Select(writer =>
        {
            ClusterConfiguration cluster = ClusterConfiguration.Open(path);
            Assert.True(cluster.TryEdit(
                new ClusterEdit.Set(["service"], ["name"], $"\"w{writer}\""), out string e), e);
            return cluster.Text;
        })];
        string[] slices = [.. texts.Select(text =>
        {
            Assert.True(NodeSlice.TrySlice(
                text, here, out IReadOnlyList<NodeSlice> slice, out string why), why);
            return slice[0].Text;
        })];

        for (int round = 0; round < 20; round++)
        {
            using Barrier start = new(Writers);
            Task<NodeDelivery>[] deliveries = [.. texts.Select(text =>
                Task.Factory.StartNew(
                    () =>
                    {
                        start.SignalAndWait();
                        return delivery.Deliver(text, here);
                    },
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default))];

            // No writer trips over another's temporary file. Where the
            // platform refuses two renames onto one file at the same instant
            // (Windows), the one refused says so in words, and the slice on
            // disk is still one writer's, whole.
            NodeDelivery[] delivered = await Task.WhenAll(deliveries);
            Assert.Contains(delivered, node => node.Outcome == NodeDelivery.Standing.Shipped);
            Assert.All(delivered, node => Assert.True(
                node.Outcome == NodeDelivery.Standing.Shipped
                    || node.Said.StartsWith(NotWritten, StringComparison.Ordinal),
                node.Said));
            Assert.Contains(File.ReadAllText(delivery.PathOf(here)), slices);
            Assert.Equal(
                [SliceDelivery.FileName],
                Directory.GetFiles(Path.GetDirectoryName(delivery.PathOf(here))!)
                    .Select(Path.GetFileName));
        }
    }

    [Fact]
    public void ADeliveryIsNotStoppedByAnotherDeliveryWritingBesideIt()
    {
        ClusterConfiguration cluster = Estate.Written(_directory);
        string here = Estate.Cluster.Nodes[0];
        SliceDelivery delivery = new(cluster.Path, Path.Combine(_directory, "slices"), here);
        string slice = delivery.PathOf(here);
        Directory.CreateDirectory(Path.GetDirectoryName(slice)!);

        // Another delivery's temporary file, open and being written: once,
        // every delivery wrote <slice>.writing, and this one was refused.
        using (new FileStream(
            $"{slice}.writing", FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            NodeDelivery delivered = delivery.Deliver(cluster.Text, here);

            Assert.True(delivered.Outcome == NodeDelivery.Standing.Shipped, delivered.Said);
        }
    }

    [Fact]
    public void ADeliveryWhoseClusterTextWasReplacedSinceDeliversNothing()
    {
        ClusterConfiguration cluster = Estate.Written(_directory);
        SliceDelivery delivery =
            new(cluster.Path, Path.Combine(_directory, "slices"), Estate.Cluster.Nodes[0]);

        // A newer save replaced the file after this one read it.
        File.AppendAllText(cluster.Path, "# saved by a newer save\n");

        IReadOnlyList<NodeDelivery> delivered = delivery.Deliver(cluster);

        Assert.Equal(cluster.Nodes, delivered.Select(node => node.Node));
        Assert.All(delivered, node =>
        {
            Assert.Equal(NodeDelivery.Standing.Refused, node.Outcome);
            Assert.StartsWith("REFUSED:", node.Said, StringComparison.Ordinal);
            Assert.Contains("a newer save replaced it", node.Said, StringComparison.Ordinal);
            Assert.False(File.Exists(delivery.PathOf(node.Node)));
        });
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }
}
