using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Gui.Pages;
using Xmip.Gui.Surface;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// The surface library's cluster fixture across the three views: every view
/// says what the run was started with; the Configuration tree has one cluster
/// row, heading it, and says of each row what its publisher says it is; the
/// Monitor counts the cluster's nodes; what a node declares is visible where
/// an operator looks at that node; and every level of the topology drills to
/// the same scope in the other two views. The owner, 2026-09-25:
/// *Configuration looks weird, two headers for the cluster*.
/// </summary>
public sealed class ClusterViewsTest : BunitContext
{
    private static readonly TestCluster Names = TestCluster.Read();
    private static readonly string Receiver = Names.WithRole("receiving");
    private static readonly string Processor = Names.WithRole("processing");
    private static readonly string Sender = Names.WithRole("sending");
    private static readonly string Nodes = $"{Names.Scope}/node";

    /// <summary>The fixture's three nodes as every view lists them: by name.</summary>
    private static readonly string[] Listed =
        [.. new[] { Receiver, Processor, Sender }.Order(StringComparer.Ordinal)];

    private static readonly string RunLine =
        $"RoundTrip · {Names.Name} · nodes {Receiver}=receiving "
        + $"{Processor}=processing+sending {Sender}=sending · online {Receiver} · realistic";

    public ClusterViewsTest()
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "Fixture", "cluster.toml");
        Services.AddSingleton(ClusterSurfaces.Over(new SnapshotOperator(fixture)));
        Services.AddSingleton(new RoleContext(Role.Observer, "tester"));
        Services.AddSingleton(new ProgramAudit("Xmip.Gui.Test", Unacted.Audit));
    }

    [Fact]
    public void EveryViewSaysWhatTheRunWasStartedWith()
    {
        const string Said = "p.run-line .run-said";

        Assert.Equal(RunLine, Render<Cluster>().Find(Said).TextContent.Trim());
        Assert.Equal(RunLine, Render<Configuration>().Find(Said).TextContent.Trim());
        Assert.Equal(RunLine, Render<Topology>().Find(Said).TextContent.Trim());
    }

    [Fact]
    public void TheConfigurationHasOneClusterRowAndItHeadsTheTree()
    {
        IRenderedComponent<Configuration> tree = Render<Configuration>();

        IElement cluster = Assert.Single(
            tree.FindAll(".tree-row"),
            row => row.QuerySelector(".kind")?.TextContent == "cluster");
        Assert.Equal(ScopeLink.Anchor(Names.Scope), cluster.Id);
        Assert.Empty(tree.FindAll(".tree > .tree-row"));

        // What the row above the tree carried is on the cluster's own row:
        // the way to the worst leaf, and to the topology.
        Assert.Equal(
            ScopeLink.Configuration($"{Nodes}/{Sender}/send/tcp/json"),
            cluster.QuerySelector("a.tree-link.problem")?.GetAttribute("href"));
        Assert.Contains(
            cluster.QuerySelectorAll("a.tree-link"),
            link => link.GetAttribute("href") == ScopeLink.TopologyAt("cluster"));
    }

    [Fact]
    public void AConfigurationRowSaysWhatThePublisherSaysItIs()
    {
        // The depth alone read a Playground cluster one level off: the
        // nodes' rollup branch said node, a node said scope, a stage said
        // receive location. The publisher's topology says what each is.
        IRenderedComponent<Configuration> tree = Render<Configuration>();

        Assert.Equal("scope", KindOf(tree, Nodes));
        Assert.Equal("node", KindOf(tree, $"{Nodes}/{Receiver}"));
        Assert.Equal("stage", KindOf(tree, $"{Nodes}/{Receiver}/receive"));
        Assert.Equal("endpoint", KindOf(tree, $"{Nodes}/{Receiver}/receive/tcp"));
        Assert.Equal("technology", KindOf(tree, $"{Nodes}/{Receiver}/receive/tcp/json"));
    }

    /// <summary>
    /// The Monitor counts the cluster's nodes as its publisher draws them:
    /// three here. It said "1 node(s)" until 2026-09-25, counting the scopes
    /// beneath the root, which over a cluster is the cluster itself.
    /// </summary>
    [Fact]
    public void TheMonitorCountsAndListsTheClustersNodes()
    {
        IRenderedComponent<Cluster> page = Render<Cluster>();

        Assert.StartsWith(
            "3 node(s)",
            page.Find("section.cluster .detail").TextContent,
            StringComparison.Ordinal);
        Assert.Equal(
            Listed,
            page.FindAll("section.nodes .row .scope").Select(scope => scope.TextContent));
    }

    /// <summary>
    /// What a node declares — its roles — reaches the operator where the operator
    /// looks at that node (ADR-0056; ADR-0014, amendment 2026-09-19): in the
    /// run line at the top of every view, on the node in the topology's
    /// inspector, and as a row of its own in the configuration tree, where the
    /// publisher's whole sentence is — including the two kinds this rig does
    /// not model.
    /// </summary>
    [Fact]
    public void ANodesDeclaredCapabilityIsVisibleWhereAnOperatorLooksAtThatNode()
    {
        Address(ScopeLink.TopologyAt($"node/{Processor}"));
        IElement declared = Render<Topology>().Find(".topology-inspector dd.capability");
        Assert.Equal("processing+sending", declared.TextContent.Split('·')[0].Trim());
        Assert.Contains("published by the node", declared.TextContent, StringComparison.Ordinal);
        Assert.Contains(
            "authentication and runtime capability are not modelled",
            declared.GetAttribute("title") ?? string.Empty,
            StringComparison.Ordinal);

        Address(ScopeLink.TopologyAt($"node/{Receiver}"));
        Assert.Contains(
            "receiving · online",
            Render<Topology>().Find(".topology-inspector dd.capability").TextContent,
            StringComparison.Ordinal);

        // A stage is not something that declares, so nothing is said of one.
        Address(ScopeLink.TopologyAt($"node/{Receiver}/receive"));
        Assert.Empty(Render<Topology>().FindAll(".topology-inspector dd.capability"));

        // The configuration tree carries the node's own words, whole, at a row
        // of its own beneath the node.
        IRenderedComponent<Configuration> tree = Render<Configuration>();
        IElement row = tree.Find($"#{ScopeLink.Anchor($"{Nodes}/{Sender}/capability")}");
        Assert.Equal("capability", row.QuerySelector(".kind")?.TextContent);
        Assert.Equal(
            "declares sending; offline; authentication and runtime capability are "
                + "not modelled",
            row.QuerySelector(".evidence")?.TextContent);
    }

    [Fact]
    public void EveryLevelDrillsToTheSameScopeInTheOtherTwoViews()
    {
        AssertDrill(Render<Topology>(), Names.Scope);

        Address(ScopeLink.TopologyAt($"node/{Sender}"));
        AssertDrill(Render<Topology>(), $"{Nodes}/{Sender}");

        Address(ScopeLink.TopologyAt($"node/{Sender}/send"));
        IRenderedComponent<Topology> send = Render<Topology>();
        AssertDrill(send, $"{Nodes}/{Sender}/send");

        IElement tcp = send.FindAll(".topology-beneath li").Single(item =>
            item.QuerySelector(".beneath-open")?.TextContent == "tcp");
        Assert.Contains(
            tcp.QuerySelectorAll("a"),
            link => link.GetAttribute("href")
                == ScopeLink.Monitor($"{Nodes}/{Sender}/send/tcp"));

        // The scopes the topology drills to are rows of the configuration.
        IRenderedComponent<Configuration> tree = Render<Configuration>();
        Assert.NotNull(tree.Find($"#{ScopeLink.Anchor($"{Nodes}/{Sender}/send/tcp")}"));
    }

    private void Address(string uri)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(uri);
    }

    private static string KindOf(IRenderedComponent<Configuration> tree, string scope)
    {
        return tree.Find($"#{ScopeLink.Anchor(scope)}").QuerySelector(".kind")?.TextContent
            ?? string.Empty;
    }

    private static void AssertDrill(IRenderedComponent<Topology> page, string scope)
    {
        List<string?> links =
            [.. page.FindAll(".inspect-links a").Select(link => link.GetAttribute("href"))];

        Assert.Equal([ScopeLink.Configuration(scope), ScopeLink.Monitor(scope)], links);
    }
}
