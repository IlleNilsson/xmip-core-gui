using Xmip.Operations.Configuration;
using Xmip.Surface;

namespace Xmip.Operations.Test;

/// <summary>
/// The desktop edits the cluster's <c>xmip.toml</c> and nothing else
/// (ADR-0031, amendment 2026-10-05), and every view, edit and refusal is the
/// runtime's (<c>xmip_operate.h</c> section 10): what is asserted here is
/// what the runtime this estate built answers.
/// </summary>
public sealed class ClusterConfigurationTest : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("xmip-cluster-configuration-").FullName;

    [Fact]
    public void TheSampleOpensAsAClusterAndValidatesInTheRuntime()
    {
        string sample = Path.Combine(
            Estate.Root(), "module", "core", "operation", "gui", "samples", "xmip.toml");
        ClusterConfiguration cluster = ClusterConfiguration.Open(sample);

        Assert.True(cluster.Editable, cluster.Refusal);
        Assert.True(cluster.Views.IsCluster);
        Assert.NotEmpty(cluster.Nodes);

        using NativeOperator runtime = new(Estate.Library());
        ConfigurationVerdict verdict = runtime.Validate(cluster.Path, cluster.Text);
        Assert.True(verdict.Ok, verdict.Said);
    }

    [Fact]
    public void ItOpensOnTheClusterItsNodesAndEveryArtifactKind()
    {
        ClusterConfiguration cluster = Estate.Written(_directory);

        Assert.Equal(Estate.Cluster.Name, cluster.Name);
        Assert.Equal(Estate.Cluster.Nodes.Take(2), cluster.Nodes);
        Assert.Equal("cluster", cluster.Views.Views[0].Kind);

        // The kinds are the runtime's; one it does not define says so.
        ArtifactView? undefined = cluster.Views.Views.FirstOrDefault(view => !view.Defined);
        Assert.NotNull(undefined);
        Assert.False(string.IsNullOrEmpty(undefined.Note));
        Assert.Contains(
            cluster.Views.Of("receive-location")!.Entries,
            entry => entry.Name == Estate.Location);
    }

    [Fact]
    public void AValueIsEditedInPlaceAndTheRestIsKept()
    {
        ClusterConfiguration cluster = Estate.Written(_directory);
        ArtifactEntry location = cluster.Views.Of("receive-location")!.Entries[0];

        Assert.True(
            cluster.TryEdit(
                new ClusterEdit.Set(location.Section, ["address"], "\"C:/xmip/elsewhere\""),
                out string refusal),
            refusal);

        Assert.True(cluster.Changed);
        Assert.Contains("C:/xmip/elsewhere", cluster.Text, StringComparison.Ordinal);
        Assert.Contains(Estate.Comment, cluster.Text, StringComparison.Ordinal);
        Assert.Contains(
            cluster.Views.Of("receive-location")!.Entries[0].Fields,
            field => field.Value == "\"C:/xmip/elsewhere\"");
    }

    [Fact]
    public void EntriesAndNodesAreAddedAndRemovedThroughTheRuntime()
    {
        ClusterConfiguration cluster = Estate.Written(_directory);
        ArtifactView sends = cluster.Views.Of("send-location")!;
        ArtifactPlace place = sends.Places.First(at => at.Scope == "cluster");

        // The runtime refuses an entry that would not read, naming what it
        // misses; given its values, the entry is added.
        Assert.False(
            cluster.TryEdit(new ClusterEdit.AddEntry(place.Section, "out"), out string r0));
        Assert.Contains("start", r0, StringComparison.Ordinal);
        ArtifactField[] values =
        [
            new(["start"], "false", ""),
            new(["transport"], "\"file\"", ""),
            new(["address"], "\"C:/xmip/out\"", ""),
        ];
        Assert.True(
            cluster.TryEdit(new ClusterEdit.AddEntry(place.Section, "out", values), out string r1),
            r1);
        ArtifactEntry added = Assert.Single(cluster.Views.Of("send-location")!.Entries);
        Assert.Equal("out", added.Name);

        Assert.True(cluster.TryEdit(new ClusterEdit.RemoveEntry(added.Section), out string r2), r2);
        Assert.Empty(cluster.Views.Of("send-location")!.Entries);

        string third = Estate.Cluster.Nodes[2];
        Assert.True(cluster.TryEdit(new ClusterEdit.AddNode(third), out string r3), r3);
        Assert.Contains(third, cluster.Nodes);
    }

    [Fact]
    public void AnEditTheRuntimeRefusesLeavesTheTextAndSaysWhy()
    {
        ClusterConfiguration cluster = Estate.Written(_directory);
        string before = cluster.Text;

        ClusterEdit twice = new ClusterEdit.AddNode(Estate.Cluster.Nodes[0]);
        Assert.False(cluster.TryEdit(twice, out string refusal));
        Assert.Contains(Estate.Cluster.Nodes[0], refusal, StringComparison.Ordinal);
        Assert.False(cluster.TryEdit(
            new ClusterEdit.Set(["service"], ["name"], "not toml"), out string notToml));
        Assert.NotEmpty(notToml);

        Assert.Equal(before, cluster.Text);
        Assert.False(cluster.Changed);
    }

    [Fact]
    public void ANodesOwnDocumentIsNeverOfferedForEditing()
    {
        ClusterConfiguration cluster = Estate.Written(_directory);
        Assert.True(NodeSlice.TrySlice(
            cluster.Text,
            Estate.Cluster.Nodes[0],
            out IReadOnlyList<NodeSlice> slices,
            out string why),
            why);

        string node = Path.Combine(_directory, "xmip-node.toml");
        File.WriteAllText(node, slices[0].Text);
        ClusterConfiguration opened = ClusterConfiguration.Open(node);

        Assert.False(opened.Editable);
        Assert.Contains("[nodes]", opened.Refusal, StringComparison.Ordinal);
        Assert.False(opened.TryEdit(new ClusterEdit.AddNode("x"), out string refusal));
        Assert.Equal(opened.Refusal, refusal);
    }

    [Fact]
    public void AClusterBegunHereIsEditableAndBecomesOneWhenItDeclaresANode()
    {
        ClusterConfiguration cluster =
            ClusterConfiguration.Open(Path.Combine(_directory, "new.toml"));

        Assert.False(cluster.Exists);
        Assert.True(cluster.Editable, cluster.Refusal);
        Assert.True(cluster.TryEdit(
            new ClusterEdit.Set(["service"], ["cluster_name"], $"\"{Estate.Cluster.Name}\""),
            out string r1), r1);
        Assert.True(cluster.Editable, cluster.Refusal);
        Assert.False(cluster.Views.IsCluster);

        Assert.True(
            cluster.TryEdit(new ClusterEdit.AddNode(Estate.Cluster.Nodes[0]), out string r2), r2);
        Assert.True(cluster.Views.IsCluster);

        Assert.True(cluster.TryWrite(out string stale), stale);
        Assert.True(cluster.Exists);
        Assert.False(cluster.Changed);
        Assert.Equal(cluster.Text, File.ReadAllText(cluster.Path));
    }

    [Fact]
    public void ASaveRefusesAFileAnotherEditorChangedAndOverwritesNothing()
    {
        ClusterConfiguration cluster = Estate.Written(_directory);
        Assert.True(cluster.TryEdit(
            new ClusterEdit.Set(["service"], ["name"], "\"mine\""), out string edit), edit);
        string theirs = File.ReadAllText(cluster.Path) + "# saved by another editor\n";
        File.WriteAllText(cluster.Path, theirs);

        Assert.False(cluster.TryWrite(out string refusal));
        Assert.Contains("changed on disk", refusal, StringComparison.Ordinal);
        Assert.Equal(theirs, File.ReadAllText(cluster.Path));
        Assert.True(cluster.Changed);

        // Opened again, it holds the other change, and saves over it.
        ClusterConfiguration again = ClusterConfiguration.Open(cluster.Path);
        Assert.Contains("another editor", again.Text, StringComparison.Ordinal);
        Assert.True(again.TryEdit(
            new ClusterEdit.Set(["service"], ["name"], "\"mine\""), out string redo), redo);
        Assert.True(again.TryWrite(out string none), none);
        Assert.Contains("another editor", File.ReadAllText(cluster.Path), StringComparison.Ordinal);
    }

    [Fact]
    public void AFileThatAppearedSinceAnEmptyOpenIsNotOverwritten()
    {
        ClusterConfiguration cluster =
            ClusterConfiguration.Open(Path.Combine(_directory, "new.toml"));
        Assert.True(cluster.TryEdit(
            new ClusterEdit.AddNode(Estate.Cluster.Nodes[0]), out string edit), edit);
        File.WriteAllText(cluster.Path, "# written elsewhere\n");

        Assert.False(cluster.TryWrite(out _));
        Assert.Equal("# written elsewhere\n", File.ReadAllText(cluster.Path));
    }

    [Fact]
    public void ASaveLeavesNoTemporaryFileAndSavesAgainAfterItself()
    {
        ClusterConfiguration cluster = Estate.Written(_directory);

        for (int save = 0; save < 2; save++)
        {
            Assert.True(cluster.TryEdit(
                new ClusterEdit.Set(["service"], ["name"], $"\"save-{save}\""), out string e), e);
            Assert.True(cluster.TryWrite(out string stale), stale);
        }

        Assert.Equal(["xmip.toml"], Directory.GetFiles(_directory).Select(Path.GetFileName));
        Assert.Contains("save-1", File.ReadAllText(cluster.Path), StringComparison.Ordinal);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }
}
