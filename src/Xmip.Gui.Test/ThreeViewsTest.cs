using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Gui.Pages;
using Xmip.Gui.Surface;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// The web GUI is three points to drill from — Configuration, Monitor and
/// Topology — and from any of them a scope leads to the same scope in the
/// others (ADR-0052, amendments 2026-09-14 and 2026-09-18). Rendered over the
/// surface library's own fixture: edge-01 holds a done Receive Location and a
/// stressed Xmip Process, edge-02 a paused Send Location and a fine one.
/// </summary>
public sealed class ThreeViewsTest : BunitContext
{
    private const string Done = "xmip:///edge-01/receive/partner";

    public ThreeViewsTest()
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "Fixture", "snapshot.toml");
        Services.AddSingleton(ClusterSurfaces.Over(new SnapshotOperator(fixture)));
        Services.AddSingleton(new RoleContext(Role.Observer));
    }

    [Fact]
    public void ATroubledBranchNamesItsWorstLeafAndLinksToItsRow()
    {
        IRenderedComponent<Configuration> page = Render<Configuration>();

        // The owner, 2026-09-18: the severity was there and the way to the
        // problem area was not.
        AngleSharp.Dom.IElement node = page.Find($"#{ScopeLink.Anchor("xmip:///edge-01")}");
        AngleSharp.Dom.IElement problem = node.QuerySelector("a.tree-link.problem")
            ?? throw new InvalidOperationException("no problem link on a troubled node");

        Assert.Equal(ScopeLink.Configuration(Done), problem.GetAttribute("href"));
        Assert.Contains("receive/partner", node.TextContent, StringComparison.Ordinal);
        Assert.Contains("connection refused", node.TextContent, StringComparison.Ordinal);
        Assert.NotNull(page.Find($"#{ScopeLink.Anchor(Done)}"));
    }

    [Fact]
    public void AFineLeafCarriesNoProblemLinkAndEveryRowIsALandingPlace()
    {
        IRenderedComponent<Configuration> page = Render<Configuration>();

        AngleSharp.Dom.IElement fine =
            page.Find($"#{ScopeLink.Anchor("xmip:///edge-02/send/billing")}");
        Assert.Null(fine.QuerySelector("a.tree-link.problem"));
        Assert.Equal(
            ScopeLink.Monitor("xmip:///edge-02/send/billing"),
            fine.QuerySelector("a.tree-link")?.GetAttribute("href"));

        // Branch or leaf, a row is somewhere a link from another view can land.
        Assert.All(
            page.FindAll(".tree-level .tree-row"),
            row => Assert.StartsWith("s-", row.Id ?? string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public void NoRowStandsAboveTheTree()
    {
        // The owner, 2026-09-25: two headers for the cluster. The tree's own
        // rows are all there is; the first level heads it.
        IRenderedComponent<Configuration> page = Render<Configuration>();

        Assert.Empty(page.FindAll(".tree > .tree-row"));
        Assert.All(
            page.FindAll(".tree-row"),
            row => Assert.StartsWith("s-", row.Id ?? string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public void TheTopologyIsAlwaysConfiguredAndObservedWithNoSwitch()
    {
        IRenderedComponent<Topology> page = Render<Topology>();

        // The owner, 2026-09-18: no need for Configured + observed; the
        // topology shall always be both.
        Assert.Empty(page.FindAll(".topology-modes"));
        Assert.DoesNotContain(
            page.FindAll("button"),
            button => button.TextContent.Trim() is "Configured" or "Observed");
    }

    [Fact]
    public void ASurfaceThatSaysNothingOfItsRunShowsNoRunLine()
    {
        // The line is the run's (the owner, 2026-09-19); a snapshot with no
        // [run] table reads as it always did, and no view invents one.
        Assert.Empty(Render<Cluster>().FindAll("p.run-line .run-said"));
        Assert.Empty(Render<Configuration>().FindAll("p.run-line .run-said"));
        Assert.Empty(Render<Topology>().FindAll("p.run-line .run-said"));

        // The line itself stays: where the records come from is always said
        // (ADR-0052 clause 3), and since 2026-09-26 it is said there.
        Assert.Contains(
            "SNAPSHOT",
            Render<Cluster>().Find("p.run-line .source").TextContent,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheMonitorsDrillLeadsToTheConfigurationAtTheSameScope()
    {
        // At the root the crumb leads to the tree from its top: the root is no
        // row, and until 2026-09-25 the link named #s-xmip----, which nothing
        // carries.
        AngleSharp.Dom.IElement root = Render<Cluster>().Find("nav.crumbs a.crumb-link");
        Assert.Equal("configuration", root.GetAttribute("href"));

        // Beneath the root it lands on the row itself, and the row is there.
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo(ScopeLink.Monitor("xmip:///edge-01/receive"));
        string? deeper = Render<Cluster>().Find("nav.crumbs a.crumb-link").GetAttribute("href");
        Assert.Equal(ScopeLink.Configuration("xmip:///edge-01/receive"), deeper);
        Assert.NotNull(
            Render<Configuration>().Find($"#{ScopeLink.Anchor("xmip:///edge-01/receive")}"));
    }

    [Fact]
    public void AScopeIsWrittenTheSameWayByEveryView()
    {
        Assert.Equal("s-xmip----C1-node-alpha", ScopeLink.Anchor("xmip:///C1/node/alpha"));
        Assert.Equal(
            "configuration#s-xmip----C1-node-alpha",
            ScopeLink.Configuration("xmip:///C1/node/alpha"));
        Assert.Equal("/?scope=xmip%3A%2F%2F%2FC1", ScopeLink.Monitor("xmip:///C1"));
    }
}
