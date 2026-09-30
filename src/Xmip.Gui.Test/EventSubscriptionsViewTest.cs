using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Gui.Pages;
using Xmip.Gui.Surface;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// The Event subscriptions view (the owner, 2026-09-29: *operation needs a view of
/// event subscriptions. Subscriber, Cluster, Node, Action. One should be able
/// to pause, resume and remove event subscriptions*; ADR-0065, amendment of
/// the same date). Rendered over a publication of cluster CT whose nodes alpha
/// and gamma hold three Event subscriptions and whose publisher takes orders: the
/// list, the drill, the order, the acts an Operator takes and the list an
/// Observer is shown without them — and every act in the host's audit as
/// <c>event.&lt;act&gt;</c>, the action <c>observe::Noun::EventSubscription</c> gives it.
/// </summary>
public sealed class EventSubscriptionsViewTest : BunitContext, IDisposable
{
    private readonly string _place = Path.Combine(
        Path.GetTempPath(), $"xmip-gui-event-subscriptions-{Guid.NewGuid():N}");

    public EventSubscriptionsViewTest()
    {
        Directory.CreateDirectory(_place);
        string snapshot = Path.Combine(_place, "CT-snapshot.toml");
        File.WriteAllText(
            snapshot,
            "node = \"xmip:///CT\"\n"
            + $"orders = '{Orders}'\n"
            + Entry("alpha", 1, "every Event", "active", 3)
            + Entry("alpha", 2, "every Event ending failure", "paused", 9)
            + Entry("gamma", 1, "every Event", "active", 0));
        Services.AddSingleton(ClusterSurfaces.Over(new SnapshotOperator(snapshot)));
        Services.AddSingleton(new ProgramAudit("Xmip.Gui.Test", Audited));
    }

    private string Orders => Path.Combine(_place, "orders");

    private string Audited => Path.Combine(_place, "audit");

    private static string Entry(string node, int id, string action, string state, int queued)
    {
        return $"[[event_subscriptions]]\nnode = \"xmip:///CT/node/{node}\"\nid = {id}\n"
            + $"subscriber = \"{(id == 1 ? "operations" : "on-call")}\"\n"
            + $"party = \"0199a0a0-0000-7000-8000-00000000000{id}\"\n"
            + $"action = \"{action}\"\nscope = \"xmip:///CT/node/{node}\"\n"
            + $"state = \"{state}\"\nqueued = {queued}\ncapacity = 64\n";
    }

    void IDisposable.Dispose()
    {
        Directory.Delete(_place, recursive: true);
        base.Dispose();
    }

    private IRenderedComponent<EventSubscriptions> At(string address, Role role)
    {
        Services.AddSingleton(new RoleContext(role));
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
    public void TheListSaysSubscriberClusterNodeAndActionAndGroupsTheNodes()
    {
        IRenderedComponent<EventSubscriptions> page = At("/event-subscriptions", Role.Observer);

        Assert.Equal(["CT", "CT", "CT"], Column(page, "subs-cluster"));
        // By subscriber, the first column — the name its Party was declared
        // with, never its identifier — and then by node and number.
        Assert.Equal(["on-call", "operations", "operations"], Column(page, "event-subscriber"));
        Assert.Equal(["alpha", "alpha", "gamma"], Column(page, "subs-node"));
        Assert.Equal(
            ["every Event ending failure", "every Event", "every Event"],
            Column(page, "event-action"));
        Assert.Equal(["paused", "active", "active"], Column(page, "subs-state"));
        Assert.Equal(
            ["alpha", "gamma"],
            page.FindAll("a.subs-group .label").Select(label => label.TextContent.Trim()));
        Assert.Contains("1 paused", page.Find(".subs-count").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void AnObserverIsShownTheListAndNoAct()
    {
        IRenderedComponent<EventSubscriptions> page =
            At("/event-subscriptions?location=xmip%3A%2F%2F%2FCT%2Fnode%2Falpha&id=2", Role.Observer);

        Assert.Contains("Event subscription 2 on alpha", page.Find(".subs-detail h2").TextContent,
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
            OrdersLeft.For(Orders, "alpha"));
        Assert.Equal(["event-subscription 1 remove"], OrdersLeft.For(Orders, "gamma"));
        Assert.Contains(
            "left for gamma", page.Find(".subs-said").TextContent, StringComparison.Ordinal);

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
            At("/event-subscriptions?location=xmip%3A%2F%2F%2FCT%2Fnode%2Falpha", Role.Observer);

        Assert.Equal(["alpha", "alpha"], Column(node, "subs-node"));
        Assert.Empty(node.FindAll("a.subs-group"));
        Assert.Equal(
            ["event subscriptions", "cluster CT", "node alpha"],
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

    [Theory]
    [InlineData(null, Role.Developer)]
    [InlineData("operator", Role.Operator)]
    [InlineData("observer", Role.Observer)]
    [InlineData("sovereign", Role.Observer)]
    public void BothHostsTakeTheRoleByOneRule(string? stated, Role expected)
    {
        string? before = Environment.GetEnvironmentVariable(RoleContext.EnvironmentVariable);
        Environment.SetEnvironmentVariable(RoleContext.EnvironmentVariable, null);

        try
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(
                    stated is null ? [] : [new(RoleContext.ConfigurationKey, stated)])
                .Build();

            Assert.Equal(expected, RoleContext.Assigned(configuration));
        }
        finally
        {
            Environment.SetEnvironmentVariable(RoleContext.EnvironmentVariable, before);
        }
    }
}
