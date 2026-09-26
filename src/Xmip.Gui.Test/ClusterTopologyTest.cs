using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Gui.Pages;
using Xmip.Gui.Surface;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// The topology the owner asked for on 2026-09-19 — cluster, nodes, receive,
/// process, send — rendered over the surface library's cluster fixture. It
/// opens on the cluster's nodes and the handoffs between them; every node is
/// a link, and the drill is in the address, cluster to node to stage to
/// endpoint; every link says what passes over it, a path configured and never
/// used as plainly as a used one; every level drills to the same scope in the
/// other two views; and the Configuration tree has one cluster row, heading
/// it. The owner, 2026-09-25: *Configuration looks weird, two headers for the
/// cluster*; *Topology does not work, one can't drill down to nodes*; *even
/// when testing, the topology does not show configured traffic or its usage*.
/// </summary>
public sealed class ClusterTopologyTest : BunitContext
{
    private const string RunLine =
        "RoundTrip · C1 · nodes alpha=receive beta=process+send gamma=send · "
        + "online alpha · realistic";

    public ClusterTopologyTest()
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "Fixture", "cluster.toml");
        Services.AddSingleton(ClusterSurfaces.Over(new SnapshotOperator(fixture)));
        Services.AddSingleton(new RoleContext(Role.Observer));
    }

    [Fact]
    public void EveryViewSaysWhatTheRunWasStartedWith()
    {
        Assert.Equal(RunLine, Render<Cluster>().Find("p.run-line .run-said").TextContent.Trim());
        Assert.Equal(RunLine, Render<Configuration>().Find("p.run-line .run-said").TextContent.Trim());
        Assert.Equal(RunLine, Render<Topology>().Find("p.run-line .run-said").TextContent.Trim());
    }

    [Fact]
    public void TheConfigurationHasOneClusterRowAndItHeadsTheTree()
    {
        IRenderedComponent<Configuration> tree = Render<Configuration>();

        IElement cluster = Assert.Single(
            tree.FindAll(".tree-row"),
            row => row.QuerySelector(".kind")?.TextContent == "cluster");
        Assert.Equal(ScopeLink.Anchor("xmip:///C1"), cluster.Id);
        Assert.Empty(tree.FindAll(".tree > .tree-row"));

        // What the row above the tree carried is on the cluster's own row:
        // the way to the worst leaf, and to the topology.
        Assert.Equal(
            ScopeLink.Configuration("xmip:///C1/node/gamma/send/tcp/json"),
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

        Assert.Equal("scope", KindOf(tree, "xmip:///C1/node"));
        Assert.Equal("node", KindOf(tree, "xmip:///C1/node/alpha"));
        Assert.Equal("stage", KindOf(tree, "xmip:///C1/node/alpha/receive"));
        Assert.Equal("endpoint", KindOf(tree, "xmip:///C1/node/alpha/receive/tcp"));
        Assert.Equal("technology", KindOf(tree, "xmip:///C1/node/alpha/receive/tcp/json"));
    }

    [Fact]
    public void TheTopologyOpensOnTheClustersNodesAndTheHandoffsBetweenThem()
    {
        IRenderedComponent<Topology> page = Render<Topology>();

        Assert.Equal(["alpha", "beta", "gamma"], Labels(page));
        Assert.All(
            page.FindAll("g.topology-node .node-kind"),
            kind => Assert.Equal("node", kind.TextContent));

        // alpha to beta and beta to gamma: the links run between stages, and are drawn
        // between the nodes that hold them while the stages are closed.
        IReadOnlyList<IElement> links = page.FindAll("g.topology-link");
        Assert.Equal(2, links.Count);
        Assert.All(links, link => Assert.Contains("send-receive", link.ClassName ?? string.Empty,
            StringComparison.Ordinal));
        Assert.Contains(links, link => (link.ClassName ?? string.Empty).Contains(
            "stressed", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryNodeOnTheCanvasLinksToItsDrill()
    {
        IRenderedComponent<Topology> page = Render<Topology>();

        Assert.Equal(
            [
                ScopeLink.TopologyAt("node/alpha"),
                ScopeLink.TopologyAt("node/beta"),
                ScopeLink.TopologyAt("node/gamma"),
            ],
            Hrefs(page));

        // And beside the canvas, each with the same scope in the other views.
        IElement alpha = page.FindAll(".topology-beneath li").Single(item =>
            item.QuerySelector(".beneath-open")?.TextContent == "alpha");
        Assert.Equal(
            [
                ScopeLink.TopologyAt("node/alpha"),
                ScopeLink.Configuration("xmip:///C1/node/alpha"),
                ScopeLink.Monitor("xmip:///C1/node/alpha"),
            ],
            alpha.QuerySelectorAll("a").Select(link => link.GetAttribute("href")));
    }

    [Fact]
    public void TheDrillGoesFromClusterToNodeToStageToEndpointByAddress()
    {
        Address(ScopeLink.TopologyAt("node/alpha"));
        IRenderedComponent<Topology> node = Render<Topology>();
        Assert.Contains("receive", Labels(node));
        Assert.DoesNotContain("process", Labels(node));
        Assert.Equal(["C1", "alpha"], Trail(node));

        Address(ScopeLink.TopologyAt("node/alpha/receive"));
        IRenderedComponent<Topology> stage = Render<Topology>();
        Assert.Contains("file", Labels(stage));
        Assert.Contains("tcp", Labels(stage));
        Assert.Equal(["C1", "alpha", "receive"], Trail(stage));
        Assert.Equal(
            ScopeLink.TopologyAt("node/alpha"),
            stage.FindAll(".topology-trail a")[1].GetAttribute("href"));

        // An endpoint has nothing beneath it: it ends at its configuration.
        Assert.Contains(
            ScopeLink.Configuration("xmip:///C1/node/alpha/receive/tcp"), Hrefs(stage));
    }

    [Fact]
    public void ConfiguredAndObservedTrafficAreBothDrawnAndSaidOnTheLine()
    {
        IRenderedComponent<Topology> cluster = Render<Topology>();

        // What passes over a used link is on the line: its volume and rate.
        Assert.All(
            cluster.FindAll("g.topology-link"),
            link => Assert.Equal("6 · 1.5/s", link.QuerySelector(".link-figure")?.TextContent));

        // Within the cluster, every path and what it carried — beta's own
        // process to its send stage is configured and has carried nothing.
        List<(string Ends, string Figure, string Origin)> traffic =
        [
            .. cluster.FindAll(".topology-traffic li").Select(item => (
                item.QuerySelector(".ends")?.TextContent ?? string.Empty,
                item.QuerySelector(".figure")?.TextContent ?? string.Empty,
                item.QuerySelector(".origin")?.TextContent ?? string.Empty)),
        ];
        Assert.Equal(
            [
                ("alpha/receive → beta/process", "6 · 1.5/s", "configured and observed"),
                ("beta/process → beta/send", "configured · no traffic observed", "configured"),
                ("beta/process → gamma/send", "6 · 1.5/s", "configured and observed"),
            ],
            traffic);

        // Opened, beta draws the configured link between its two stages,
        // dashed by its class and saying so on itself, beside the two used
        // ones that reach its process stage from the rest of the cluster.
        Address(ScopeLink.TopologyAt("node/beta"));
        IReadOnlyList<IElement> links = Render<Topology>().FindAll("g.topology-link");
        Assert.Equal(3, links.Count);
        IElement idle = Assert.Single(links, link =>
            (link.ClassName ?? string.Empty).Split(' ').Contains("configured"));
        Assert.Equal(
            "configured · no traffic observed",
            idle.QuerySelector(".link-figure")?.TextContent);
    }

    /// <summary>
    /// Open at a node, the canvas is that node and what is beneath it — its
    /// frame, its stages — and nothing beside it. Traffic that leaves it runs
    /// to one marker that says it is outside, and the inspector says the same
    /// ends. Until 2026-09-25 the cluster's box stood beside beta's stages and
    /// the other nodes' traffic was drawn into it.
    /// </summary>
    [Fact]
    public void AnOpenNodeShowsItselfAndItsChildrenAndTrafficLeavingItGoesOutside()
    {
        Address(ScopeLink.TopologyAt("node/beta"));
        IRenderedComponent<Topology> beta = Render<Topology>();

        Assert.Equal(["process", "send"], Labels(beta));
        Assert.Contains("beta", beta.Find("g.topology-focus .focus-label").TextContent,
            StringComparison.Ordinal);

        IElement outside = beta.Find("g.topology-outside");
        Assert.Equal("outside", outside.QuerySelector(".node-label")?.TextContent);
        Assert.Equal("beyond beta", outside.QuerySelector(".node-kind")?.TextContent);

        Assert.Equal(
            ["outside → process", "process → send", "process → outside"],
            beta.FindAll(".topology-traffic li .ends").Select(ends => ends.TextContent));

        // The cluster open at its root sends nothing outside it, and says so by
        // drawing no marker.
        Address(ScopeLink.Topology());
        Assert.Empty(Render<Topology>().FindAll("g.topology-outside"));
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
            "3 node(s)", page.Find("section.cluster .detail").TextContent, StringComparison.Ordinal);
        Assert.Equal(
            ["alpha", "beta", "gamma"],
            page.FindAll("section.nodes .row .scope").Select(scope => scope.TextContent));
    }

    /// <summary>
    /// What a node declares it can do reaches the operator where the operator
    /// looks at that node (ADR-0056; ADR-0014, amendment 2026-09-19): in the
    /// run line at the top of every view, on the node in the topology's
    /// inspector, and as a row of its own in the configuration tree, where the
    /// publisher's whole sentence is — including the two kinds this rig does
    /// not model.
    /// </summary>
    [Fact]
    public void ANodesDeclaredCapabilityIsVisibleWhereAnOperatorLooksAtThatNode()
    {
        Address(ScopeLink.TopologyAt("node/beta"));
        IElement declared = Render<Topology>().Find(".topology-inspector dd.capability");
        Assert.Equal("process+send", declared.TextContent.Split('·')[0].Trim());
        Assert.Contains("published by the node", declared.TextContent, StringComparison.Ordinal);
        Assert.Contains(
            "authentication and runtime capability are not modelled",
            declared.GetAttribute("title") ?? string.Empty,
            StringComparison.Ordinal);

        Address(ScopeLink.TopologyAt("node/alpha"));
        Assert.Contains(
            "receive · online",
            Render<Topology>().Find(".topology-inspector dd.capability").TextContent,
            StringComparison.Ordinal);

        // A stage is not something that declares, so nothing is said of one.
        Address(ScopeLink.TopologyAt("node/alpha/receive"));
        Assert.Empty(Render<Topology>().FindAll(".topology-inspector dd.capability"));

        // The configuration tree carries the node's own words, whole, at a row
        // of its own beneath the node.
        IRenderedComponent<Configuration> tree = Render<Configuration>();
        IElement row = tree.Find($"#{ScopeLink.Anchor("xmip:///C1/node/gamma/capability")}");
        Assert.Equal("capability", row.QuerySelector(".kind")?.TextContent);
        Assert.Equal(
            "declares send; offline; authentication and runtime capability are "
                + "not modelled",
            row.QuerySelector(".evidence")?.TextContent);
    }

    [Fact]
    public void EveryLevelDrillsToTheSameScopeInTheOtherTwoViews()
    {
        AssertDrill(Render<Topology>(), "xmip:///C1");

        Address(ScopeLink.TopologyAt("node/gamma"));
        AssertDrill(Render<Topology>(), "xmip:///C1/node/gamma");

        Address(ScopeLink.TopologyAt("node/gamma/send"));
        IRenderedComponent<Topology> send = Render<Topology>();
        AssertDrill(send, "xmip:///C1/node/gamma/send");

        IElement tcp = send.FindAll(".topology-beneath li").Single(item =>
            item.QuerySelector(".beneath-open")?.TextContent == "tcp");
        Assert.Contains(
            tcp.QuerySelectorAll("a"),
            link => link.GetAttribute("href")
                == ScopeLink.Monitor("xmip:///C1/node/gamma/send/tcp"));

        // The scopes the topology drills to are rows of the configuration.
        IRenderedComponent<Configuration> tree = Render<Configuration>();
        Assert.NotNull(tree.Find($"#{ScopeLink.Anchor("xmip:///C1/node/gamma/send/tcp")}"));
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

    private static List<string> Labels(IRenderedComponent<Topology> page)
    {
        return [.. page.FindAll("g.topology-node .node-label").Select(label => label.TextContent)];
    }

    private static List<string?> Hrefs(IRenderedComponent<Topology> page)
    {
        return [.. page.FindAll("a.topology-node-link").Select(link => link.GetAttribute("href"))];
    }

    private static List<string> Trail(IRenderedComponent<Topology> page)
    {
        return [.. page.FindAll(".topology-trail a").Select(link => link.TextContent)];
    }

    private static void AssertDrill(IRenderedComponent<Topology> page, string scope)
    {
        List<string?> links =
            [.. page.FindAll(".inspect-links a").Select(link => link.GetAttribute("href"))];

        Assert.Equal([ScopeLink.Configuration(scope), ScopeLink.Monitor(scope)], links);
    }
}
