using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Gui.Pages;
using Xmip.Gui.Surface;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// What the owner sees of a node the run reads, his words of 2026-11-10 held
/// (ADR-0052, amendment 2026-11-11): the Work Process card says no Work
/// Process is placed while the process stage holds Subscriptions alone; a
/// stage reads as the Work Process in the drill's crumbs and in the
/// Configuration view; every row the Monitor lists says from and to; and a
/// test's own verdict beneath a Receive Location is no configuration.
/// </summary>
public sealed class ServedWordsTest : BunitContext
{
    private static readonly TestCluster Names = TestCluster.Read();
    private static readonly string Processor = Names.WithRole("processing");
    private static readonly string Node = $"{Names.Scope}/node/{Processor}";
    private readonly string path =
        Path.Combine(Path.GetTempPath(), $"xmip-served-{Guid.NewGuid():N}.xmip");

    public ServedWordsTest()
    {
        File.WriteAllText(path, $$"""
            node = "{{Names.Scope}}"

            [run]
            cluster = "{{Names.Name}}"
            tests = ["Carried"]
            nodes = ["{{Processor}}"]
            real = ["{{Processor}}"]

            [[records]]
            scope = "{{Node}}/receive/In"
            state = "fine"

            [[records]]
            scope = "{{Node}}/receive/In/delivery"
            state = "stressed"
            severity = 60
            evidence = "a document no Subscription takes was dropped"

            [[records]]
            scope = "{{Node}}/process/onward"
            state = "fine"

            [[subscriptions]]
            node = "{{Node}}"
            name = "onward"
            send_ports = ["Out"]

            [topology]
            observed_unix_nanos = 5

            [[topology.nodes]]
            id = "node/{{Processor}}"
            kind = "node"
            scope = "{{Node}}"

            [[topology.nodes]]
            id = "node/{{Processor}}/receive/In"
            parent = "node/{{Processor}}/receive"
            kind = "endpoint"
            scope = "{{Node}}/receive/In"

            [[topology.links]]
            id = "in"
            from = "node/{{Processor}}/receive/In"
            to = "node/{{Processor}}/process/onward"
            """);
        Services.AddSingleton(ClusterSurfaces.Over(new SnapshotOperator(path)));
        Services.AddSingleton(new RoleContext(Role.Observer, RoleAssignment.ProcessCaller()));
        Services.AddSingleton(new ProgramAudit("Xmip.Gui.Test", Unacted.Audit));
    }

    [Fact]
    public void TheWorkProcessCardSaysNoneIsPlacedBesideTheSubscriptionsItCounts()
    {
        IRenderedComponent<Cluster> page = Render<Cluster>();
        string middle = page.FindAll("section.stages .stage .items")[1].TextContent;

        Assert.StartsWith(
            $"Subscription ×1 · {English.NoWorkProcess} · ", middle, StringComparison.Ordinal);
    }

    [Fact]
    public void TheStageReadsAsTheWorkProcessInTheCrumbsAndTheConfiguration()
    {
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo(ScopeLink.Monitor($"{Node}/process"));
        IRenderedComponent<Cluster> page = Render<Cluster>();

        Assert.Equal(
            "Work Process",
            page.FindAll("nav.crumbs a.crumb-btn")[^1].TextContent);

        IRenderedComponent<Configuration> tree = Render<Configuration>();
        List<string> labels =
        [
            .. tree.FindAll(".tree-row .label").Select(label => label.TextContent),
        ];
        Assert.Contains("Work Process", labels);
        Assert.DoesNotContain("process", labels);
        Assert.DoesNotContain("delivery", labels);
    }

    [Fact]
    public void EveryRowNeedingAttentionSaysFromAndTo()
    {
        IRenderedComponent<Cluster> page = Render<Cluster>();

        IReadOnlyList<AngleSharp.Dom.IElement> rows = page.FindAll("section.list .row");
        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Equal(
            $"from In [{Processor}] → to Out",
            row.QuerySelector(".from-to")?.TextContent));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        File.Delete(path);
    }
}
