using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Gui.Components;
using Xmip.Gui.Pages;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// The Dead Message Queue view (ADR-0052, amendment 2026-10-01: *lists per
/// cluster and node, opens one to show its promoted properties and every
/// Subscription's decline, and offers Replay as an Operator act*). Rendered
/// over a publication of the test cluster whose receiving and processing
/// nodes keep three Messages no Subscription matched, and whose publisher
/// takes orders: the list grouped by node, the drill to a node and to one
/// Message with its gate verdicts, promoted properties and declines in the
/// order written, Replay for an Operator, the list an Observer is shown
/// without it, and every Replay in the host's audit as
/// <c>dead-message.replay</c>.
/// </summary>
public sealed class DeadMessagesViewTest : BunitContext, IDisposable
{
    private static readonly TestCluster Names = TestCluster.Read();
    private static readonly string Receiver = Names.WithRole("receiving");
    private static readonly string Processor = Names.WithRole("processing");

    /// <summary>The receiving node's address, escaped for a query.</summary>
    private static readonly string AtReceiver =
        Uri.EscapeDataString($"{Names.Scope}/node/{Receiver}");

    private readonly string _place = Path.Combine(
        Path.GetTempPath(), $"xmip-gui-dead-messages-{Guid.NewGuid():N}");

    public DeadMessagesViewTest()
    {
        Directory.CreateDirectory(_place);
        string snapshot = Path.Combine(_place, $"{Names.Name}-snapshot.toml");
        File.WriteAllText(
            snapshot,
            $"node = \"{Names.Scope}\"\n"
            + $"orders = '{Orders}'\n"
            + Entry(Receiver, "m-1", 1, "declines = [[\"structured\", \"no MessageType\"]]\n")
            + Entry(
                Receiver, "m-2", 2,
                "validation = [[\"schema\", \"valid\"], [\"signature\", \"valid\"]]\n"
                + "promoted = [[\"MessageType\", \"Invoice\"], [\"Party\", \"partner-x\"]]\n"
                + "declines = [[\"structured\", \"MessageType is Invoice, not json\"], "
                + "[\"edi\", \"paused\"]]\n")
            + Entry(Processor, "m-3", 3, string.Empty));
        Services.AddSingleton(ClusterSurfaces.Over(new SnapshotOperator(snapshot)));
        Services.AddSingleton(new ProgramAudit("Xmip.Gui.Test", Audited));
    }

    private string Orders => Path.Combine(_place, "orders");

    private string Audited => Path.Combine(_place, "audit");

    private static string Entry(string node, string message, int sequence, string rest)
    {
        return $"[[dead_messages]]\nnode = \"{Names.Scope}/node/{node}\"\n"
            + $"message = \"{message}\"\nsequence = {sequence}\nlocation = \"orders\"\n"
            + $"received_unix_nanos = {sequence * 1_000_000_000L}\n{rest}";
    }

    void IDisposable.Dispose()
    {
        Directory.Delete(_place, recursive: true);
        base.Dispose();
    }

    private IRenderedComponent<DeadMessages> At(string address, Role role)
    {
        Services.AddSingleton(new RoleContext(role));
        Services.GetRequiredService<NavigationManager>().NavigateTo(address);
        return Render<DeadMessages>();
    }

    private static string[] Column(IRenderedComponent<DeadMessages> page, string column)
    {
        return
        [
            .. page.FindAll($"div.subs-row:not(.subs-head) .{column}")
                .Select(cell => cell.TextContent.Trim()),
        ];
    }

    [Fact]
    public void TheListSaysEveryMessageOfTheClusterOldestFirstAndGroupsTheNodes()
    {
        IRenderedComponent<DeadMessages> page = At("/dead-messages", Role.Observer);

        Assert.Equal(["m-1", "m-2", "m-3"], Column(page, "dead-message"));
        Assert.Equal([Names.Name, Names.Name, Names.Name], Column(page, "subs-cluster"));
        Assert.Equal([Receiver, Receiver, Processor], Column(page, "subs-node"));
        Assert.Equal(["orders", "orders", "orders"], Column(page, "dead-location"));
        Assert.Equal(["1", "2", "0"], Column(page, "dead-declines"));
        Assert.Equal(
            ["received ↑", "message", "cluster", "node", "Receive Location", "declines"],
            page.FindAll("a.subs-sort").Select(head => head.TextContent.Trim()));
        Assert.Equal(
            new[] { Receiver, Processor }.Order(StringComparer.Ordinal),
            page.FindAll("a.subs-group .label").Select(label => label.TextContent.Trim()));
        Assert.All(
            page.FindAll("a.subs-group"), group => Assert.Contains("none", group.ClassList));
        Assert.Contains(
            "3 of 3 Message(s)", page.Find(".subs-count").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDrillStandsAtANode()
    {
        IRenderedComponent<DeadMessages> node =
            At($"/dead-messages?location={AtReceiver}", Role.Observer);

        Assert.Equal(["m-1", "m-2"], Column(node, "dead-message"));
        Assert.Empty(node.FindAll("a.subs-group"));
        Assert.Equal(
            ["dead message queue", $"cluster {Names.Name}", $"node {Receiver}"],
            node.FindAll(".crumbs .crumb-btn").Select(crumb => crumb.TextContent.Trim()));
    }

    [Fact]
    public void OneMessageShowsItsVerdictsPropertiesAndDeclinesInTheOrderWritten()
    {
        IRenderedComponent<DeadMessages> page = At(
            $"/dead-messages?location={AtReceiver}&message=m-2", Role.Observer);

        Assert.Contains(
            $"Message m-2 on {Receiver}", page.Find(".dead-detail h2").TextContent,
            StringComparison.Ordinal);
        Assert.Equal(
            ["schema", "valid", "signature", "valid"], Pairs(page, "validation"));
        Assert.Equal(
            ["MessageType", "Invoice", "Party", "partner-x"], Pairs(page, "promoted"));
        Assert.Equal(
            ["structured", "MessageType is Invoice, not json", "edi", "paused"],
            Pairs(page, "declines"));
        Assert.Equal(
            ["dead message queue", $"cluster {Names.Name}", $"node {Receiver}", "message m-2"],
            page.FindAll(".crumbs .crumb-btn").Select(crumb => crumb.TextContent.Trim()));
    }

    [Fact]
    public void AnObserverIsShownTheListAndNoAct()
    {
        IRenderedComponent<DeadMessages> list = At("/dead-messages", Role.Observer);
        Assert.Empty(list.FindAll("button"));

        Services.GetRequiredService<NavigationManager>().NavigateTo(
            $"/dead-messages?location={AtReceiver}&message=m-2");
        IRenderedComponent<DeadMessages> one = Render<DeadMessages>();
        Assert.Single(one.FindAll(".dead-detail"));
        Assert.Empty(one.FindAll("button"));
        Assert.False(Directory.Exists(Orders));
    }

    [Fact]
    public void AnOperatorReplaysAndEveryReplayIsAudited()
    {
        IRenderedComponent<DeadMessages> page = At("/dead-messages", Role.Operator);

        Assert.Equal(
            ["Replay", "Replay", "Replay"],
            page.FindAll("button").Select(button => button.TextContent.Trim()));
        Assert.Equal(
            DeadMessageReplay.ReplaySaid, page.FindAll("button")[0].GetAttribute("title"));

        page.FindAll("button")[1].Click();

        Assert.Equal(["dead-message m-2 replay"], OrdersLeft.For(Orders, Receiver));
        Assert.Contains(
            $"left for {Receiver}", page.Find(".subs-said").TextContent, StringComparison.Ordinal);

        string audit = File.ReadAllText(Path.Combine(Audited, "audit.toml"));
        Assert.Contains("action = \"dead-message.replay\"", audit, StringComparison.Ordinal);
        Assert.Contains("\"message\" = \"m-2\"", audit, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOpenedMessageIsReplayedAndSaysWhatAReplayDoes()
    {
        IRenderedComponent<DeadMessages> page = At(
            $"/dead-messages?location={AtReceiver}&message=m-1", Role.Operator);

        Assert.Equal(
            DeadMessageReplay.ReplaySaid, page.Find(".dead-detail .subs-act-said").TextContent);

        page.Find(".dead-detail button").Click();

        Assert.Equal(["dead-message m-1 replay"], OrdersLeft.For(Orders, Receiver));
    }

    [Fact]
    public void AColumnOrdersTheOtherWayWhenAsked()
    {
        IRenderedComponent<DeadMessages> sorted =
            At("/dead-messages?sort=declines&order=descending", Role.Observer);

        Assert.Equal(["m-2", "m-1", "m-3"], Column(sorted, "dead-message"));
    }

    private static string[] Pairs(IRenderedComponent<DeadMessages> page, string kind)
    {
        return
        [
            .. page.FindAll($"dl.dead-{kind} dt, dl.dead-{kind} dd")
                .Select(cell => cell.TextContent.Trim()),
        ];
    }
}
