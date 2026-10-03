using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Gui.Pages;
using Xmip.Gui.Surface;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// The "show test clusters" box (the owner, 2026-09-29: *when tests are run
/// include a checkbox if [the test] cluster should be shown or not in the
/// operation tools*; ADR-0052, amendment 2026-09-30). A cluster is hidden
/// because its run declared itself hidden — never because of its name — so
/// the fixture is the test cluster beside a copy of the second cluster
/// fixture, once declared hidden and once not. Off by default; in the
/// address; on every view.
/// </summary>
public sealed class HiddenClusterTest : BunitContext, IDisposable
{
    private static readonly string First = TestCluster.Read().Name;

    /// <summary>The second cluster, as its fixture's publication names it.</summary>
    private static readonly string Second =
        ClusterSurfaces.NameOf(new SnapshotOperator(Fixture("cluster-c2.toml")));

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), $"xmip-gui-hidden-{Environment.ProcessId}-{Guid.NewGuid():N}");

    public HiddenClusterTest()
    {
        Directory.CreateDirectory(_directory);
        Services.AddSingleton(new RoleContext(Role.Observer));
        Services.AddSingleton(new ProgramAudit("Xmip.Gui.Test", _directory));
        File.WriteAllText(
            Path.Combine(_directory, "audit.toml"),
            Record("01", $"{ScopeTree.Root}{First}", hidden: false)
            + Record("02", $"{ScopeTree.Root}{Second}", hidden: true));
    }

    void IDisposable.Dispose()
    {
        Directory.Delete(_directory, recursive: true);
        base.Dispose();
    }

    private static string Fixture(string name)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixture", name);
    }

    // The second cluster's fixture, its run declaring itself hidden or not.
    private string Published(bool hidden)
    {
        string text = File.ReadAllText(Fixture("cluster-c2.toml"));

        if (hidden)
        {
            text = text.Replace("\n[run]\n", "\n[run]\nhidden = true\n", StringComparison.Ordinal);
        }

        string path = Path.Combine(_directory, $"{Second}-{hidden}.toml");
        File.WriteAllText(path, text);

        return path;
    }

    private static string Record(string id, string location, bool hidden)
    {
        return "[[record]]\n"
            + $"audit_id = \"{id}\"\nat = \"2026-09-30T10:00:0{id[1]}.000000000Z\"\n"
            + $"program = \"probe\"\nhost = \"{Environment.MachineName.ToLowerInvariant()}\"\n"
            + "process = \"42\"\n"
            + $"location = \"{location}\"\n"
            + (hidden ? "hidden = \"true\"\n" : string.Empty)
            + "action = \"start\"\nphase = \"begin\"\nseverity = \"information\"\n\n";
    }

    private void Holding(bool hidden)
    {
        Services.AddSingleton(ClusterSurfaces.Over(
            [
                new SnapshotOperator(Fixture("cluster.toml")),
                new SnapshotOperator(Published(hidden)),
            ]));
    }

    private IRenderedComponent<TPage> At<TPage>(string address)
        where TPage : IComponent
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(address);
        return Render<TPage>();
    }

    private static string[] Picked(IRenderedComponent<IComponent> page)
    {
        return [.. page.FindAll("a.cluster-pick").Select(pick => pick.TextContent.Trim())];
    }

    private static IElement Box(IRenderedComponent<IComponent> page)
    {
        return page.Find("p.run-line label.run-hidden input[type=checkbox]");
    }

    [Fact]
    public void AHiddenClusterIsNotListedAndTheBoxIsOffOnEveryView()
    {
        Holding(hidden: true);

        foreach (IRenderedComponent<IComponent> page in (IRenderedComponent<IComponent>[])
            [At<Cluster>("/"), At<Configuration>("/configuration"), At<Topology>("/topology"),
             At<Subscriptions>("/subscriptions"),
             At<EventSubscriptions>("/event-subscriptions"), At<Audit>("/audit")])
        {
            Assert.Empty(Picked(page));
            Assert.False(Box(page).HasAttribute("checked"));
            Assert.DoesNotContain(
                Second, page.Find("p.run-line").TextContent, StringComparison.Ordinal);
        }

        // Asked for by name, the hidden one is not reached: the view is on the
        // first.
        IRenderedComponent<Cluster> asked = At<Cluster>($"/?cluster={Second}");
        Assert.Contains(
            First, asked.Find("p.run-line .run-said").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void TickedTheHiddenClusterIsListedMarkedTestAndEveryLinkCarriesTheBox()
    {
        Holding(hidden: true);

        IRenderedComponent<Cluster> page = At<Cluster>("/?hidden=include");

        Assert.Equal([First, $"{Second} · test"], Picked(page));
        Assert.Contains("test", page.FindAll("a.cluster-pick")[1].ClassList);
        Assert.True(Box(page).HasAttribute("checked"));
        Assert.All(
            page.FindAll("a.cluster-pick"),
            pick => Assert.Contains("hidden=include", pick.GetAttribute("href"),
                StringComparison.Ordinal));
        Assert.All(
            page.FindAll("section.drill a.crumb-link"),
            link => Assert.Contains("hidden=include", link.GetAttribute("href"),
                StringComparison.Ordinal));

        IRenderedComponent<Cluster> onIt = At<Cluster>($"/?cluster={Second}&hidden=include");
        Assert.Contains(
            "hidden test run", onIt.Find("p.run-line .run-said").TextContent,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AClusterWhoseRunDeclaredNothingIsShownLikeAnyOther()
    {
        Holding(hidden: false);

        IRenderedComponent<Cluster> page = At<Cluster>("/");

        Assert.Equal([First, Second], Picked(page));
        Assert.Empty(page.FindAll("a.cluster-pick.test"));
    }

    [Fact]
    public void TheAuditOfAHiddenRunIsLeftOutWithItAndMarkedTestWhenShown()
    {
        Holding(hidden: true);

        IRenderedComponent<Audit> off = At<Audit>("/audit");
        string[] groups =
            [.. off.FindAll("a.audit-group .label").Select(label => label.TextContent)];
        Assert.Equal([First], groups);
        Assert.Contains("1 of 1 record(s)", off.Find(".audit-count").TextContent,
            StringComparison.Ordinal);

        IRenderedComponent<Audit> on = At<Audit>("/audit?hidden=include");
        groups = [.. on.FindAll("a.audit-group .label").Select(label => label.TextContent)];
        Assert.Equal([First, $"{Second} · test"], groups);
        Assert.Single(on.FindAll("a.audit-row.test"));
        Assert.All(
            on.FindAll("a.audit-group"),
            group => Assert.Contains("hidden=include", group.GetAttribute("href"),
                StringComparison.Ordinal));
    }

    [Fact]
    public void TickingTheBoxKeepsTheRestOfTheAddress()
    {
        Assert.Equal(
            "/audit?severity=error&hidden=include",
            ScopeLink.Hidden("audit?severity=error", includeHidden: true));
        Assert.Equal(
            "/audit?severity=error",
            ScopeLink.Hidden("audit?severity=error&hidden=include", includeHidden: false));
        Assert.Equal(
            "/topology", ScopeLink.Hidden("topology?hidden=include", includeHidden: false));
    }
}
