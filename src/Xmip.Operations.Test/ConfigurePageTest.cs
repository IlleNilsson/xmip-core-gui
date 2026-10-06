using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Operations.Configuration;
using Xmip.Operations.Test.Components.Pages;
using Xmip.Surface;

namespace Xmip.Operations.Test;

/// <summary>
/// The Configure page as an operator meets it, rendered over the runtime this
/// estate built: edits not saved are never lost by leaving — the page holds
/// the way out and asks save, discard or stay — Discard changes drops them,
/// a save over another editor's change is refused and offers a reload, the
/// node configured here is planned and never said to start, and every
/// control is named for what it edits.
/// </summary>
public sealed class ConfigurePageTest : BunitContext, IDisposable
{
    private const string Elsewhere = "http://localhost/elsewhere";

    private readonly string _directory =
        Directory.CreateTempSubdirectory("xmip-configure-page-").FullName;

    private readonly NativeOperator _runtime = new(Estate.Library());

    private readonly string _path;

    public ConfigurePageTest()
    {
        _path = Estate.Written(_directory).Path;
        Services.AddSingleton(new RuntimeCommands(
            _runtime,
            _runtime.Path,
            new ProgramAudit("xmip-operations", Path.Combine(_directory, "audit")),
            new SliceDelivery(
                _path, Path.Combine(_directory, "slices"), Estate.Cluster.Nodes[0])));
        Services.AddSingleton(new RoleContext(Role.Operator, "tester"));
    }

    private NavigationManager Nav => Services.GetRequiredService<NavigationManager>();

    [Fact]
    public void LeavingWithEditsNotSavedIsHeldAndStayKeepsThem()
    {
        IRenderedComponent<Configure> page = Edited();

        Leave(page);

        Assert.NotEqual(Elsewhere, Nav.Uri);
        IElement asked = page.Find(".leaving");
        Assert.Contains("not saved", asked.TextContent, StringComparison.Ordinal);

        page.Find(".leaving button.stay").Click();

        Assert.Empty(page.FindAll(".leaving"));
        Assert.NotEmpty(page.FindAll(".changed"));
        Assert.NotEqual(Elsewhere, Nav.Uri);
    }

    [Fact]
    public void DiscardAndLeaveDropsTheEditsAndGoes()
    {
        string before = File.ReadAllText(_path);
        IRenderedComponent<Configure> page = Edited();

        Leave(page);
        page.Find(".leaving button.discard").Click();

        Assert.Equal(Elsewhere, Nav.Uri);
        Assert.Equal(before, File.ReadAllText(_path));
    }

    [Fact]
    public void SaveAndLeaveWritesTheEditsAndGoes()
    {
        IRenderedComponent<Configure> page = Edited();

        Leave(page);
        page.Find(".leaving button.primary").Click();

        Assert.Equal(Elsewhere, Nav.Uri);
        Assert.Contains("edited-here", File.ReadAllText(_path), StringComparison.Ordinal);
    }

    [Fact]
    public void LeavingWithNothingChangedIsNotHeld()
    {
        IRenderedComponent<Configure> page = Render<Configure>();

        Leave(page);

        Assert.Equal(Elsewhere, Nav.Uri);
        Assert.Empty(page.FindAll(".leaving"));
    }

    [Fact]
    public void DiscardChangesIsOfferedOnlyWhileThereAreEditsAndDropsThem()
    {
        IRenderedComponent<Configure> page = Render<Configure>();
        Assert.True(page.Find(".path button.discard").HasAttribute("disabled"));

        page = Edited();
        IElement discard = page.Find(".path button.discard");
        Assert.False(discard.HasAttribute("disabled"));

        discard.Click();

        Assert.Empty(page.FindAll(".changed"));
        Assert.DoesNotContain(
            "edited-here", page.Find("input[aria-label='name value']").GetAttribute("value"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ASaveOverAnotherEditorsChangeIsRefusedAndOffersAReload()
    {
        IRenderedComponent<Configure> page = Edited();
        File.AppendAllText(_path, "# saved by another editor\n");

        page.Find(".path button.primary").Click();

        Assert.Contains("changed on disk", page.Find(".status").TextContent, StringComparison.Ordinal);
        Assert.Contains("another editor", File.ReadAllText(_path), StringComparison.Ordinal);
        Assert.DoesNotContain("edited-here", File.ReadAllText(_path), StringComparison.Ordinal);

        page.Find(".path button.reload").Click();

        Assert.Empty(page.FindAll(".changed"));
        Assert.Empty(page.FindAll(".path button.reload"));
    }

    [Fact]
    public void TheNodeConfiguredHereIsPlannedNeverSaidToStart()
    {
        IRenderedComponent<Configure> page = Render<Configure>();
        string node = Estate.Cluster.Nodes[0];

        Assert.Equal($"Plan {node}", page.Find(".path button.plan").TextContent.Trim());
        Assert.Equal("configured here", page.Find(".configs .badge").TextContent.Trim());
        Assert.DoesNotContain("Start", page.Find(".path").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("started", page.Markup, StringComparison.Ordinal);

        page.Find(".path button.plan").Click();

        Assert.DoesNotContain("started", page.Find(".status").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryControlIsNamedForWhatItEdits()
    {
        IRenderedComponent<Configure> page = Render<Configure>();
        page.Find(".configs .config-item:not(.undefined)").Click();

        IReadOnlyList<IElement> controls = page.FindAll(".cfg input, .cfg select, .cfg textarea");
        Assert.NotEmpty(controls);
        Assert.All(controls, control => Assert.False(
            string.IsNullOrWhiteSpace(control.GetAttribute("aria-label")), control.OuterHtml));

        // A value's control says whose value it is; a remove says what it removes.
        Assert.NotEmpty(page.FindAll("input[aria-label='name value']"));
        IReadOnlyList<IElement> removes = page.FindAll("button.del");
        Assert.NotEmpty(removes);
        Assert.All(removes, remove => Assert.StartsWith(
            "Remove ", remove.GetAttribute("aria-label") ?? string.Empty, StringComparison.Ordinal));
    }

    // The page with the cluster's name edited and not saved.
    private IRenderedComponent<Configure> Edited()
    {
        IRenderedComponent<Configure> page = Render<Configure>();
        page.Find("input[aria-label='name value']").Change("\"edited-here\"");
        Assert.NotEmpty(page.FindAll(".changed"));
        return page;
    }

    private void Leave(IRenderedComponent<Configure> page)
    {
        page.InvokeAsync(() => Nav.NavigateTo(Elsewhere)).GetAwaiter().GetResult();
    }

    void IDisposable.Dispose()
    {
        base.Dispose();
        _runtime.Dispose();
        Directory.Delete(_directory, recursive: true);
    }
}
