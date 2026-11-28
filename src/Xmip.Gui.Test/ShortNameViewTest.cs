using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Gui.Components;
using Xmip.Gui.Pages;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// Where a view shows the cluster's name, it shows the short name its
/// publisher says, the tooltip carrying the rest, Web and Desktop alike
/// (ADR-0028 and ADR-0052, amendments 2026-11-28): the status bar, each
/// cluster in the chooser, and the drill's cluster crumb. The link keeps the
/// cluster's name, the one its file declares. Rendered over the test cluster's fixture
/// with a <c>[cluster]</c> table written beneath it, and the second
/// cluster's fixture, which says none.
/// </summary>
public sealed class ShortNameViewTest : BunitContext
{
    private static readonly TestCluster Names = TestCluster.Read();

    private static string Fixture(string name)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixture", name);
    }

    [Fact]
    public void TheShortNameShowsWhereTheClusterIsNamedAndItsTooltipCarriesTheRest()
    {
        using TestFolder folder = new("gui-short-name");
        string named = folder.Inside("named.xmip");
        File.WriteAllText(
            named,
            File.ReadAllText(Fixture("cluster.xmip"))
                + "\n[cluster]\nshort_name = \"Ab\"\nfile = \"c/a-file.xmip\"\n");
        using ClusterSurfaces surfaces = ClusterSurfaces.Over(
            [new SnapshotOperator(named), new SnapshotOperator(Fixture("cluster-cl2.xmip"))]);
        string second = surfaces.Clusters.Single(name => name != Names.Name);
        Services.AddSingleton(surfaces);
        Services.AddSingleton(new RoleContext(Role.Observer, RoleAssignment.ProcessCaller()));
        Services.AddSingleton(new ProgramAudit("Xmip.Gui.Test", Unacted.Audit));
        string title = $"a-file · short name Ab · cluster {Names.Name} · from c/a-file.xmip";

        IRenderedComponent<StatusBar> bar = Render<StatusBar>();
        Assert.Equal("Ab", bar.Find(".status-cluster").TextContent.Trim());
        Assert.Equal(title, bar.Find(".status-cluster").GetAttribute("title"));

        IRenderedComponent<Cluster> page = Render<Cluster>();
        Assert.Equal(
            ["Ab", second],
            page.FindAll("a.cluster-pick").Select(pick => pick.TextContent.Trim()));
        Assert.Equal(title, page.FindAll("a.cluster-pick")[0].GetAttribute("title"));
        Assert.Equal($"cluster {second}", page.FindAll("a.cluster-pick")[1].GetAttribute("title"));
        Assert.Contains(
            $"cluster={Names.Name}", page.FindAll("a.cluster-pick")[0].GetAttribute("href"),
            StringComparison.Ordinal);

        // The Monitor's own crumb at the cluster, and the drill's cluster crumb
        // a list view draws (DrillTrail).
        AngleSharp.Dom.IElement top = page.FindAll("nav.crumbs a.crumb-btn")[0];
        Assert.Equal(("Ab", title), (top.TextContent.Trim(), top.GetAttribute("title")));
        Services.GetRequiredService<NavigationManager>().NavigateTo(
            $"/subscriptions?location={Uri.EscapeDataString(Names.NodeScope(0))}");
        IRenderedComponent<Subscriptions> drilled = Render<Subscriptions>();
        AngleSharp.Dom.IElement crumb = drilled.FindAll("nav.crumbs a.crumb-btn")
            .Single(one => one.TextContent.Trim() == "cluster Ab");
        Assert.Equal(title, crumb.GetAttribute("title"));
    }
}
