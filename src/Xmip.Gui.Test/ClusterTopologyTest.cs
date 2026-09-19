using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Gui.Pages;
using Xmip.Gui.Surface;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// The topology the owner asked for on 2026-09-19 — cluster, nodes, receive,
/// process, send — rendered over the surface library's cluster fixture: it
/// opens level by level down to a transport's endpoint, the handoffs R to P
/// to S are drawn between the nodes, every level drills to the same scope in
/// the other two views, and every view says what the run was started with.
/// </summary>
public sealed class ClusterTopologyTest : BunitContext
{
    private const string RunLine =
        "RoundTrip · C1 · nodes R1=receive P1=process+send S1=send · online R1 · realistic";

    public ClusterTopologyTest()
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "Fixture", "cluster.toml");
        Services.AddSingleton<IOperatorSurface>(new SnapshotOperator(fixture));
        Services.AddSingleton(new RoleContext(Role.Observer));
    }

    [Fact]
    public void EveryViewSaysWhatTheRunWasStartedWith()
    {
        Assert.Equal(RunLine, Render<Cluster>().Find("p.run-line").TextContent.Trim());
        Assert.Equal(RunLine, Render<Configuration>().Find("p.run-line").TextContent.Trim());
        Assert.Equal(RunLine, Render<Topology>().Find("p.run-line").TextContent.Trim());
    }

    [Fact]
    public void TheTopologyOpensFromTheClusterToItsNodesAndDrawsTheHandoffs()
    {
        IRenderedComponent<Topology> page = Render<Topology>();

        Assert.Equal(["C1"], Labels(page));
        Assert.Equal("cluster", page.Find("g.topology-node .node-kind").TextContent);
        Assert.Empty(page.FindAll("g.topology-link"));

        Open(page, "C1");

        Assert.Equal(["P1", "R1", "S1"], Labels(page));
        Assert.All(
            page.FindAll("g.topology-node .node-kind"),
            kind => Assert.Equal("node", kind.TextContent));

        // R1 to P1 and P1 to S1: the links run between stages, and are drawn
        // between the nodes that hold them while the stages are closed.
        IReadOnlyList<IElement> links = page.FindAll("g.topology-link");
        Assert.Equal(2, links.Count);
        Assert.All(links, link => Assert.Contains("sendreceive", link.ClassName ?? string.Empty,
            StringComparison.Ordinal));
        Assert.Contains(links, link => (link.ClassName ?? string.Empty).Contains(
            "stressed", StringComparison.Ordinal));
    }

    [Fact]
    public void ANodeOpensToItsStageAndTheStageToOneEndpointPerTransport()
    {
        IRenderedComponent<Topology> page = Render<Topology>();

        Open(page, "C1");
        Open(page, "R1");
        Assert.Contains("receive", Labels(page));
        Assert.DoesNotContain("process", Labels(page));

        Open(page, "receive");
        Assert.Contains("file", Labels(page));
        Assert.Contains("tcp", Labels(page));

        Select(page, "tcp");
        Assert.Equal("endpoint", page.Find(".topology-inspector dd").TextContent);
        Assert.Empty(page.FindAll("button.inspect-action"));
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
        IRenderedComponent<Topology> page = Render<Topology>();

        Open(page, "C1");
        Select(page, "P1");

        IElement declared = page.Find(".topology-inspector dd.capability");
        Assert.Equal("process+send", declared.TextContent.Split('·')[0].Trim());
        Assert.Contains("published by the node", declared.TextContent, StringComparison.Ordinal);
        Assert.Contains(
            "authentication and runtime capability are not modelled",
            declared.GetAttribute("title") ?? string.Empty,
            StringComparison.Ordinal);

        Select(page, "R1");
        Assert.Contains(
            "receive · online",
            page.Find(".topology-inspector dd.capability").TextContent,
            StringComparison.Ordinal);

        // A stage is not something that declares, so nothing is said of one.
        Open(page, "R1");
        Select(page, "receive");
        Assert.Empty(page.FindAll(".topology-inspector dd.capability"));

        // The configuration tree carries the node's own words, whole, at a row
        // of its own beneath the node.
        IRenderedComponent<Configuration> tree = Render<Configuration>();
        IElement row = tree.Find($"#{ScopeLink.Anchor("xmip:///C1/node/S1/capability")}");
        Assert.Equal("capability", row.QuerySelector(".kind")?.TextContent);
        Assert.Equal(
            "declares send; offline; authentication and runtime capability are "
                + "not modelled in this rig",
            row.QuerySelector(".evidence")?.TextContent);
    }

    [Fact]
    public void EveryLevelDrillsToTheSameScopeInTheOtherTwoViews()
    {
        IRenderedComponent<Topology> page = Render<Topology>();

        Select(page, "C1");
        AssertDrill(page, "xmip:///C1");

        Open(page, "C1");
        Select(page, "S1");
        AssertDrill(page, "xmip:///C1/node/S1");

        Open(page, "S1");
        Select(page, "send");
        AssertDrill(page, "xmip:///C1/node/S1/send");

        Open(page, "send");
        Select(page, "tcp");
        AssertDrill(page, "xmip:///C1/node/S1/send/tcp");

        // The scopes the topology drills to are rows of the configuration.
        IRenderedComponent<Configuration> tree = Render<Configuration>();
        Assert.NotNull(tree.Find($"#{ScopeLink.Anchor("xmip:///C1/node/S1/send/tcp")}"));
    }

    private static List<string> Labels(IRenderedComponent<Topology> page)
    {
        return [.. page.FindAll("g.topology-node .node-label").Select(label => label.TextContent)];
    }

    private static void Select(IRenderedComponent<Topology> page, string label)
    {
        IElement node = page.FindAll("g.topology-node").Single(candidate =>
            candidate.QuerySelector(".node-label")?.TextContent == label);
        node.Click();
    }

    private static void Open(IRenderedComponent<Topology> page, string label)
    {
        Select(page, label);
        page.Find("button.inspect-action").Click();
    }

    private static void AssertDrill(IRenderedComponent<Topology> page, string scope)
    {
        List<string?> links =
            [.. page.FindAll(".inspect-links a").Select(link => link.GetAttribute("href"))];

        Assert.Equal([ScopeLink.Configuration(scope), ScopeLink.Monitor(scope)], links);
    }
}
