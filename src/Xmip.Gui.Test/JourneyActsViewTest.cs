using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Gui.Components;
using Xmip.Gui.Pages;
using Xmip.Gui.Surface;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// Every Journey that failed at a Send Port, listed at the Port's scope with
/// Retry and Dismiss on each (runtime-model.md section 13; ADR-0013): the
/// Monitor's drill at the Port, over a publication of the test cluster whose
/// sending node published one Port with two failed Journeys, one whose
/// evidence names a Journey the publication lists none for, and one without,
/// and whose publisher takes orders. Each Journey listed with why and both
/// acts for an Operator, each left for the sending node; the Journeys and no
/// act for an Observer; nothing where the publication lists none, whatever
/// Journey the evidence names; nothing at a Port with no failed Journey; and every act in the host's
/// audit as <c>journey.retry</c> or <c>journey.dismiss</c>, as the caller the
/// browser proved and never the host's account; a caller nothing proved is
/// offered no act and the gate takes none.
/// </summary>
public sealed partial class JourneyActsViewTest : BunitContext, IDisposable
{
    private const string First = "01928f6e-7c1a-7d3e-9a4b-5c6d7e8f9a0b";
    private const string Second = "01928f6e-7c1a-7d3e-9a4b-5c6d7e8f9a0c";
    private const string Named = "01928f6e-7c1a-7d3e-9a4b-5c6d7e8f9a0d";

    // The identity the browser's connection proved, which every act is taken
    // and audited as (ADR-0009, amendment 2026-10-06).
    private const string Proven = "CN=xmip-operator";

    private static readonly TestCluster Names = TestCluster.Read();
    private static readonly string Sender = Names.WithRole("sending");
    private static readonly string Sending = $"{Names.Scope}/node/{Sender}";
    private static readonly string Failing = $"{Sending}/send/invoices";
    private static readonly string Evidenced = $"{Sending}/send/audit";
    private static readonly string Fine = $"{Sending}/send/orders";

    private readonly string _place = Path.Combine(
        Path.GetTempPath(), $"xmip-gui-journey-{Guid.NewGuid():N}");

    public JourneyActsViewTest()
    {
        Directory.CreateDirectory(_place);
        string snapshot = Path.Combine(_place, $"{Names.Name}-snapshot.toml");
        File.WriteAllText(
            snapshot,
            $"node = \"{Names.Scope}\"\n"
            + $"orders = '{Orders}'\n"
            + Record(
                Failing,
                "Send Port; sent 4, failed 2, waiting 0, failed in its queue 2; the Journey "
                + $"{Second} failed: every Send Location failed its tries")
            + Record(
                Evidenced,
                $"Send Port; sent 1, failed 1, waiting 0; the Journey {Named} failed: refused")
            + Record(Fine, "Send Port; sent 4, failed 0, waiting 0, failed in its queue 0")
            + $"\n[[failed_journeys]]\nnode = \"{Sending}\"\nsend_port = \"invoices\"\n"
            + "count = 2\n"
            + $"\n[[failed_journeys.journeys]]\njourney = \"{First}\"\nsequence = 3\n"
            + "reason = \"invoices: the far end refused it\"\n"
            + $"\n[[failed_journeys.journeys]]\njourney = \"{Second}\"\nsequence = 8\n"
            + "reason = \"invoices: every Send Location failed its tries\"\n");
        Services.AddSingleton(ClusterSurfaces.Over(new SnapshotOperator(snapshot)));
        Services.AddSingleton(new ProgramAudit("Xmip.Gui.Test", Audited));
    }

    private string Orders => Path.Combine(_place, "orders");

    private string Audited => Path.Combine(_place, "audit");

    private static string Record(string scope, string evidence)
    {
        return $"[[records]]\nscope = \"{scope}\"\nstate = \"fine\"\nseverity = 0\n"
            + $"evidence = \"{evidence}\"\nobserved_unix_nanos = 1789111688000000000\n";
    }

    void IDisposable.Dispose()
    {
        Directory.Delete(_place, recursive: true);
        base.Dispose();
    }

    private IRenderedComponent<Cluster> At(string scope, Role role, string? who = Proven)
    {
        Services.AddSingleton(new RoleContext(role, who));
        Services.GetRequiredService<NavigationManager>().NavigateTo(ScopeLink.Monitor(scope));
        return Render<Cluster>();
    }

    private static IReadOnlyList<string> Listed(IRenderedComponent<Cluster> page)
    {
        return [.. page.FindAll(".journey-failed").Select(row => row.GetAttribute("data-journey")!)];
    }

    [Fact]
    public void AnOperatorRetriesOrDismissesEachJourneyThatFailedAndEachIsAudited()
    {
        IRenderedComponent<Cluster> page = At(Failing, Role.Operator);

        Assert.Equal([First, Second], Listed(page));
        Assert.Contains(
            "2 Journeys failed", page.Find(".journey-acts").TextContent, StringComparison.Ordinal);
        Assert.Contains(
            "invoices: the far end refused it", page.FindAll(".journey-failed")[0].TextContent,
            StringComparison.Ordinal);
        Assert.Equal(
            ["Retry", "Dismiss", "Retry", "Dismiss"],
            page.FindAll(".journey-failed button").Select(button => button.TextContent.Trim()));
        Assert.Equal(
            JourneyActs.RetrySaid, page.FindAll(".journey-failed button")[0].GetAttribute("title"));

        page.FindAll(".journey-failed")[0].QuerySelectorAll("button")[0].Click();
        page.FindAll(".journey-failed")[1].QuerySelectorAll("button")[1].Click();
        page.Find(".journey-confirm button.dismiss-confirm").Click();

        Assert.Equal(
            [$"journey {First} retry", $"journey {Second} dismiss"],
            OrdersLeft.For(Orders, Sender));
        Assert.Contains(
            $"left for {Sender}", page.Find(".journey-acts .subs-said").TextContent,
            StringComparison.Ordinal);

        string audit = File.ReadAllText(Path.Combine(Audited, "audit.toml"));
        Assert.Contains("action = \"journey.retry\"", audit, StringComparison.Ordinal);
        Assert.Contains("action = \"journey.dismiss\"", audit, StringComparison.Ordinal);
        Assert.Contains($"\"journey\" = \"{First}\"", audit, StringComparison.Ordinal);
        Assert.Contains($"\"journey\" = \"{Second}\"", audit, StringComparison.Ordinal);
        Assert.Equal(
            [Proven],
            WhoSaid().Matches(audit).Select(said => said.Groups[1].Value).Distinct());
        Assert.All(
            Directory.GetFiles(Orders, "*.toml", SearchOption.AllDirectories),
            order => Assert.Contains(Proven, File.ReadAllText(order), StringComparison.Ordinal));
    }

    [Fact]
    public void ACallerNothingProvedIsShownNoActAndTheGateRefusesOneThatArrives()
    {
        IRenderedComponent<Cluster> page = At(Failing, Role.Observer, who: null);

        Assert.Empty(page.FindAll(".journey-acts button"));

        JourneyOperation refused = new GatedOperator(
                Services.GetRequiredService<ClusterSurfaces>().First,
                new RoleContext(Role.Operator, null),
                Services.GetRequiredService<ProgramAudit>())
            .Act(Failing, First, JourneyAct.Retry, Proven);

        Assert.False(refused.Applied);
        Assert.StartsWith("REFUSED. Nothing proved", refused.Result, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Orders));
    }

    [GeneratedRegex("^\"who\" = \"([^\"]*)\"$", RegexOptions.Multiline)]
    private static partial Regex WhoSaid();

    [Fact]
    public void AnObserverIsShownTheJourneysAndNoAct()
    {
        IRenderedComponent<Cluster> page = At(Failing, Role.Observer);

        Assert.Equal([First, Second], Listed(page));
        Assert.Empty(page.FindAll(".journey-acts button"));
        Assert.False(Directory.Exists(Orders));
    }

    [Fact]
    public void APublicationThatListsNoneAtAPortOffersNoActWhateverItsEvidenceNames()
    {
        IRenderedComponent<Cluster> page = At(Evidenced, Role.Operator);

        Assert.Empty(Listed(page));
        Assert.Empty(page.FindAll(".journey-acts"));
        Assert.False(Directory.Exists(Orders));
    }

    [Fact]
    public void APortWithNoFailedJourneyOffersNoAct()
    {
        IRenderedComponent<Cluster> page = At(Fine, Role.Operator);

        Assert.Single(page.FindAll(".drill-leaf"));
        Assert.Empty(page.FindAll(".journey-acts"));
    }
}
