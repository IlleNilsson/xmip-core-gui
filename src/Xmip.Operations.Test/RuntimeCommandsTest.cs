using Xmip.Operations.Configuration;
using Xmip.Surface;

namespace Xmip.Operations.Test;

/// <summary>
/// The desktop's acts on the cluster's <c>xmip.toml</c> are the operator's
/// and are audited (ADR-0062): a save, each node's slice and ship, a refused
/// edit, a plan. Each is read back here from the audit directory the
/// desktop was told, as the audit capability wrote it.
/// </summary>
public sealed class RuntimeCommandsTest : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("xmip-runtime-commands-").FullName;

    private readonly NativeOperator _runtime = new(Estate.Library());

    private string AuditDirectory => Path.Combine(_directory, "audit");

    private RuntimeCommands Commands(string clusterPath, string? node)
    {
        return new RuntimeCommands(
            _runtime,
            _runtime.Path,
            new ProgramAudit("xmip-operations", AuditDirectory),
            new SliceDelivery(clusterPath, Path.Combine(_directory, "slices"), node));
    }

    private string Audited()
    {
        return File.ReadAllText(Path.Combine(AuditDirectory, "audit.toml"));
    }

    [Fact]
    public void ASaveWritesTheFileSlicesEachNodeAndAuditsEveryStep()
    {
        ClusterConfiguration cluster = Estate.Written(_directory);
        string here = Estate.Cluster.Nodes[0];
        Assert.True(cluster.TryEdit(
            new ClusterEdit.Set(["service"], ["name"], "\"xmip-saved\""), out string edit), edit);

        ClusterSave saved = Commands(cluster.Path, here).Save(cluster);

        Assert.True(saved.Saved, saved.Said);
        Assert.False(cluster.Changed);
        Assert.Contains("xmip-saved", File.ReadAllText(cluster.Path), StringComparison.Ordinal);
        Assert.Equal(cluster.Nodes, saved.Nodes.Select(node => node.Node));
        Assert.Equal(NodeDelivery.Standing.Shipped, saved.Nodes[0].Outcome);
        Assert.Equal(NodeDelivery.Standing.NotShipped, saved.Nodes[1].Outcome);

        string audit = Audited();
        Assert.Contains("save cluster configuration", audit, StringComparison.Ordinal);
        Assert.Contains("\"slice\"", audit, StringComparison.Ordinal);
        Assert.Contains("\"ship\"", audit, StringComparison.Ordinal);
        Assert.Contains("\"validate\"", audit, StringComparison.Ordinal);
    }

    [Fact]
    public void ANodesOwnDocumentIsNotSavedAndTheRefusalIsAudited()
    {
        ClusterConfiguration cluster =
            ClusterConfiguration.Open(Path.Combine(_directory, "new.toml"));
        Assert.True(cluster.TryEdit(
            new ClusterEdit.Set(["service"], ["name"], "\"xmip\""), out string edit), edit);

        ClusterSave saved = Commands(cluster.Path, null).Save(cluster);

        Assert.False(saved.Saved);
        Assert.Empty(saved.Nodes);
        Assert.False(File.Exists(cluster.Path));
        Assert.Contains("save cluster configuration", Audited(), StringComparison.Ordinal);
    }

    [Fact]
    public void ARefusedEditIsAuditedWithTheRuntimesSentence()
    {
        ClusterConfiguration cluster = Estate.Written(_directory);
        ClusterEdit twice = new ClusterEdit.AddNode(Estate.Cluster.Nodes[0]);
        Assert.False(cluster.TryEdit(twice, out string refusal));

        Commands(cluster.Path, null).Refused(twice, cluster.Path, refusal);

        string audit = Audited();
        Assert.Contains("edit cluster configuration", audit, StringComparison.Ordinal);
        Assert.Contains(Estate.Cluster.Nodes[0], audit, StringComparison.Ordinal);
    }

    [Fact]
    public void TheNodeConfiguredHereIsPlannedFromItsSliceAndSaidNotToRun()
    {
        string here = Estate.Cluster.Nodes[0];
        ClusterConfiguration cluster = Estate.Written(_directory);
        RuntimeCommands commands = Commands(cluster.Path, here);

        ConfigurationVerdict planned = commands.PlanNode();

        // Planned from the slice, whatever the runtime then says of it, and
        // never said to have started: xmip_start_v1 runs nothing.
        Assert.Equal(commands.Delivery.PathOf(here), planned.Path);
        Assert.True(File.Exists(planned.Path));
        Assert.DoesNotContain("started", planned.Said, StringComparison.Ordinal);
        Assert.Contains("plan node", Audited(), StringComparison.Ordinal);
    }

    [Fact]
    public void ADesktopNamingNoNodePlansNone()
    {
        ClusterConfiguration cluster = Estate.Written(_directory);

        ConfigurationVerdict refused = Commands(cluster.Path, null).PlanNode();

        Assert.False(refused.Ok);
        Assert.Contains("names no Node", refused.Said, StringComparison.Ordinal);
    }

    [Fact]
    public void ASaveOverAnotherEditorsChangeIsRefusedStaleAndAudited()
    {
        ClusterConfiguration cluster = Estate.Written(_directory);
        Assert.True(cluster.TryEdit(
            new ClusterEdit.Set(["service"], ["name"], "\"mine\""), out string edit), edit);
        File.AppendAllText(cluster.Path, "# theirs\n");

        ClusterSave saved = Commands(cluster.Path, null).Save(cluster);

        Assert.False(saved.Saved);
        Assert.True(saved.Stale);
        Assert.Empty(saved.Nodes);
        Assert.Contains("changed on disk", saved.Said, StringComparison.Ordinal);
        Assert.Contains("# theirs", File.ReadAllText(cluster.Path), StringComparison.Ordinal);
        Assert.Contains("save cluster configuration", Audited(), StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _runtime.Dispose();
        Directory.Delete(_directory, recursive: true);
    }
}
