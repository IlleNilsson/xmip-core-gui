using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Abi.Operate;
using Xmip.Gui.Components;
using Xmip.Gui.Pages;
using Xmip.Gui.Surface;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// The Reports view (ADR-0072, amendment 2026-11-10; the owner, the same
/// day: <i>with a little icon saying if they were presenting existing data or
/// probing for data</i>), over the runtime this estate built: every Report in
/// the runtime's list and order, each with its kind's icon and words; one not
/// yet available said so and linked nowhere; a quick one linked to the view
/// that presents it; a Quest's last probe as the time to expect; the levels
/// in the address; and a host with no cluster's configuration saying where a
/// Quest is probed. Which Reports there are is <c>xmip-core-report</c>'s and
/// tested there; an entry is found here by what it holds, never by its name,
/// and every word compared is the runtime's, read through
/// <see cref="ReportCatalog"/>.
/// </summary>
public sealed class ReportsViewTest : BunitContext, IDisposable
{
    private static readonly TestCluster Configured = TestCluster.Read();

    private readonly string _place =
        Directory.CreateTempSubdirectory("xmip-gui-reports-").FullName;

    public ReportsViewTest()
    {
        Services.AddSingleton(new ProgramAudit("Xmip.Gui.Test", _place));
        Services.AddSingleton(new RoleContext(Role.Observer, RoleAssignment.ProcessCaller()));
    }

    void IDisposable.Dispose()
    {
        Directory.Delete(_place, recursive: true);
        base.Dispose();
    }

    private ProgramAudit Audit => Services.GetRequiredService<ProgramAudit>();

    private static string Fixture(string name)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixture", name);
    }

    // The host: one cluster, or two; and the desktop's cluster.xmip, with the
    // node configured here, registered as the desktop registers it, or none,
    // as the web host has.
    private void Hosting(bool desktop, string? node = null, bool two = false)
    {
        SnapshotOperator first = new(Fixture("cluster.xmip"));
        Services.AddSingleton(two
            ? ClusterSurfaces.Over([first, new SnapshotOperator(Fixture("cluster-cl2.xmip"))])
            : ClusterSurfaces.Over(first));

        if (desktop)
        {
            ReportHosting.Add(
                Services, new SliceDelivery(Configured.Path, Path.Combine(_place, "slices"), node));
        }
    }

    private IRenderedComponent<Reports> At(string address)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(address);
        return Render<Reports>();
    }

    private static IElement[] Rows(IRenderedComponent<Reports> page)
    {
        return [.. page.FindAll(".report-entries div.report-entry")];
    }

    // The first Quest built that takes a parameter: the one a probe opens.
    private static ReportEntry Probing(ReportListing listing)
    {
        return listing.Entries.First(entry => entry is
        {
            Kind: ReportKind.Quest, Available: true, Parameter: not null,
        });
    }

    [Fact]
    public void TheLegendShowsBothKindsWithTheirIconsInTheRuntimesWords()
    {
        Hosting(desktop: false);
        ReportListing listing = ReportCatalog.List(Audit);

        IRenderedComponent<Reports> page = At("/reports");

        IElement[] legend = [.. page.FindAll("ul.report-legend li")];
        Assert.Equal(listing.Kinds.Count, legend.Length);
        Assert.Equal(Enum.GetValues<ReportKind>().Length, legend.Length);

        foreach ((ReportKindWords kind, IElement said) in listing.Kinds.Zip(legend))
        {
            Assert.Contains(kind.Name, said.TextContent, StringComparison.Ordinal);
            Assert.Contains(kind.Meaning, said.TextContent, StringComparison.Ordinal);
            Assert.NotNull(said.QuerySelector("svg.report-icon"));
            Assert.Contains(kind.Word, said.QuerySelector(".report-kind")!.ClassList);
        }
    }

    [Fact]
    public void EveryEntryShowsItsKindsWordAndGlyphInTheRuntimesOrder()
    {
        Hosting(desktop: false);
        ReportListing listing = ReportCatalog.List(Audit);

        IElement[] rows = Rows(At("/reports"));

        Assert.Equal(listing.Entries.Select(entry => entry.Name),
            rows.Select(row => row.QuerySelector(".report-name")!.TextContent.Trim()));

        foreach ((ReportEntry entry, IElement row) in listing.Entries.Zip(rows))
        {
            IElement kind = row.QuerySelector(".report-kind")!;
            IElement icon = kind.QuerySelector("svg.report-icon")!;
            string glyph = entry.Kind == ReportKind.ExistingData ? "page" : "probing";
            string word = listing.Kinds.Single(words => words.Kind == entry.Kind).Word;
            // The icon alone; its name is what it says to a reader and on hover.
            Assert.Equal(string.Empty, kind.TextContent.Trim());
            Assert.Equal(entry.Words.Name, icon.GetAttribute("aria-label"));
            Assert.Equal($"{entry.Words.Name}: {entry.Words.Meaning}", kind.GetAttribute("title"));
            Assert.Contains(word, kind.ClassList);
            Assert.Contains(glyph, (icon.GetAttribute("class") ?? string.Empty).Split(' '));
        }
    }

    [Fact]
    public void AnEntryNotYetAvailableSaysSoAndLinksNowhere()
    {
        Hosting(desktop: false);
        ReportListing listing = ReportCatalog.List(Audit);

        IElement[] rows = Rows(At("/reports"));

        Assert.Contains(listing.Entries, entry => !entry.Available);

        foreach ((ReportEntry entry, IElement row) in listing.Entries.Zip(rows))
        {
            if (entry.Available)
            {
                Assert.DoesNotContain("unavailable", row.ClassList);
                continue;
            }

            Assert.Contains("unavailable", row.ClassList);
            Assert.Empty(row.QuerySelectorAll("a"));
            Assert.Equal(entry.Expect, row.QuerySelector(".report-expect")!.TextContent.Trim());
        }
    }

    [Fact]
    public void AQuickEntryOpensAtTheViewThatPresentsItOnTheClusterInView()
    {
        Hosting(desktop: false, two: true);
        string second = ClusterSurfaces.NameOf(new SnapshotOperator(Fixture("cluster-cl2.xmip")));
        ReportListing listing = ReportCatalog.List(Audit);
        ReportEntry audit = listing.Entries.Single(
            entry => entry.ReadBy == OperateAbi.AuditReadEntrypoint);
        string carried = $"cluster={Uri.EscapeDataString(second)}";

        IRenderedComponent<Reports> page = At($"/reports?cluster={second}");

        IElement row = Rows(page)[listing.Entries.ToList().IndexOf(audit)];
        string? href = row.QuerySelector("a.report-name")?.GetAttribute("href");
        Assert.NotNull(href);
        Assert.StartsWith("/audit", href, StringComparison.Ordinal);
        Assert.Contains(carried, href, StringComparison.Ordinal);

        // Opened by name, it is not read a second time here: it links there.
        IRenderedComponent<Reports> one = At(
            ScopeLink.Reports(new Carry(second), report: audit.Name));
        string? presented = one.Find("a.report-presented").GetAttribute("href");
        Assert.StartsWith("/audit", presented, StringComparison.Ordinal);
        Assert.Contains(carried, presented, StringComparison.Ordinal);
        Assert.Empty(one.FindAll(".report-tally"));
    }

    [Fact]
    public void AQuestSaysHowLongItsLastProbeTookOnceProbed()
    {
        Hosting(desktop: true, node: Configured.WithRole("storage"));
        ReportEntry before = Probing(ReportCatalog.List(Audit));
        int place = ReportCatalog.List(Audit).Entries.ToList().FindIndex(
            entry => entry.Name == before.Name);

        Assert.Null(before.TookMilliseconds);
        Assert.Equal(
            before.Expect,
            Rows(At("/reports"))[place].QuerySelector(".report-expect")!.TextContent.Trim());

        At(ScopeLink.Reports(report: before.Name, parameter: before.Parameter!.Choices[0]))
            .Find(".report-tally");

        ReportEntry after = Probing(ReportCatalog.List(Audit));
        Assert.NotNull(after.TookMilliseconds);
        Assert.NotEqual(before.Expect, after.Expect);
        Assert.Equal(
            after.Expect,
            Rows(At("/reports"))[place].QuerySelector(".report-expect")!.TextContent.Trim());
    }

    [Fact]
    public void AnObserverOpensAQuestsThreeLevelsEachInTheAddress()
    {
        Hosting(desktop: true, node: Configured.WithRole("storage"));
        ReportEntry quest = Probing(ReportCatalog.List(Audit));

        // The list links the Report.
        IElement listed = Rows(At("/reports")).Single(
            row => row.QuerySelector(".report-name")!.TextContent.Trim() == quest.Name);
        string first = listed.QuerySelector("a.report-name")!.GetAttribute("href")!;
        Assert.Equal(ScopeLink.Reports(report: quest.Name), first);

        // The Report: its levels, and a link per choice of its parameter.
        IRenderedComponent<Reports> report = At(first);
        Assert.Equal(
            quest.Levels,
            report.FindAll("ol.report-levels li").Select(level => level.TextContent.Trim()));
        string[] choices =
            [.. report.FindAll("a.report-choice").Select(choice => choice.GetAttribute("href")!)];
        Assert.Equal(quest.Parameter!.Choices.Count, choices.Length);
        Assert.All(
            choices, choice => Assert.Contains("parameter=", choice, StringComparison.Ordinal));

        // Probed for a choice: the tally and a row per requirement, each a
        // link to itself in full.
        IRenderedComponent<Reports> probed = At(choices[0]);
        Assert.NotNull(probed.Find(".report-tally"));
        IElement[] links = [.. probed.FindAll(".report-rows a.report-row-name")];
        Assert.NotEmpty(links);
        Assert.All(links, row => Assert.Contains(
            "row=", row.GetAttribute("href"), StringComparison.Ordinal));
        Assert.Equal(3, probed.FindAll("nav.crumbs a.crumb-btn").Count);

        // One requirement in full.
        IRenderedComponent<Reports> one = At(links[0].GetAttribute("href")!);
        Assert.Contains(
            links[0].TextContent.Trim(), one.Find(".report-requirement h2").TextContent,
            StringComparison.Ordinal);
        Assert.NotEmpty(one.Find(".report-requirement .report-says").TextContent.Trim());
        Assert.Empty(one.FindAll(".report-rows"));
        Assert.Equal(4, one.FindAll("nav.crumbs a.crumb-btn").Count);
    }

    [Fact]
    public void ARenderAtTheSameAddressProbesNothingAgainAndAMoveProbesOnce()
    {
        Hosting(desktop: true, node: Configured.WithRole("storage"));
        ReportEntry quest = Probing(ReportCatalog.List(Audit));
        string choice = quest.Parameter!.Choices[0];

        IRenderedComponent<Reports> page = At(
            ScopeLink.Reports(report: quest.Name, parameter: choice));
        string row = page.FindAll(".report-rows a.report-row-name")[0].TextContent.Trim();
        Assert.Equal(1, Probes());

        // A tick of the publication renders the view again at its address.
        page.Render();
        Assert.Equal(1, Probes());

        // A move to another address, on the same page, probes once.
        Services.GetRequiredService<NavigationManager>().NavigateTo(
            ScopeLink.Reports(report: quest.Name, parameter: choice, row: row));
        Assert.Contains(
            row, page.Find(".report-requirement h2").TextContent, StringComparison.Ordinal);
        Assert.Equal(2, Probes());
    }

    // How many probes this host's audit holds: the runtime records each one.
    private int Probes()
    {
        return File.ReadAllLines(Path.Combine(_place, "audit.toml"))
            .Count(line => line.Contains("\"quest.probe\"", StringComparison.Ordinal));
    }

    [Fact]
    public void AClusterFileProbedWithNoNodeIsRefusedAndEachNodeOffered()
    {
        Hosting(desktop: true);
        ReportEntry quest = Probing(ReportCatalog.List(Audit));
        string choice = quest.Parameter!.Choices[0];

        IRenderedComponent<Reports> refused = At(
            ScopeLink.Reports(report: quest.Name, parameter: choice));

        Assert.StartsWith(
            "REFUSED", refused.Find("p.subs-said.refused").TextContent.Trim(),
            StringComparison.Ordinal);
        IElement[] nodes = [.. refused.FindAll("nav.report-nodes a.report-choice")];
        Assert.Equal(
            Configured.Nodes,
            nodes.Select(node => node.TextContent.Trim()).Order(StringComparer.Ordinal));

        string storage = Configured.WithRole("storage");
        IRenderedComponent<Reports> onIt = At(
            nodes.Single(node => node.TextContent.Trim() == storage).GetAttribute("href")!);
        Assert.NotNull(onIt.Find(".report-tally"));
        Assert.Equal(
            storage, onIt.Find("nav.report-nodes a.report-choice.current").TextContent.Trim());
    }

    [Fact]
    public void AHostWithNoClusterConfigurationSaysWhereAQuestIsProbed()
    {
        Hosting(desktop: false);
        ReportEntry quest = Probing(ReportCatalog.List(Audit));

        foreach (string address in (string[])
            [
                ScopeLink.Reports(report: quest.Name),
                ScopeLink.Reports(report: quest.Name, parameter: quest.Parameter!.Choices[0]),
            ])
        {
            IRenderedComponent<Reports> page = At(address);

            Assert.Equal(
                ReportDetail.Where(quest.Name), page.Find("p.report-where").TextContent.Trim());
            Assert.Empty(page.FindAll("a.report-choice"));
            Assert.Empty(page.FindAll(".report-tally"));
        }
    }

    [Fact]
    public void TheTallyAndTheRowsAreHeadedInTheRuntimesWords()
    {
        string node = Configured.WithRole("storage");
        Hosting(desktop: true, node: node);
        ReportEntry quest = Probing(ReportCatalog.List(Audit));
        string choice = quest.Parameter!.Choices[0];
        ReportProbe probe = new(quest.Name, Configured.Path) { Parameter = choice, Node = node };
        ReportProbed probed = ReportCatalog.Open(probe, Audit);

        IRenderedComponent<Reports> page = At(
            ScopeLink.Reports(report: quest.Name, parameter: choice));

        Assert.Equal(
            probed.Tally.Select(count => (count.State, $"{count.Count}")),
            page.FindAll(".report-tally dt.report-count").Select(state => (
                state.TextContent.Trim(), state.NextElementSibling!.TextContent.Trim())));
        Assert.Equal(
            probed.Row,
            page.FindAll(".report-rows .subs-head span")[1].TextContent.Trim());
    }

    [Fact]
    public void ARuntimeThatCarriesNoReportsIsSaidInItsWords()
    {
        // The phone: a remote build, which refuses section 25 in words.
        const string Refusal = "REFUSED: this runtime is a remote build, which has no Reports.";
        Hosting(desktop: false);
        Services.AddSingleton(new ReportHosting(
            () => throw new InvalidOperationException(Refusal), null));

        IRenderedComponent<Reports> page = At("/reports");

        Assert.Equal(
            $"NATIVE — the Reports cannot be read here: {Refusal}",
            page.Find("p.subs-said").TextContent.Trim());
        Assert.Empty(Rows(page));
    }

    [Fact]
    public void ARefusalIsSaidInWords()
    {
        Hosting(desktop: true, node: Configured.WithRole("storage"));
        ReportEntry quest = Probing(ReportCatalog.List(Audit));

        foreach (string address in (string[])
            [
                ScopeLink.Reports(report: "no Report of this name"),
                ScopeLink.Reports(report: quest.Name, parameter: "no choice of this name"),
            ])
        {
            IRenderedComponent<Reports> page = At(address);

            Assert.StartsWith(
                "REFUSED", page.Find("p.subs-said.refused").TextContent.Trim(),
                StringComparison.Ordinal);
            Assert.Empty(page.FindAll(".report-tally"));
        }

        // A name that is none is the runtime's to refuse, naming those there
        // are, in the same sentence every surface says.
        string said = Assert.Throws<InvalidOperationException>(
            () => ReportCatalog.Named(Audit, "no Report of this name")).Message;
        Assert.Equal(
            said,
            At(ScopeLink.Reports(report: "no Report of this name"))
                .Find("p.subs-said.refused").TextContent.Trim());
    }
}
