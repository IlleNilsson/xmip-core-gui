using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Abi.Operate;
using Xmip.Gui.Pages;
using Xmip.Gui.Surface;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// One host, one surface per cluster, each of its own kind (the design
/// approved 2026-11-12, sections 3.4 and 3.5): the test cluster local at a
/// node that gathers its view, and the second cluster a snapshot. The run
/// line names both, and says where the records in view come from: for the
/// gathered one, after where the node is reached, what its view says of
/// itself — who gathered it and how many nodes it reached, in the node's own
/// words, never composed here. Web and Desktop render this one component.
/// </summary>
public sealed class GatheredClusterViewTest : BunitContext
{
    private static readonly TestCluster Names = TestCluster.Read();

    private static readonly string Words =
        $"{Names.Name}, gathered by a monitoring node: 3 of 3 nodes reached";

    public GatheredClusterViewTest()
    {
        Services.AddSingleton(new RoleContext(Role.Observer, RoleAssignment.ProcessCaller()));
        Services.AddSingleton(new ProgramAudit("Xmip.Gui.Test", Unacted.Audit));
    }

    [Fact]
    public void TheRunLineNamesEachClusterAndSaysWhoGatheredTheOneInView()
    {
        using HeldCaller me = RuntimeLibrary.Rules.SecurityRoles.Process(out string refused)
            ?? throw new InvalidOperationException(refused);
        using ServedNode served = RuntimeLibrary.Rules.Remotes.ServeLocal(
            $"xmip-gathered-view-test-{Guid.NewGuid():n}", [me.Name], [], [], "gathered-view",
            out string reason) ?? throw new InvalidOperationException(reason);
        served.Post(File.ReadAllText(Fixture("snapshot.xmip")));
        served.PostCluster(Gathered());

        using ClusterSurfaces surfaces = ClusterSurfaces.Over(
            [new LocalOperator(served.Address), new SnapshotOperator(Fixture("cluster-cl2.xmip"))]);
        string second = surfaces.Clusters[1];
        Services.AddSingleton(surfaces);

        IRenderedComponent<Cluster> gathered = Render<Cluster>();

        Assert.Equal(
            [Names.Name, second],
            gathered.FindAll("a.cluster-pick").Select(pick => pick.TextContent.Trim()));
        Assert.Equal(
            $"LOCAL — {served.Address} — {Words}",
            gathered.Find("p.run-line span.source").TextContent.Trim());

        Services.GetRequiredService<NavigationManager>()
            .NavigateTo($"/?{ClusterView.Query}={second}");
        IRenderedComponent<Cluster> snapshot = Render<Cluster>();

        Assert.StartsWith(
            "SNAPSHOT — ", snapshot.Find("p.run-line span.source").TextContent.Trim(),
            StringComparison.Ordinal);
    }

    private static string Fixture(string name)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixture", name);
    }

    // The cluster fixture as a gathered view, saying the words of itself.
    private static string Gathered()
    {
        string[] lines = File.ReadAllText(Fixture("cluster.xmip")).Split('\n');
        int said = Array.FindIndex(
            lines, line => line.StartsWith("source = ", StringComparison.Ordinal));
        lines[said] = $"source = \"{Words}\"";
        return string.Join('\n', lines);
    }
}
