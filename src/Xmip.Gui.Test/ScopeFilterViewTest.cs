using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Gui.Pages;
using Xmip.Gui.Surface;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// The filter the three views gained on 2026-09-19, over the surface library's
/// cluster fixture: C1 with alpha, beta and gamma. The pattern is the estate's, matched
/// by <see cref="ScopePattern"/> — the same one the prompt and the executable
/// match with (ADR-0052 clause 1; ADR-0059 clauses 7 and 8) — and what it
/// narrows, what it never narrows, and what it says when it names nothing are
/// asserted here. A page fetched over HTTP returns before anyone has typed, so
/// only a rendered component can prove a filter that is applied.
/// </summary>
public sealed class ScopeFilterViewTest : BunitContext
{
    public ScopeFilterViewTest()
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "Fixture", "cluster.toml");
        Services.AddSingleton(ClusterSurfaces.Over(new SnapshotOperator(fixture)));
        Services.AddSingleton(new RoleContext(Role.Observer));
    }

    [Fact]
    public void EveryViewCarriesTheSameFilterBoxAndAnEmptyBoxSaysNothing()
    {
        Assert.NotNull(Render<Cluster>().Find("#filter-monitor"));
        Assert.NotNull(Render<Configuration>().Find("#filter-configuration"));
        Assert.NotNull(Render<Topology>().Find("#filter-topology"));

        // Empty is everything, and a view that is not narrowing says nothing
        // about a filter at all.
        Assert.Empty(Render<Cluster>().FindAll(".scope-filter-said"));
    }

    /// <summary>
    /// The monitor narrows its rows and leaves the cluster alone: the banner
    /// and the stage tiles say what the whole cluster is whatever is typed, so
    /// no pattern can make a troubled estate look fine.
    /// </summary>
    [Fact]
    public void TheMonitorNarrowsItsRowsAndNeverItsBanner()
    {
        IRenderedComponent<Cluster> page = Render<Cluster>();
        string banner = page.Find("section.cluster .state").TextContent;
        int before = page.FindAll("section.list .row").Count;

        page.Find("#filter-monitor").Change("*/gamma*");

        IReadOnlyList<IElement> rows = page.FindAll("section.list .row");
        Assert.NotEmpty(rows);
        Assert.True(rows.Count < before, "the filter narrowed nothing");
        Assert.All(
            rows, row => Assert.Contains("gamma", row.TextContent, StringComparison.Ordinal));
        Assert.Equal(banner, page.Find("section.cluster .state").TextContent);
        Assert.Contains("of ", page.Find(".scope-filter-said").TextContent,
            StringComparison.Ordinal);

        // Taking the filter off puts everything back.
        page.Find("#filter-monitor").Change(string.Empty);
        Assert.Equal(before, page.FindAll("section.list .row").Count);
    }

    /// <summary>
    /// The tree keeps the path down to what matched, however deep — a row that
    /// cannot be reached is a row that is not there — and drops the branches
    /// that lead nowhere near it.
    /// </summary>
    [Fact]
    public void TheConfigurationTreeKeepsThePathDownToWhatMatched()
    {
        IRenderedComponent<Configuration> page = Render<Configuration>();

        Assert.NotEmpty(page.FindAll($"#{ScopeLink.Anchor("xmip:///C1/node/alpha")}"));

        page.Find("#filter-configuration").Change("*/gamma/send/tcp");

        Assert.NotEmpty(page.FindAll($"#{ScopeLink.Anchor("xmip:///C1/node")}"));
        Assert.NotEmpty(page.FindAll($"#{ScopeLink.Anchor("xmip:///C1/node/gamma")}"));
        Assert.NotEmpty(page.FindAll($"#{ScopeLink.Anchor("xmip:///C1/node/gamma/send/tcp")}"));

        // What it matched shows what is beneath it, and nothing else stays.
        Assert.NotEmpty(
            page.FindAll($"#{ScopeLink.Anchor("xmip:///C1/node/gamma/send/tcp/json")}"));
        Assert.Empty(page.FindAll($"#{ScopeLink.Anchor("xmip:///C1/node/alpha")}"));
        Assert.Empty(page.FindAll($"#{ScopeLink.Anchor("xmip:///C1/node/gamma/send/file")}"));
    }

    /// <summary>
    /// The topology filters its nodes, and a link is drawn only where both of
    /// its ends are still shown: the handoffs alpha to beta to gamma go when beta does.
    /// </summary>
    [Fact]
    public void TheTopologyNarrowsItsNodesAndDropsALinkWithAHiddenEnd()
    {
        IRenderedComponent<Topology> page = Render<Topology>();

        // The cluster stands open by itself, filtered or not (2026-09-25).
        page.Find("#filter-topology").Change("*/alpha");

        Assert.Equal(["alpha"], Labels(page));
        Assert.Empty(page.FindAll("g.topology-link"));
    }

    /// <summary>
    /// A pattern that names nothing is said in words, on every view. An empty
    /// page reads as a healthy estate, and that is exactly what must not
    /// happen (ADR-0059 clause 7: refused, naming the pattern).
    /// </summary>
    [Fact]
    public void APatternThatMatchesNothingIsRefusedOnEveryView()
    {
        List<IElement> said = Filtered();

        Assert.Equal(3, said.Count);
        Assert.All(said, one =>
        {
            Assert.StartsWith("REFUSED", one.TextContent, StringComparison.Ordinal);
            Assert.Contains("xmip:///C1/node/Q*", one.TextContent, StringComparison.Ordinal);
            Assert.Contains("scope(s) published", one.TextContent, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// And the topology says it where the picture was, so a canvas that has
    /// gone empty is never read as a cluster that publishes no topology.
    /// </summary>
    [Fact]
    public void TheTopologySaysWhyItsCanvasIsEmptyRatherThanLookingUnpublished()
    {
        IRenderedComponent<Topology> page = Render<Topology>();

        page.Find("#filter-topology").Change("xmip:///C1/node/Q*");

        IElement empty = page.Find("section.topology-empty h1");
        Assert.Equal("Nothing matches xmip:///C1/node/Q*", empty.TextContent);
        Assert.Empty(page.FindAll("g.topology-node"));
    }

    /// <summary>The three views, each asked for a pattern that names nothing,
    /// and what each of them says about it.</summary>
    private List<IElement> Filtered()
    {
        const string nowhere = "xmip:///C1/node/Q*";
        IRenderedComponent<Cluster> monitor = Render<Cluster>();
        IRenderedComponent<Configuration> tree = Render<Configuration>();
        IRenderedComponent<Topology> topology = Render<Topology>();

        monitor.Find("#filter-monitor").Change(nowhere);
        tree.Find("#filter-configuration").Change(nowhere);
        topology.Find("#filter-topology").Change(nowhere);

        return
        [
            monitor.Find(".scope-filter-said.refused"),
            tree.Find(".scope-filter-said.refused"),
            topology.Find(".scope-filter-said.refused"),
        ];
    }

    private static List<string> Labels(IRenderedComponent<Topology> page)
    {
        return [.. page.FindAll("g.topology-node .node-label").Select(label => label.TextContent)];
    }
}
