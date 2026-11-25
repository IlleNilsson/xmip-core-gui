using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Gui.Components;
using Xmip.Gui.Pages;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// The Instances view (runtime-model.md section 22; ADR-0066, amendment
/// 2026-11-25): over a publication of the test cluster whose processing node
/// runs Order in parts — one Instance waiting for the rest of its order, one
/// failed by a Message that waits in its queue — and whose receiving node
/// runs Returns. Each Work Process with its Instances and the Messages that
/// failed in its queue; the drill to a node and to one Instance with its
/// values and history of Messages; Retry and Dismiss for an Operator,
/// declined by the snapshot, which is never acted on (ADR-0014, amendment
/// 2026-11-13), each in the host's audit as <c>message.retry</c> or
/// <c>message.dismiss</c>; the list an Observer is shown without them.
/// </summary>
public sealed class InstancesViewTest : BunitContext
{
    private static readonly TestCluster Names = TestCluster.Read();
    private static readonly string Receiver = Names.WithRole("receiving");
    private static readonly string Processor = Names.WithRole("processing");

    /// <summary>The processing node's address, escaped for a query.</summary>
    private static readonly string AtProcessor =
        Uri.EscapeDataString($"{Names.Scope}/node/{Processor}");

    private readonly TestFolder _place = new("gui-instances");

    public InstancesViewTest()
    {
        string snapshot = _place.Inside($"{Names.Name}-snapshot.xmip");
        File.WriteAllText(
            snapshot,
            $"node = \"{Names.Scope}\"\n"
            + $"[[work_process_instances]]\nnode = \"{Names.Scope}/node/{Processor}\"\n"
            + "work_process = \"Order in parts\"\nfailed_count = 1\n"
            + Instance("i-1", "state = \"waiting\"\nwaits_for = \"message or time\"\n"
                + "until_unix_nanos = 9000000000\ncorrelations = [[\"Order\", \"A-1001\"]]\n"
                + "values = [[\"Ordered\", \"3\"], [\"Delivered\", \"1\"]]\n")
            + "[[work_process_instances.instances.history]]\nat_unix_nanos = 1000000000\n"
            + "what = \"started\"\nmessage = \"m-1\"\njourney = \"j-0\"\n"
            + Instance("i-2", "state = \"failed\"\ncode = \"offline\"\n"
                + "reason = \"the unit cannot reach its stock\"\nfailed_by = \"j-1\"\n")
            + "[[work_process_instances.failed]]\njourney = \"j-1\"\nsequence = 7\n"
            + "reason = \"Order in parts threw offline\"\n"
            + $"[[work_process_instances]]\nnode = \"{Names.Scope}/node/{Receiver}\"\n"
            + "work_process = \"Returns\"\nfailed_count = 0\n"
            + Instance("i-3", "state = \"completed\"\n"));
        Services.AddSingleton(ClusterSurfaces.Over(new SnapshotOperator(snapshot)));
        Services.AddSingleton(new ProgramAudit("Xmip.Gui.Test", Audited));
    }

    private string Audited => _place.Inside("audit");

    private static string Instance(string id, string rest)
    {
        return $"[[work_process_instances.instances]]\ninstance = \"{id}\"\n{rest}"
            + "turns = 2\nupdated_unix_nanos = 5000000000\n";
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        await base.DisposeAsyncCore();
        _place.Dispose();
    }

    private IRenderedComponent<Instances> At(string address, Role role)
    {
        Services.AddSingleton(new RoleContext(role, RoleAssignment.ProcessCaller()));
        Services.GetRequiredService<NavigationManager>().NavigateTo(address);
        return Render<Instances>();
    }

    private static string[] Attribute(IRenderedComponent<Instances> page, string css, string name)
    {
        return [.. page.FindAll(css).Select(element => element.GetAttribute(name) ?? string.Empty)];
    }

    [Fact]
    public void EachWorkProcessListsItsInstancesAndTheMessagesThatFailedInItsQueue()
    {
        IRenderedComponent<Instances> page = At("/instances", Role.Observer);

        Assert.Equal(
            ["Order in parts", "Returns"],
            Attribute(page, "section.work-process", "data-work-process"));
        Assert.Equal(["i-1", "i-2", "i-3"], Attribute(page, "tr.instance-row", "data-instance"));
        Assert.Equal(
            ["waiting", "failed", "completed"],
            page.FindAll("tr.instance-row .instance-state").Select(cell => cell.TextContent));
        Assert.Equal(
            "Ordered 3, Delivered 1",
            page.Find("tr.instance-row .instance-values").TextContent);
        Assert.Equal(["j-1"], Attribute(page, ".message-failed", "data-journey"));
        Assert.Contains(
            "3 of 3 Instance(s)", page.Find(".subs-count").TextContent, StringComparison.Ordinal);
        Assert.Empty(page.FindAll("button.act"));
    }

    [Fact]
    public void TheDrillStandsAtANodeAndOpensOneInstanceWithItsHistory()
    {
        IRenderedComponent<Instances> node =
            At($"/instances?location={AtProcessor}", Role.Observer);
        Assert.Equal(["i-1", "i-2"], Attribute(node, "tr.instance-row", "data-instance"));

        Services.GetRequiredService<NavigationManager>().NavigateTo(
            $"/instances?location={AtProcessor}&instance=i-1");
        IRenderedComponent<Instances> one = Render<Instances>();
        Assert.Contains(
            "Instance i-1 of Order in parts", one.Find(".instance-detail h2").TextContent,
            StringComparison.Ordinal);
        Assert.Equal(
            ["Ordered", "3", "Delivered", "1"],
            one.FindAll("dl.instance-values dt, dl.instance-values dd")
                .Select(cell => cell.TextContent.Trim()));
        Assert.Contains(
            "started", one.Find("table.instance-history tbody").TextContent,
            StringComparison.Ordinal);
        Assert.Equal(
            ["instances", $"cluster {Names.Name}", $"node {Processor}", "instance i-1"],
            one.FindAll(".crumbs .crumb-btn").Select(crumb => crumb.TextContent.Trim()));
    }

    [Fact]
    public void AnOperatorRetriesOrDismissesAFailedMessageAndEachIsAudited()
    {
        IRenderedComponent<Instances> page = At("/instances", Role.Operator);

        Assert.Equal(
            InstanceDetail.RetrySaid,
            page.FindAll(".message-failed button")[0].GetAttribute("title"));
        page.FindAll(".message-failed button")[0].Click();
        Assert.Contains(
            SnapshotOperator.NotActed, page.Find(".subs-said").TextContent,
            StringComparison.Ordinal);

        page.FindAll(".message-failed button")[1].Click();
        Assert.Equal(
            InstanceDetail.Asked("j-1"), page.Find(".message-confirm .subs-act-said").TextContent);
        page.Find(".message-confirm button.dismiss-confirm").Click();

        Assert.Equal(
            ["message.retry j-1", "message.dismiss j-1"],
            ActsAsked.Of(Audited, Processor, "journey"));
    }

    [Fact]
    public void AFailedInstanceOpenedOffersTheActsOnTheMessageThatFailedIt()
    {
        IRenderedComponent<Instances> page =
            At($"/instances?location={AtProcessor}&instance=i-2", Role.Operator);

        Assert.Contains(
            "offline: the unit cannot reach its stock",
            page.Find(".instance-threw").TextContent,
            StringComparison.Ordinal);
        Assert.Equal(["j-1"], Attribute(page, ".instance-detail .message-failed", "data-journey"));

        page.Find(".instance-detail .message-failed button").Click();

        Assert.Equal(["message.retry j-1"], ActsAsked.Of(Audited, Processor, "journey"));
    }
}
