using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Gui.Pages;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// The Event subscriptions view (the owner, 2026-09-29: *operation needs a view of
/// event subscriptions. Subscriber, Cluster, Node, Action. One should be able
/// to pause, resume and remove event subscriptions*; ADR-0065, amendment of
/// the same date). Rendered over a publication of the test cluster whose
/// receiving and sending nodes hold three Event subscriptions and whose
/// publisher takes orders: the list, the drill, the order, the acts an
/// Operator takes and the list an Observer is shown without them — and every act in the host's audit as
/// <c>event.&lt;act&gt;</c>, the action <c>observe::Noun::EventSubscription</c> gives it.
/// </summary>
public sealed class EventSubscriptionsViewTest : BunitContext, IDisposable
{
    private static readonly TestCluster Names = TestCluster.Read();
    private static readonly string Receiver = Names.WithRole("receiving");
    private static readonly string Processor = Names.WithRole("processing");
    private static readonly string Sender = Names.WithRole("sending");
    private static readonly string Nodes = $"{Names.Scope}/node";

    /// <summary>The receiving node's address, escaped for a query.</summary>
    private static readonly string AtReceiver = Uri.EscapeDataString($"{Nodes}/{Receiver}");

    private readonly string _place = Path.Combine(
        Path.GetTempPath(), $"xmip-gui-event-subscriptions-{Guid.NewGuid():N}");

    public EventSubscriptionsViewTest()
    {
        Directory.CreateDirectory(_place);
        string snapshot = Path.Combine(_place, $"{Names.Name}-snapshot.toml");
        File.WriteAllText(
            snapshot,
            $"node = \"{Names.Scope}\"\n"
            + $"orders = '{Orders}'\n"
            + Entry(Receiver, 1, "every Event", "active", 3)
            + Entry(Receiver, 2, "every Event ending failure", "paused", 9)
            + Entry(Sender, 1, "every Event", "active", 0)
            + $"[[unheard]]\nby = \"{Nodes}/{Receiver}\"\nnode = \"{Nodes}/{Processor}\"\n"
            + "since_unix_nanos = 1790000000000000000\nwhy = \"connection refused\"\n");
        Services.AddSingleton(ClusterSurfaces.Over(new SnapshotOperator(snapshot)));
        Services.AddSingleton(new ProgramAudit("Xmip.Gui.Test", Audited));
    }

    private string Orders => Path.Combine(_place, "orders");

    private string Audited => Path.Combine(_place, "audit");

    private static string Entry(string node, int id, string action, string state, int queued)
    {
        return $"[[event_subscriptions]]\nnode = \"{Nodes}/{node}\"\nid = {id}\n"
            + $"subscriber = \"{(id == 1 ? "operations" : "on-call")}\"\n"
            + $"party = \"0199a0a0-0000-7000-8000-00000000000{id}\"\n"
            + $"action = \"{action}\"\nscope = \"{Nodes}/{node}\"\n"
            + $"state = \"{state}\"\nqueued = {queued}\ncapacity = 64\n";
    }

    void IDisposable.Dispose()
    {
        Directory.Delete(_place, recursive: true);
        base.Dispose();
    }

    private IRenderedComponent<EventSubscriptions> At(string address, Role role)
    {
        Services.AddSingleton(new RoleContext(role, "tester"));
        Services.GetRequiredService<NavigationManager>().NavigateTo(address);
        return Render<EventSubscriptions>();
    }

    private static string[] Column(IRenderedComponent<EventSubscriptions> page, string column)
    {
        return
        [
            .. page.FindAll($"div.subs-row:not(.subs-head) .{column}")
                .Select(cell => cell.TextContent.Trim()),
        ];
    }

    [Fact]
    public void AMemberANodeDoesNotHearIsSaidInOneLineAndOfferedNoAct()
    {
        IRenderedComponent<EventSubscriptions> page = At("/event-subscriptions", Role.Operator);

        string said = Assert.Single(page.FindAll("ul.unheard li")).TextContent.Trim();
        Assert.Equal(
            $"{Receiver}: not hearing {Nodes}/{Processor} since 2026-09-21T14:13:20Z: "
            + "connection refused",
            said);
        Assert.Empty(page.FindAll("ul.unheard button"));
    }

    [Fact]
    public void ANodeThatHearsEveryMemberSaysNothingUnheard()
    {
        IRenderedComponent<EventSubscriptions> page =
            At($"/event-subscriptions?location={Nodes}/{Sender}", Role.Operator);

        Assert.Empty(page.FindAll("ul.unheard li"));
    }

    [Fact]
    public void TheListSaysSubscriberClusterNodeAndActionAndGroupsTheNodes()
    {
        IRenderedComponent<EventSubscriptions> page = At("/event-subscriptions", Role.Observer);

        Assert.Equal([Names.Name, Names.Name, Names.Name], Column(page, "subs-cluster"));
        // By subscriber, the first column — the name its Party was declared
        // with, never its identifier — and then by node and number.
        Assert.Equal(["on-call", "operations", "operations"], Column(page, "event-subscriber"));
        Assert.Equal([Receiver, Receiver, Sender], Column(page, "subs-node"));
        Assert.Equal(
            ["every Event ending failure", "every Event", "every Event"],
            Column(page, "event-action"));
        Assert.Equal(["paused", "active", "active"], Column(page, "subs-state"));
        Assert.Equal(
            [Receiver, Sender],
            page.FindAll("a.subs-group .label").Select(label => label.TextContent.Trim()));
        Assert.Contains("1 paused", page.Find(".subs-count").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void AnObserverIsShownTheListAndNoAct()
    {
        IRenderedComponent<EventSubscriptions> page =
            At($"/event-subscriptions?location={AtReceiver}&id=2", Role.Observer);

        Assert.Contains(
            $"Event subscription 2 on {Receiver}", page.Find(".subs-detail h2").TextContent,
            StringComparison.Ordinal);
        string detail = page.Find(".subs-fields").TextContent;
        Assert.Contains("on-call", detail, StringComparison.Ordinal);
        Assert.Contains(
            "0199a0a0-0000-7000-8000-000000000002", detail, StringComparison.Ordinal);
        Assert.Empty(page.FindAll("button.act"));
        Assert.False(Directory.Exists(Orders));
    }

    [Fact]
    public void AnOperatorPausesResumesAndRemovesAndEveryActIsAuditedAsAnEventAct()
    {
        IRenderedComponent<EventSubscriptions> page = At("/event-subscriptions", Role.Operator);

        Assert.Equal(
            ["Resume", "Remove", "Pause", "Remove", "Pause", "Remove"],
            page.FindAll("button.act").Select(button => button.TextContent.Trim()));

        page.FindAll("button.act")[2].Click();
        page.FindAll("button.act")[0].Click();
        page.FindAll("button.act")[5].Click();

        Assert.Equal(
            ["event-subscription 1 pause", "event-subscription 2 resume"],
            OrdersLeft.For(Orders, Receiver));
        Assert.Equal(["event-subscription 1 remove"], OrdersLeft.For(Orders, Sender));
        Assert.Contains(
            $"left for {Sender}", page.Find(".subs-said").TextContent, StringComparison.Ordinal);

        string audit = File.ReadAllText(Path.Combine(Audited, "audit.toml"));
        foreach (string act in new[] { "pause", "resume", "remove" })
        {
            Assert.Contains($"action = \"event.{act}\"", audit, StringComparison.Ordinal);
        }

        Assert.Contains("\"subscriber\" = \"operations\"", audit, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDrillStandsAtANode()
    {
        IRenderedComponent<EventSubscriptions> node =
            At($"/event-subscriptions?location={AtReceiver}", Role.Observer);

        Assert.Equal([Receiver, Receiver], Column(node, "subs-node"));
        Assert.Empty(node.FindAll("a.subs-group"));
        Assert.Equal(
            ["event subscriptions", $"cluster {Names.Name}", $"node {Receiver}"],
            node.FindAll(".crumbs .crumb-btn").Select(crumb => crumb.TextContent.Trim()));
    }

    [Fact]
    public void AColumnOrdersTheOtherWayWhenAsked()
    {
        IRenderedComponent<EventSubscriptions> sorted =
            At("/event-subscriptions?sort=queued&order=descending", Role.Observer);
        Assert.Equal(["9 of 64", "3 of 64", "0 of 64"], Column(sorted, "subs-figure").Where(
            (_, index) => index % 3 == 0));
    }
}
