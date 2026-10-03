using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Gui.Components;
using Xmip.Gui.Pages;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// The Subscriptions view (the owner, 2026-09-30: *a Subscription view, for
/// all subscriptions per cluster, with pause and resume, but not remove. That
/// is handled with the TOML configuration files*; ADR-0013, amendment of the
/// same date). Rendered over a publication of the test cluster whose receiving
/// and sending nodes route by three Subscriptions and whose publisher takes
/// orders: the list,
/// the drill to a node and to one Subscription with its configuration as the
/// file says it, the acts an Operator takes — Pause and Resume, never Remove —
/// the list an Observer is shown without them, and every act in the host's
/// audit as <c>subscription.&lt;act&gt;</c>.
/// </summary>
public sealed class SubscriptionsViewTest : BunitContext, IDisposable
{
    private const string Edi = "[[subscriptions]]\nname = \"edi\"\n"
        + "filter = \"MessageType = 'edi'\"\nsend_port = \"RoundTripOut\"";

    private static readonly TestCluster Names = TestCluster.Read();
    private static readonly string Receiver = Names.WithRole("receiving");
    private static readonly string Sender = Names.WithRole("sending");

    /// <summary>The receiving node's address, escaped for a query.</summary>
    private static readonly string AtReceiver =
        Uri.EscapeDataString($"{Names.Scope}/node/{Receiver}");

    private readonly string _place = Path.Combine(
        Path.GetTempPath(), $"xmip-gui-subscriptions-{Guid.NewGuid():N}");

    public SubscriptionsViewTest()
    {
        Directory.CreateDirectory(_place);
        string snapshot = Path.Combine(_place, $"{Names.Name}-snapshot.toml");
        File.WriteAllText(
            snapshot,
            $"node = \"{Names.Scope}\"\n"
            + $"orders = '{Orders}'\n"
            + Entry(Receiver, "structured", "active", 12, 0)
            + Entry(Receiver, "edi", "paused", 3, 9)
            + Entry(Sender, "flat", "active", 40, 0));
        Services.AddSingleton(ClusterSurfaces.Over(new SnapshotOperator(snapshot)));
        Services.AddSingleton(new ProgramAudit("Xmip.Gui.Test", Audited));
    }

    private string Orders => Path.Combine(_place, "orders");

    private string Audited => Path.Combine(_place, "audit");

    private static string Entry(string node, string name, string state, int picked, int held)
    {
        string configuration = name == "edi"
            ? Edi
            : $"[[subscriptions]]\nname = \"{name}\"";

        return $"[[subscriptions]]\nnode = \"{Names.Scope}/node/{node}\"\nname = \"{name}\"\n"
            + "application = \"RoundTrip\"\n"
            + $"filter = \"MessageType = '{name}'\"\n"
            + "destination = \"the Send Port 'RoundTripOut'\"\n"
            + "file = \"applications/round-trip.xmip.toml\"\n"
            + $"configuration = '''\n{configuration}'''\n"
            + (state == "paused" ? "by = \"ilian\"\n" : string.Empty)
            + $"state = \"{state}\"\npicked_up = {picked}\nheld = {held}\n";
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
    public void TheListSaysEverySubscriptionOfTheClusterAndGroupsTheNodes()
    {
        IRenderedComponent<Subscriptions> page = At("/subscriptions", Role.Observer);

        // By its configured name, the first column, then by node.
        Assert.Equal(["edi", "flat", "structured"], Column(page, "name"));
        Assert.Equal([Names.Name, Names.Name, Names.Name], Column(page, "cluster"));
        Assert.Equal([Receiver, Sender, Receiver], Column(page, "node"));
        Assert.Equal(
            ["MessageType = 'edi'", "MessageType = 'flat'", "MessageType = 'structured'"],
            Column(page, "filter"));
        Assert.All(
            Column(page, "destination"),
            leads => Assert.Equal("the Send Port 'RoundTripOut'", leads));
        Assert.Equal(["paused", "active", "active"], Column(page, "state"));
        Assert.Equal(
            ["subscription ↑", "cluster", "node", "filter", "leads to", "state", "picked up",
                "held", "since"],
            page.FindAll("a.subs-sort").Select(head => head.TextContent.Trim()));
        Assert.Equal(
            [Receiver, Sender],
            page.FindAll("a.subs-group .label").Select(label => label.TextContent.Trim()));
        Assert.Contains("1 paused", page.Find(".subs-count").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDrillStandsAtANode()
    {
        IRenderedComponent<Subscriptions> node =
            At($"/subscriptions?location={AtReceiver}", Role.Observer);

        Assert.Equal(["edi", "structured"], Column(node, "name"));
        Assert.Empty(node.FindAll("a.subs-group"));
        Assert.Equal(
            ["subscriptions", $"cluster {Names.Name}", $"node {Receiver}"],
            node.FindAll(".crumbs .crumb-btn").Select(crumb => crumb.TextContent.Trim()));
    }

    [Fact]
    public void OneSubscriptionShowsItsConfigurationAsTheFileSaysIt()
    {
        IRenderedComponent<Subscriptions> page = At(
            $"/subscriptions?location={AtReceiver}&name=edi", Role.Observer);

        Assert.Contains(
            $"Subscription edi on {Receiver}", page.Find(".subs-detail h2").TextContent,
            StringComparison.Ordinal);
        Assert.Equal(Edi, page.Find("pre.subs-configuration").TextContent.TrimEnd('\n'));
        Assert.Equal("applications/round-trip.xmip.toml", page.Find(".subs-file").TextContent);
        Assert.Equal("ilian", page.Find(".subs-by").TextContent);
        Assert.Contains(
            "RoundTrip", page.Find(".subs-fields").TextContent, StringComparison.Ordinal);
        Assert.Equal(
            ["subscriptions", $"cluster {Names.Name}", $"node {Receiver}", "subscription edi"],
            page.FindAll(".crumbs .crumb-btn").Select(crumb => crumb.TextContent.Trim()));
    }

    [Fact]
    public void AnObserverIsShownTheListAndNoAct()
    {
        IRenderedComponent<Subscriptions> list = At("/subscriptions", Role.Observer);
        Assert.Empty(list.FindAll("button"));

        Services.GetRequiredService<NavigationManager>().NavigateTo(
            $"/subscriptions?location={AtReceiver}&name=edi");
        IRenderedComponent<Subscriptions> one = Render<Subscriptions>();
        Assert.Single(one.FindAll(".subs-detail"));
        Assert.Empty(one.FindAll("button"));
        Assert.False(Directory.Exists(Orders));
    }

    [Fact]
    public void AnOperatorPausesAndResumesAndIsOfferedNoRemove()
    {
        IRenderedComponent<Subscriptions> page = At("/subscriptions", Role.Operator);

        Assert.Equal(
            ["Resume", "Pause", "Pause"],
            page.FindAll("button").Select(button => button.TextContent.Trim()));
        Assert.Equal(SubscriptionActs.ResumeSaid, page.FindAll("button")[0].GetAttribute("title"));
        Assert.Equal(SubscriptionActs.PauseSaid, page.FindAll("button")[1].GetAttribute("title"));
        // Nothing offers to remove one; the view says where that is done.
        Assert.DoesNotContain("remove", page.Markup.Replace(
            SubscriptionOperation.Configured, string.Empty, StringComparison.Ordinal),
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            SubscriptionOperation.Configured, page.Find(".subs-configured").TextContent);

        page.FindAll("button")[2].Click();
        page.FindAll("button")[0].Click();

        Assert.Equal(
            ["subscription structured pause", "subscription edi resume"],
            OrdersLeft.For(Orders, Receiver));
        Assert.Contains(
            $"left for {Receiver}", page.Find(".subs-said").TextContent, StringComparison.Ordinal);

        string audit = File.ReadAllText(Path.Combine(Audited, "audit.toml"));
        Assert.Contains("action = \"subscription.pause\"", audit, StringComparison.Ordinal);
        Assert.Contains("action = \"subscription.resume\"", audit, StringComparison.Ordinal);
        Assert.Contains("\"subscription\" = \"structured\"", audit, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOpenedSubscriptionIsActedOnAndSaysWhatTheActDoes()
    {
        IRenderedComponent<Subscriptions> page = At(
            $"/subscriptions?location={AtReceiver}&name=edi", Role.Operator);

        Assert.Equal("Resume", page.Find(".subs-detail button").TextContent.Trim());
        Assert.Equal(SubscriptionActs.ResumeSaid, page.Find(".subs-act-said").TextContent.Trim());

        page.Find(".subs-detail button").Click();

        Assert.Equal(["subscription edi resume"], OrdersLeft.For(Orders, Receiver));
    }

    [Fact]
    public void AColumnOrdersTheOtherWayWhenAsked()
    {
        IRenderedComponent<Subscriptions> sorted =
            At("/subscriptions?sort=picked-up&order=descending", Role.Observer);

        Assert.Equal(
            ["40", "12", "3"],
            Column(sorted, "figure").Where((_, index) => index % 2 == 0));
    }
}
