using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Gui.Pages;
using Xmip.Gui.Surface;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// One host, two clusters (ADR-0052, amendment 2026-09-20). The amendment of
/// 2026-09-14 left *one page navigating between them* queued; this is that
/// page, on all three views. Rendered over the surface library's two cluster
/// fixtures: the test cluster with its receiving, processing and sending
/// nodes, and a second cluster with a receiving node and a done sending one.
/// </summary>
public sealed class TwoClustersTest : BunitContext
{
    private static readonly TestCluster Names = TestCluster.Read();
    private static readonly string First = Names.Name;
    private static readonly string Receiver = Names.WithRole("receiving");
    private static readonly string Sender = Names.WithRole("sending");

    /// <summary>The second cluster's name, as its publication says it.</summary>
    private readonly string second;

    public TwoClustersTest()
    {
        ClusterSurfaces surfaces = ClusterSurfaces.Over(
            [Snapshot("cluster.toml"), Snapshot("cluster-c2.toml")]);
        second = surfaces.Clusters.Single(name => name != First);
        Services.AddSingleton(surfaces);
        Services.AddSingleton(new RoleContext(Role.Observer, "tester"));
        Services.AddSingleton(new ProgramAudit("Xmip.Gui.Test", Unacted.Audit));
    }

    private static SnapshotOperator Snapshot(string fixture)
    {
        return new SnapshotOperator(Path.Combine(AppContext.BaseDirectory, "Fixture", fixture));
    }

    private void Address(string uri)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(uri);
    }

    private static string[] Picked(IRenderedComponent<IComponent> page)
    {
        return [.. page.FindAll("a.cluster-pick").Select(pick => pick.TextContent.Trim())];
    }

    private static string Current(IRenderedComponent<IComponent> page)
    {
        return page.Find("a.cluster-pick.current").TextContent.Trim();
    }

    [Fact]
    public void EveryViewSaysWhichClusterItIsOnAndOffersTheOther()
    {
        // The owner has said twice the views are crowded, and there is a run
        // line and a filter line already: the chooser is on the run line, not
        // on a line of its own, and every view carries the same one.
        foreach (IRenderedComponent<IComponent> page in
            (IRenderedComponent<IComponent>[])
                [Render<Cluster>(), Render<Configuration>(), Render<Topology>()])
        {
            Assert.Equal([First, second], Picked(page));
            Assert.Equal(First, Current(page));
            Assert.Single(page.FindAll("p.run-line"));
            Assert.Single(page.FindAll("p.run-line span.run-clusters"));
        }
    }

    [Fact]
    public void AMonitorRowNamesTheNodeItsRecordIsOnAndNeverTheCluster()
    {
        // Open problem 25, row q: the node column took the first segment,
        // which is the cluster. The node is observe's reading, asked of the
        // runtime, and the cluster's own rollup is on no node.
        IRenderedComponent<Cluster> page = Render<Cluster>();
        Dictionary<string, string> nodeOf = page.FindAll("section.list .row").ToDictionary(
            row => row.QuerySelector(".scope")?.TextContent ?? string.Empty,
            row => row.QuerySelector(".node")?.TextContent ?? string.Empty);

        Assert.Equal(Sender, nodeOf[$"node/{Sender}/send/tcp/json"]);
        Assert.Equal(string.Empty, nodeOf["node"]);
        Assert.DoesNotContain(First, nodeOf.Values);
    }

    [Fact]
    public void AViewToldAClusterReadsThatClustersPublicationAndNoOther()
    {
        Address($"/configuration?cluster={second}");
        IRenderedComponent<Configuration> page = Render<Configuration>();

        Assert.Equal(second, Current(page));
        Assert.Contains(
            $"RoundTrip · {second} · nodes {Receiver}=receiving {Sender}=sending"
                + $" · online {Receiver} · harsh",
            page.Find("p.run-line").TextContent,
            StringComparison.Ordinal);

        // The second's tree, and nothing of the first's: two clusters are two
        // scope trees.
        Assert.NotEmpty(
            page.FindAll($"#{ScopeLink.Anchor($"{ScopeTree.Root}{second}/node/{Receiver}")}"));
        Assert.Empty(page.FindAll($"#{ScopeLink.Anchor($"{Names.Scope}/node/{Receiver}")}"));
    }

    [Fact]
    public void EachViewsChooserKeepsTheViewAndStartsTheDrillOver()
    {
        // A scope of the cluster being left names nothing in the one being
        // entered, so the address carries the cluster and nothing else.
        Address($"/topology?cluster={First}");
        IRenderedComponent<Topology> topology = Render<Topology>();

        Assert.Equal(
            $"/topology?cluster={second}",
            topology.FindAll("a.cluster-pick")[1].GetAttribute("href"));

        Address($"/?scope={Uri.EscapeDataString(Names.Scope)}&cluster={First}");
        IRenderedComponent<Cluster> monitor = Render<Cluster>();

        Assert.Equal(
            $"/?cluster={second}", monitor.FindAll("a.cluster-pick")[1].GetAttribute("href"));
    }

    [Fact]
    public void ALinkOutOfAViewCarriesTheClusterItWasWrittenOn()
    {
        // The three views lead to one another (ADR-0052, amendment 2026-09-18);
        // a link that dropped the cluster would land on the other cluster's
        // tree at a scope that is not in it.
        string root = $"{ScopeTree.Root}{second}";
        string leaf = $"{root}/node/{Sender}/send/tcp/json";
        Address($"/configuration?cluster={second}");
        IElement row = Render<Configuration>().Find($"#{ScopeLink.Anchor(leaf)}");

        Assert.Equal(
            ScopeLink.Monitor(leaf, second),
            row.QuerySelector("a.tree-link:not(.problem)")?.GetAttribute("href"));
        Assert.Contains(
            $"cluster={second}", ScopeLink.Monitor(root, second), StringComparison.Ordinal);
        Assert.Equal(
            $"configuration?cluster={second}#s-xmip----{second}",
            ScopeLink.Configuration(root, second));
        Assert.Equal($"/topology?cluster={second}", ScopeLink.Topology(second));
    }

    [Fact]
    public void AClusterThisHostDoesNotHoldFallsBackToTheFirstRatherThanFailing()
    {
        // A roll that ended leaves a link behind. The answer is the other
        // cluster, never an error page.
        Address($"/configuration?cluster={First}{second}");
        IRenderedComponent<Configuration> page = Render<Configuration>();

        Assert.Equal(First, Current(page));
        Assert.NotEmpty(page.FindAll($"#{ScopeLink.Anchor($"{Names.Scope}/node/{Receiver}")}"));
    }

    [Fact]
    public void AHostWithOneClusterDrawsNoChooserAndWritesTheAddressesItAlwaysWrote()
    {
        using BunitContext alone = new();
        alone.Services.AddSingleton(ClusterSurfaces.Over(Snapshot("cluster.toml")));
        alone.Services.AddSingleton(new RoleContext(Role.Observer, "tester"));
        alone.Services.AddSingleton(new ProgramAudit("Xmip.Gui.Test", Unacted.Audit));

        IRenderedComponent<Configuration> page = alone.Render<Configuration>();

        string node = $"{Names.Scope}/node/{Receiver}";
        Assert.Empty(page.FindAll("a.cluster-pick"));
        Assert.Equal(
            ScopeLink.Monitor(node),
            page.Find($"#{ScopeLink.Anchor(node)}")
                .QuerySelector("a.tree-link:not(.problem)")?.GetAttribute("href"));
        Assert.Equal(
            $"configuration#s-xmip----{First}", ScopeLink.Configuration(Names.Scope));
        Assert.Equal("/topology", ScopeLink.Topology());
    }
}
