using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Gui.Components;
using Xmip.Gui.Pages;
using Xmip.Gui.Surface;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// Retry and Dismiss where a Send Port's last failed Journey is shown
/// (runtime-model.md section 13; ADR-0013): the Monitor's drill at the Port's
/// scope, whose record's evidence names the Journey and why. Rendered over a
/// publication of the test cluster whose sending node published one Port
/// with a failed Journey and one without, and whose publisher takes orders:
/// both acts for an Operator, each left for the sending node; none for an
/// Observer; none at a Port that names no failed Journey; and every act in the
/// host's audit as <c>journey.retry</c> or <c>journey.dismiss</c>.
/// </summary>
public sealed class JourneyActsViewTest : BunitContext, IDisposable
{
    private const string Journey = "01928f6e-7c1a-7d3e-9a4b-5c6d7e8f9a0b";

    private static readonly TestCluster Names = TestCluster.Read();
    private static readonly string Sender = Names.WithRole("sending");
    private static readonly string Failing = $"{Names.Scope}/node/{Sender}/send/invoices";
    private static readonly string Sending = $"{Names.Scope}/node/{Sender}/send/orders";

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
                $"started; tcp at 127.0.0.1:9; sent 4, failed 1, waiting 0; the Journey {Journey} "
                + "failed: every Send Location failed its tries")
            + Record(Sending, "started; tcp at 127.0.0.1:9; sent 4, failed 0, waiting 0"));
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

    private IRenderedComponent<Cluster> At(string scope, Role role)
    {
        Services.AddSingleton(new RoleContext(role));
        Services.GetRequiredService<NavigationManager>().NavigateTo(ScopeLink.Monitor(scope));
        return Render<Cluster>();
    }

    [Fact]
    public void AnOperatorRetriesAndDismissesTheJourneyThePortSaysAndEachIsAudited()
    {
        IRenderedComponent<Cluster> page = At(Failing, Role.Operator);

        Assert.Equal(
            ["Retry", "Dismiss"],
            page.FindAll(".journey-acts button").Select(button => button.TextContent.Trim()));
        Assert.Equal(
            JourneyActs.RetrySaid, page.FindAll(".journey-acts button")[0].GetAttribute("title"));
        Assert.Contains(Journey, page.Find(".journey-acts").TextContent, StringComparison.Ordinal);

        page.FindAll(".journey-acts button")[0].Click();
        page.FindAll(".journey-acts button")[1].Click();

        Assert.Equal(
            [$"journey {Journey} retry", $"journey {Journey} dismiss"],
            OrdersLeft.For(Orders, Sender));
        Assert.Contains(
            $"left for {Sender}", page.Find(".journey-acts .subs-said").TextContent,
            StringComparison.Ordinal);

        string audit = File.ReadAllText(Path.Combine(Audited, "audit.toml"));
        Assert.Contains("action = \"journey.retry\"", audit, StringComparison.Ordinal);
        Assert.Contains("action = \"journey.dismiss\"", audit, StringComparison.Ordinal);
        Assert.Contains($"\"journey\" = \"{Journey}\"", audit, StringComparison.Ordinal);
    }

    [Fact]
    public void AnObserverIsShownTheJourneyAndNoAct()
    {
        IRenderedComponent<Cluster> page = At(Failing, Role.Observer);

        Assert.Contains(Journey, page.Find(".journey-acts").TextContent, StringComparison.Ordinal);
        Assert.Empty(page.FindAll(".journey-acts button"));
        Assert.False(Directory.Exists(Orders));
    }

    [Fact]
    public void APortThatNamesNoFailedJourneyOffersNoAct()
    {
        IRenderedComponent<Cluster> page = At(Sending, Role.Operator);

        Assert.Single(page.FindAll(".drill-leaf"));
        Assert.Empty(page.FindAll(".journey-acts"));
    }
}
