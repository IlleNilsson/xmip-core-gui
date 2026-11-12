using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Gui.Components;
using Xmip.Gui.Surface;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// One navigation for both hosts: the nine views in the operator's own
/// order, what a host adds after them, and the role described in one set of
/// words. Until 2026-09-27 the web host and the desktop each wrote the bar
/// and worded the Observer differently; since 2026-11-12 an operator
/// arranges the views (ADR-0052, amendment of that date), kept here by the
/// desktop's store, a local TOML file.
/// </summary>
public sealed class TopNavTest : BunitContext
{
    private readonly string folder =
        Path.Combine(Path.GetTempPath(), $"xmip-bar-{Guid.NewGuid():n}");

    private readonly BarOrderFile store;

    public TopNavTest()
    {
        store = new BarOrderFile(Path.Combine(folder, BarOrderFile.Name));
        Services.AddSingleton<IBarOrderStore>(store);
    }

    [Fact]
    public void TheBarLinksTheNineViewsInOrderAndDescribesTheRoleOnce()
    {
        Services.AddSingleton(new RoleContext(Role.Observer, RoleAssignment.ProcessCaller()));

        IRenderedComponent<TopNav> bar = Render<TopNav>();

        // Most wanted first (ADR-0052, amendment 2026-11-12): the Monitor,
        // the Dead Message Queue and the Subscriptions that fix it, the
        // Topology, the Configuration, the Event subscriptions, the Audit,
        // the Reports offered an Observer too (ADR-0072, amendment
        // 2026-11-10), and the Secrets last, each tab by what it is.
        Assert.Equal(
            [
                "/monitor", "/dead-messages", "/subscriptions", "/topology", "/configuration",
                "/event-subscriptions", "/audit", "/reports", "/secrets",
            ],
            bar.FindAll("a.topnav-link").Select(link => link.GetAttribute("href")));
        Assert.Equal(
            [
                "Monitor", "Dead Message Queue", "Subscriptions", "Topology", "Configuration",
                "Event subscriptions", "Audit", "Reports", "Secrets",
            ],
            bar.FindAll("a.topnav-link").Select(link => link.TextContent.Trim()));
        IElement role = bar.Find(".role-pick");
        Assert.Equal("Observer", role.TextContent);
        Assert.Equal(Role.Observer.Describe(), role.GetAttribute("title"));
    }

    [Fact]
    public void AViewDraggedOntoAnotherTakesItsPlaceAndIsKept()
    {
        Services.AddSingleton(new RoleContext(Role.Observer, RoleAssignment.ProcessCaller()));
        IRenderedComponent<TopNav> bar = Render<TopNav>();

        // The Topology dropped on the Monitor: first, and the bare address's.
        bar.FindAll(".topnav-tab")[3].DragStart();
        bar.FindAll(".topnav-tab")[0].Drop();

        Assert.Equal("/topology", Hrefs(bar)[0]);
        Assert.Equal("/monitor", Hrefs(bar)[1]);
        Assert.Equal("/topology", BarOrder.Start(new BarOrderFile(Kept).Order));
        Assert.Contains(
            "order = \"topology,monitor,", File.ReadAllText(Kept), StringComparison.Ordinal);
    }

    [Fact]
    public void ArrangeMovesAViewByTheKeyboardAndResetGoesBackToTheDefault()
    {
        Services.AddSingleton(new RoleContext(Role.Observer, RoleAssignment.ProcessCaller()));
        IRenderedComponent<TopNav> bar = Render<TopNav>();
        Assert.Empty(bar.FindAll(".topnav-move"));

        bar.Find("button.topnav-arrange").Click();
        IElement[] moves = [.. bar.FindAll(".topnav-move")];
        Assert.Equal(2 * BarOrder.Default.Count, moves.Length);
        Assert.True(moves[0].HasAttribute("disabled"), "the first view moves no further left");
        Assert.True(moves[^1].HasAttribute("disabled"), "the last view moves no further right");

        bar.Find("[aria-label='Move Monitor right']").Click();
        Assert.Equal(["/dead-messages", "/monitor"], Hrefs(bar).Take(2));

        bar.FindAll("button.topnav-arrange")[1].Click();
        Assert.Equal(BarOrder.Default.Select(tab => tab.Href), Hrefs(bar));
        Assert.DoesNotContain("order", File.ReadAllText(Kept), StringComparison.Ordinal);
    }

    [Fact]
    public void AHostAddsItsOwnLinksAfterTheViews()
    {
        Services.AddSingleton(new RoleContext(Role.Operator, RoleAssignment.ProcessCaller()));

        IRenderedComponent<TopNav> bar = Render<TopNav>(parameters => parameters
            .AddChildContent("<a class=\"topnav-link\" href=\"/configure\">Configure</a>"));

        Assert.Equal(
            "/configure",
            bar.FindAll("a.topnav-link")[^1].GetAttribute("href"));
        Assert.Equal("also acts and configures", bar.Find(".role-pick").GetAttribute("title"));
    }

    [Fact]
    public void EveryRoleIsDescribed()
    {
        Assert.All(
            Enum.GetValues<Role>(),
            role => Assert.False(string.IsNullOrEmpty(role.Describe()), $"{role}"));
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }

        base.Dispose(disposing);
    }

    private string Kept => Path.Combine(folder, BarOrderFile.Name);

    private static string?[] Hrefs(IRenderedComponent<TopNav> bar)
    {
        return [.. bar.FindAll("a.topnav-link").Select(link => link.GetAttribute("href"))];
    }
}
