using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Gui.Components;
using Xmip.Gui.Surface;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// One navigation for both hosts: the nine views in the operator's own
/// order and what a host adds after them; every role is described in one
/// set of words, which the status bar shows since 2026-11-25
/// (<see cref="StatusBarTest"/>). Until 2026-09-27 the web host and the
/// desktop each wrote the bar and worded the Observer differently; since
/// 2026-11-12 an operator
/// arranges the views (ADR-0052, amendment of that date), kept here by the
/// desktop's store, a local TOML file; and since 2026-11-25 the bar's cog
/// opens the operator's Settings, where the theme is chosen (ADR-0052,
/// amendment of that date).
/// </summary>
public sealed class TopNavTest : BunitContext
{
    // The attribute bUnit marks a referenced element with.
    private const string Reference = "blazor:elementreference";

    // Made here; the store's own folder beneath it is the store's to make.
    private readonly TestFolder folder = new("bar");

    private readonly PreferenceFile store;

    public TopNavTest()
    {
        store = new PreferenceFile(Kept);
        Services.AddSingleton<IPreferenceStore>(store);
    }

    [Fact]
    public void TheBarLinksTheNineViewsInOrder()
    {
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
    }

    [Fact]
    public void AViewDraggedOntoAnotherTakesItsPlaceAndIsKept()
    {
        IRenderedComponent<TopNav> bar = Render<TopNav>();

        // The Topology dropped on the Monitor: first, and the bare address's.
        bar.FindAll(".topnav-tab")[3].DragStart();
        bar.FindAll(".topnav-tab")[0].Drop();

        Assert.Equal("/topology", Hrefs(bar)[0]);
        Assert.Equal("/monitor", Hrefs(bar)[1]);
        Assert.Equal("/topology", BarOrder.Start(new PreferenceFile(Kept).Order));
        Assert.Contains(
            "order = \"topology,monitor,", File.ReadAllText(Kept), StringComparison.Ordinal);
    }

    [Fact]
    public void ArrangeMovesAViewByTheKeyboardAndResetGoesBackToTheDefault()
    {
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
    public void TheCogOpensSettingsWhereTheThemeIsChosenAndKept()
    {
        IRenderedComponent<Themed> page = Render<Themed>(parameters => parameters
            .AddChildContent<TopNav>());
        Assert.Equal("system", page.Find(".themed").GetAttribute("data-theme"));

        // Until 2026-11-25 the theme was a button on the bar; now the bar
        // holds a cog named Settings, closed (ADR-0052, amendment 2026-11-25).
        IElement cog = page.Find("nav.topnav button.settings-cog");
        Assert.Equal("Settings", cog.GetAttribute("aria-label"));
        Assert.Equal("false", cog.GetAttribute("aria-expanded"));
        Assert.Empty(page.FindAll(".settings-panel"));
        Assert.DoesNotContain(
            page.FindAll("nav.topnav button"),
            button => Themes.Contains(button.TextContent.Trim()));

        cog.Click();
        cog = page.Find("button.settings-cog");
        Assert.Equal("true", cog.GetAttribute("aria-expanded"));
        Assert.Equal(page.Find(".settings-panel").Id, cog.GetAttribute("aria-controls"));
        IElement row = page.Find(".settings-panel .settings-row");
        Assert.Equal("Theme", page.Find($"#{row.GetAttribute("aria-labelledby")}").TextContent);
        Assert.Equal(Themes, Choices(page).Select(choice => choice.TextContent.Trim()));
        Assert.Equal(["true", "false", "false"], Pressed(page));

        // The theme in force is not kept again.
        Choices(page)[0].Click();
        Assert.False(File.Exists(Kept), "the system's theme, chosen again, writes nothing");

        Choices(page)[1].Click();
        Assert.Equal("light", page.Find(".themed").GetAttribute("data-theme"));
        Choices(page)[2].Click();

        page.WaitForAssertion(() =>
            Assert.Equal("dark", page.Find(".themed").GetAttribute("data-theme")));
        Assert.Equal(["false", "false", "true"], Pressed(page));
        Assert.Contains("choice = \"dark\"", File.ReadAllText(Kept), StringComparison.Ordinal);
        Assert.Equal(Theme.Dark, new PreferenceFile(Kept).Theme);
    }

    [Fact]
    public void SettingsCloseByTheCogByEscapeAndByAPressOutside()
    {
        IRenderedComponent<TopNav> bar = Render<TopNav>();
        // The cog's reference, as the first render marks it: a later render
        // keeps the element and no longer marks it.
        string? reference = bar.Find("button.settings-cog").GetAttribute(Reference);
        Assert.False(string.IsNullOrEmpty(reference), "the cog is referenced");

        bar.Find("button.settings-cog").Click();
        bar.Find("button.settings-cog").Click();
        Assert.Empty(bar.FindAll(".settings-panel"));

        // Escape from within the panel closes it, and focus goes back to the
        // cog, the element the operator opened it from.
        bar.Find("button.settings-cog").Click();
        bar.Find(".settings-choice").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.Empty(bar.FindAll(".settings-panel"));
        Assert.Equal("false", bar.Find("button.settings-cog").GetAttribute("aria-expanded"));
        ElementReference focused =
            (ElementReference)JSInterop.VerifyFocusAsyncInvoke().Arguments[0]!;
        Assert.Equal(reference, focused.Id);

        bar.Find("button.settings-cog").Click();
        bar.Find(".settings-backdrop").Click();
        Assert.Empty(bar.FindAll(".settings-panel"));
    }

    [Fact]
    public void AHostAddsItsOwnLinksAfterTheViews()
    {
        IRenderedComponent<TopNav> bar = Render<TopNav>(parameters => parameters
            .AddChildContent("<a class=\"topnav-link\" href=\"/configure\">Configure</a>"));

        Assert.Equal(
            "/configure",
            bar.FindAll("a.topnav-link")[^1].GetAttribute("href"));
    }

    [Fact]
    public void EveryRoleIsDescribed()
    {
        Assert.All(
            Enum.GetValues<Role>(),
            role => Assert.False(string.IsNullOrEmpty(role.Describe()), $"{role}"));
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        await base.DisposeAsyncCore();
        folder.Dispose();
    }

    private string Kept => Path.Combine(folder.Path, "desktop", PreferenceFile.Name);

    // The themes Settings offers, by the names the rule gives them.
    private static string[] Themes => [.. Enum.GetValues<Theme>().Select(ThemeChoice.Name)];

    private static string?[] Hrefs(IRenderedComponent<TopNav> bar)
    {
        return [.. bar.FindAll("a.topnav-link").Select(link => link.GetAttribute("href"))];
    }

    private static IElement[] Choices(IRenderedComponent<Themed> page)
    {
        return [.. page.FindAll(".settings-panel .settings-choice")];
    }

    private static string?[] Pressed(IRenderedComponent<Themed> page)
    {
        return [.. Choices(page).Select(choice => choice.GetAttribute("aria-pressed"))];
    }
}
