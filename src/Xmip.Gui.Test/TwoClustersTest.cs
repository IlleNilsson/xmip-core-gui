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
/// fixtures: C1 with R1, P1 and S1, and C2 with R2 and a done S2.
/// </summary>
public sealed class TwoClustersTest : BunitContext
{
    public TwoClustersTest()
    {
        Services.AddSingleton(ClusterSurfaces.Over(
            [Snapshot("cluster.toml"), Snapshot("cluster-c2.toml")]));
        Services.AddSingleton(new RoleContext(Role.Observer));
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
            Assert.Equal(["C1", "C2"], Picked(page));
            Assert.Equal("C1", Current(page));
            Assert.Single(page.FindAll("p.run-line"));
            Assert.Single(page.FindAll("p.run-line span.run-clusters"));
        }
    }

    [Fact]
    public void AViewToldAClusterReadsThatClustersPublicationAndNoOther()
    {
        Address("/configuration?cluster=C2");
        IRenderedComponent<Configuration> page = Render<Configuration>();

        Assert.Equal("C2", Current(page));
        Assert.Contains(
            "RoundTrip · C2 · nodes R2=receive S2=send · online R2 · harsh",
            page.Find("p.run-line").TextContent,
            StringComparison.Ordinal);

        // C2's tree, and nothing of C1's: two clusters are two scope trees.
        Assert.NotEmpty(page.FindAll($"#{ScopeLink.Anchor("xmip:///C2/node/R2")}"));
        Assert.Empty(page.FindAll($"#{ScopeLink.Anchor("xmip:///C1/node/R1")}"));
    }

    [Fact]
    public void EachViewsChooserKeepsTheViewAndStartsTheDrillOver()
    {
        // A scope of the cluster being left names nothing in the one being
        // entered, so the address carries the cluster and nothing else.
        Address("/topology?cluster=C1");
        IRenderedComponent<Topology> topology = Render<Topology>();

        Assert.Equal(
            "/topology?cluster=C2",
            topology.FindAll("a.cluster-pick")[1].GetAttribute("href"));

        Address("/?scope=xmip%3A%2F%2F%2FC1&cluster=C1");
        IRenderedComponent<Cluster> monitor = Render<Cluster>();

        Assert.Equal("/?cluster=C2", monitor.FindAll("a.cluster-pick")[1].GetAttribute("href"));
    }

    [Fact]
    public void ALinkOutOfAViewCarriesTheClusterItWasWrittenOn()
    {
        // The three views lead to one another (ADR-0052, amendment 2026-09-18);
        // a link that dropped the cluster would land on the other cluster's
        // tree at a scope that is not in it.
        Address("/configuration?cluster=C2");
        IElement row = Render<Configuration>()
            .Find($"#{ScopeLink.Anchor("xmip:///C2/node/S2/send/tcp/json")}");

        Assert.Equal(
            ScopeLink.Monitor("xmip:///C2/node/S2/send/tcp/json", "C2"),
            row.QuerySelector("a.tree-link:not(.problem)")?.GetAttribute("href"));
        Assert.Contains(
            "cluster=C2", ScopeLink.Monitor("xmip:///C2", "C2"), StringComparison.Ordinal);
        Assert.Equal(
            "configuration?cluster=C2#s-xmip----C2",
            ScopeLink.Configuration("xmip:///C2", "C2"));
        Assert.Equal("/topology?cluster=C2", ScopeLink.Topology("C2"));
    }

    [Fact]
    public void AClusterThisHostDoesNotHoldFallsBackToTheFirstRatherThanFailing()
    {
        // A roll that ended leaves a link behind. The answer is the other
        // cluster, never an error page.
        Address("/configuration?cluster=Z9");
        IRenderedComponent<Configuration> page = Render<Configuration>();

        Assert.Equal("C1", Current(page));
        Assert.NotEmpty(page.FindAll($"#{ScopeLink.Anchor("xmip:///C1/node/R1")}"));
    }

    [Fact]
    public void AHostWithOneClusterDrawsNoChooserAndWritesTheAddressesItAlwaysWrote()
    {
        using BunitContext alone = new();
        alone.Services.AddSingleton(ClusterSurfaces.Over(Snapshot("cluster.toml")));
        alone.Services.AddSingleton(new RoleContext(Role.Observer));

        IRenderedComponent<Configuration> page = alone.Render<Configuration>();

        Assert.Empty(page.FindAll("a.cluster-pick"));
        Assert.Equal(
            ScopeLink.Monitor("xmip:///C1/node/R1"),
            page.Find($"#{ScopeLink.Anchor("xmip:///C1/node/R1")}")
                .QuerySelector("a.tree-link:not(.problem)")?.GetAttribute("href"));
        Assert.Equal("configuration#s-xmip----C1", ScopeLink.Configuration("xmip:///C1"));
        Assert.Equal("/topology", ScopeLink.Topology());
    }
}
