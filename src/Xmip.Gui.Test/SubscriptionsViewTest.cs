using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Gui.Pages;
using Xmip.Gui.Surface;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// The Subscriptions view (the owner, 2026-09-29: *operation needs a view of
/// event subscriptions. Subscriber, Cluster, Node, Action. One should be able
/// to pause, resume and remove event subscriptions*; ADR-0065, amendment of
/// the same date). Rendered over a publication of cluster CT whose nodes R1
/// and S1 hold three subscriptions and whose publisher takes orders: the
/// list, the drill, the order, the acts an Operator takes and the list an
/// Observer is shown without them — and every act in the host's audit.
/// </summary>
public sealed class SubscriptionsViewTest : BunitContext, IDisposable
{
    private readonly string _place = Path.Combine(
        Path.GetTempPath(), $"xmip-gui-subscriptions-{Guid.NewGuid():N}");

    public SubscriptionsViewTest()
    {
        Directory.CreateDirectory(_place);
        string snapshot = Path.Combine(_place, "CT-snapshot.toml");
        File.WriteAllText(
            snapshot,
            "node = \"xmip:///CT\"\n"
            + $"orders = '{Orders}'\n"
            + Entry("R1", 1, "every Event", "active", 3)
            + Entry("R1", 2, "every Event ending failure", "paused", 9)
            + Entry("S1", 1, "every Event", "active", 0));
        Services.AddSingleton(ClusterSurfaces.Over(new SnapshotOperator(snapshot)));
        Services.AddSingleton(new ProgramAudit("Xmip.Gui.Test", Audited));
    }

    private string Orders => Path.Combine(_place, "orders");

    private string Audited => Path.Combine(_place, "audit");

    private static string Entry(string node, int id, string action, string state, int queued)
    {
        return $"[[subscriptions]]\nnode = \"xmip:///CT/node/{node}\"\nid = {id}\n"
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

    private IRenderedComponent<Subscriptions> At(string address, Role role)
    {
        Services.AddSingleton(new RoleContext(role));
        Services.GetRequiredService<NavigationManager>().NavigateTo(address);
        return Render<Subscriptions>();
    }

    private static string[] Column(IRenderedComponent<Subscriptions> page, string column)
    {
        return
        [
            .. page.FindAll($"div.subs-row:not(.subs-head) .subs-{column}")
                .Select(cell => cell.TextContent.Trim()),
        ];
    }

    [Fact]
    public void TheListSaysSubscriberClusterNodeAndActionAndGroupsTheNodes()
    {
        IRenderedComponent<Subscriptions> page = At("/event-subscriptions", Role.Observer);

        Assert.Equal(["CT", "CT", "CT"], Column(page, "cluster"));
        // By subscriber, the first column — the name its Party was declared
        // with, never its identifier — and then by node and number.
        Assert.Equal(["on-call", "operations", "operations"], Column(page, "subscriber"));
        Assert.Equal(["R1", "R1", "S1"], Column(page, "node"));
        Assert.Equal(
            ["every Event ending failure", "every Event", "every Event"], Column(page, "action"));
        Assert.Equal(["paused", "active", "active"], Column(page, "state"));
        Assert.Equal(
            ["R1", "S1"],
            page.FindAll("a.subs-group .label").Select(label => label.TextContent.Trim()));
        Assert.Contains("1 paused", page.Find(".subs-count").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void AnObserverIsShownTheListAndNoAct()
    {
        IRenderedComponent<Subscriptions> page =
            At("/event-subscriptions?location=xmip%3A%2F%2F%2FCT%2Fnode%2FR1&id=2", Role.Observer);

        Assert.Contains("subscription 2 on R1", page.Find(".subs-detail h2").TextContent,
            StringComparison.Ordinal);
        string detail = page.Find(".subs-fields").TextContent;
        Assert.Contains("on-call", detail, StringComparison.Ordinal);
        Assert.Contains(
            "0199a0a0-0000-7000-8000-000000000002", detail, StringComparison.Ordinal);
        Assert.Empty(page.FindAll("button.act"));
        Assert.False(Directory.Exists(Orders));
    }

    [Fact]
    public void AnOperatorPausesResumesAndRemovesAndEveryActIsAudited()
    {
        IRenderedComponent<Subscriptions> page = At("/event-subscriptions", Role.Operator);

        Assert.Equal(
            ["Resume", "Remove", "Pause", "Remove", "Pause", "Remove"],
            page.FindAll("button.act").Select(button => button.TextContent.Trim()));

        page.FindAll("button.act")[2].Click();
        page.FindAll("button.act")[0].Click();
        page.FindAll("button.act")[5].Click();

        string[] left = [.. Directory.GetFiles(Path.Combine(Orders, "R1"), "*.toml")
            .Concat(Directory.GetFiles(Path.Combine(Orders, "S1"), "*.toml"))
            .Select(Path.GetFileName)
            .Select(name => name![(name!.IndexOf('-', StringComparison.Ordinal) + 1)..])];
        Assert.Equal(["1-pause.toml", "2-resume.toml", "1-remove.toml"], left);
        Assert.Contains(
            "left for S1", page.Find(".subs-said").TextContent, StringComparison.Ordinal);

        string audit = File.ReadAllText(Path.Combine(Audited, "audit.toml"));
        foreach (string act in new[] { "pause", "resume", "remove" })
        {
            Assert.Contains($"action = \"subscription.{act}\"", audit, StringComparison.Ordinal);
        }

        Assert.Contains("\"subscriber\" = \"operations\"", audit, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDrillStandsAtANode()
    {
        IRenderedComponent<Subscriptions> node =
            At("/event-subscriptions?location=xmip%3A%2F%2F%2FCT%2Fnode%2FR1", Role.Observer);

        Assert.Equal(["R1", "R1"], Column(node, "node"));
        Assert.Empty(node.FindAll("a.subs-group"));
        Assert.Equal(
            ["event subscriptions", "cluster CT", "node R1"],
            node.FindAll(".crumbs .crumb-btn").Select(crumb => crumb.TextContent.Trim()));
    }

    [Fact]
    public void AColumnOrdersTheOtherWayWhenAsked()
    {
        IRenderedComponent<Subscriptions> sorted =
            At("/event-subscriptions?sort=queued&order=descending", Role.Observer);
        Assert.Equal(["9 of 64", "3 of 64", "0 of 64"], Column(sorted, "figure").Where(
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
