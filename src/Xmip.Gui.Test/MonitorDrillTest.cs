using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Gui.Pages;
using Xmip.Gui.Surface;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// From whichever view the operator starts, a scope that is not fine leads,
/// one link at a time, to the leaf that explains it and that leaf's record;
/// and every number on the Monitor is the one its source published for what
/// the card or row says it counts. The owner, 2026-09-26: *drill-down does
/// not work and datapoints are wrong*, *the operation web is not drillable
/// and has too many headers*, and *it is all about solving the problem*.
/// Rendered over the surface library's cluster fixture, whose worst leaf is
/// the sending node's tcp/json Send Location.
/// </summary>
public sealed class MonitorDrillTest : BunitContext
{
    private static readonly TestCluster Names = TestCluster.Read();
    private static readonly string Sender = Names.WithRole("sending");
    private static readonly string Leaf = $"{Names.Scope}/node/{Sender}/send/tcp/json";

    public MonitorDrillTest()
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "Fixture", "cluster.toml");
        Services.AddSingleton(ClusterSurfaces.Over(new SnapshotOperator(fixture)));
        Services.AddSingleton(new RoleContext(Role.Observer, "tester"));
        Services.AddSingleton(new ProgramAudit("Xmip.Gui.Test", Unacted.Audit));
    }

    [Fact]
    public void AStageCardCountsItsOwnStageAndItsLocations()
    {
        // Until 2026-09-26 a card summed the cluster and called every leaf
        // beneath its stage, each contract and identity step, configured.
        IRenderedComponent<Cluster> page = Render<Cluster>();
        List<(string Value, string Items)> cards =
        [
            .. page.FindAll("section.stages .stage").Select(card => (
                card.QuerySelector(".value")?.TextContent ?? string.Empty,
                card.QuerySelector(".items")?.TextContent ?? string.Empty)),
        ];

        Assert.Equal("6", cards[0].Value);
        Assert.StartsWith("receive location ×2 ", cards[0].Items, StringComparison.Ordinal);
        Assert.Equal("6", cards[1].Value);
        Assert.StartsWith("work process ×1 ", cards[1].Items, StringComparison.Ordinal);
        Assert.Equal("5", cards[2].Value);
        Assert.StartsWith("send location ×2 ", cards[2].Items, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDrillStartsAtTheClusterAndEachRowIsTheNextAddress()
    {
        IRenderedComponent<Cluster> page = Render<Cluster>();

        // One cluster, named once: no "cluster" crumb above it, and the rows
        // are what is beneath it rather than the cluster alone.
        Assert.Equal([Names.Name], Crumbs(page));
        Assert.Equal(
            [ScopeLink.Monitor($"{Names.Scope}/node")],
            page.FindAll("section.drill a.branch").Select(row => row.GetAttribute("href")));
    }

    [Fact]
    public void TheWorstRowAtEveryLevelLeadsToTheLeafAndTheLeafShowsItsRecord()
    {
        string at = Names.Scope;

        for (int step = 0; step < 8; step++)
        {
            Address(ScopeLink.Monitor(at));
            IRenderedComponent<Cluster> page = Render<Cluster>();
            string? next = page.FindAll("section.drill a.branch")
                .Select(row => row.GetAttribute("href"))
                .FirstOrDefault();

            if (next is null)
            {
                break;
            }

            at = Uri.UnescapeDataString(next["/?scope=".Length..]);
        }

        Assert.Equal(Leaf, at);
        Address(ScopeLink.Monitor(Leaf));
        IRenderedComponent<Cluster> bottom = Render<Cluster>();
        AngleSharp.Dom.IElement record = bottom.Find("section.drill .drill-leaf");
        Assert.Contains("2/3 rounds passed, 1 failed", record.TextContent, StringComparison.Ordinal);
        Assert.Contains("severity 40", record.TextContent, StringComparison.Ordinal);
        Assert.Equal([Names.Name, "node", Sender, "send", "tcp", "json"], Crumbs(bottom));
    }

    [Fact]
    public void TheClustersWhyIsOneClickFromTheLeaf()
    {
        AngleSharp.Dom.IElement why = Render<Cluster>().Find("section.cluster .why a.leaf");

        Assert.Equal(ScopeLink.Monitor(Leaf), why.GetAttribute("href"));
        Assert.Equal($"node/{Sender}/send/tcp/json", why.TextContent);
    }

    [Fact]
    public void ANeedsAttentionRowAndANodeRowAreLinksIntoTheDrill()
    {
        IRenderedComponent<Cluster> page = Render<Cluster>();

        Assert.Contains(
            ScopeLink.Monitor(Leaf),
            page.FindAll("section.list .row a.scope").Select(link => link.GetAttribute("href")));
        Assert.Equal(
            // The fixture's three nodes, by name.
            [
                .. new[] { Names.WithRole("receiving"), Names.WithRole("processing"), Sender }
                    .Order(StringComparer.Ordinal)
                    .Select(name => ScopeLink.Monitor($"{Names.Scope}/node/{name}")),
            ],
            page.FindAll("section.nodes .row a.scope").Select(link => link.GetAttribute("href")));
    }

    [Fact]
    public void TheTopologysOpenNodeNamesItsProblemAndLinksToIt()
    {
        Address(ScopeLink.TopologyAt($"node/{Sender}"));

        AngleSharp.Dom.IElement problem =
            Render<Topology>().Find("aside.topology-inspector dd .why a.leaf");

        Assert.Equal(ScopeLink.Monitor(Leaf), problem.GetAttribute("href"));
    }

    [Fact]
    public void ALocationsOwnVerdictStandsAboveWhatIsBeneathIt()
    {
        // 2026-09-26: the drill's last step at a Done Send Location showed
        // only its fine identity step, and the cause was gone.
        string path = Path.Combine(Path.GetTempPath(), $"xmip-own-{Guid.NewGuid():N}.toml");
        string location = $"{Names.Scope}/node/{Sender}/send/dns/regex";
        File.WriteAllText(path, $"""
            node = "{Names.Scope}"
            [[records]]
            scope = "{location}"
            state = "done"
            severity = 90
            evidence = "no port was free"
            [[records]]
            scope = "{location}/identity"
            state = "fine"
            """);
        using BunitContext own = new();
        own.Services.AddSingleton(ClusterSurfaces.Over(new SnapshotOperator(path)));
        own.Services.AddSingleton(new RoleContext(Role.Observer, "tester"));
        own.Services.AddSingleton(new ProgramAudit("Xmip.Gui.Test", Unacted.Audit));
        own.Services.GetRequiredService<NavigationManager>()
            .NavigateTo(ScopeLink.Monitor(location));

        IRenderedComponent<Cluster> page = own.Render<Cluster>();

        Assert.Contains(
            "no port was free",
            page.Find("section.drill .drill-leaf").TextContent,
            StringComparison.Ordinal);
        Assert.Single(page.FindAll("section.drill a.branch"));
        File.Delete(path);
    }

    [Fact]
    public void EveryViewHasOneLineAboveItsDataAndNamesItselfOnce()
    {
        // Until 2026-09-26 each view had a bar of its own under the navigation
        // that named the view a second time.
        foreach (IRenderedComponent<IComponent> page in
            (IRenderedComponent<IComponent>[])
                [Render<Cluster>(), Render<Configuration>(), Render<Topology>()])
        {
            Assert.Empty(page.FindAll("header.bar"));
            Assert.Single(page.FindAll("p.run-line"));
        }
    }

    private void Address(string uri)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(uri);
    }

    private static List<string> Crumbs(IRenderedComponent<Cluster> page)
    {
        return [.. page.FindAll("nav.crumbs a.crumb-btn").Select(crumb => crumb.TextContent)];
    }
}
