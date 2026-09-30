using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Gui.Pages;
using Xmip.Gui.Surface;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// The Audit view (the owner, 2026-09-29: *the operation web needs an audit
/// view: audited entries in the clusters. Drill-down, sorting and
/// filtering*; ADR-0062, amendment of the same date). Rendered over an audit
/// file in the shape the capability writes it: a roll and a node of C1 that
/// declared their locations, a node of C2, and a cmdlet that declared none.
/// </summary>
public sealed class AuditViewTest : BunitContext, IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), $"xmip-gui-audit-{Environment.ProcessId}-{Guid.NewGuid():N}");

    public AuditViewTest()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(
            Path.Combine(_directory, "audit.toml"),
            Record("01", "2026-09-29T10:00:01.000000000Z", "xmip-playground-C1-roll",
                "xmip:///C1", "start", "begin", "information", null)
            + Record("02", "2026-09-29T10:00:02.000000000Z", "xmip-playground-C1-node-alpha",
                "xmip:///C1/node/alpha", "start", "begin", "information", null)
            + Record("03", "2026-09-29T10:00:03.000000000Z", "xmip-playground-C1-node-alpha",
                "xmip:///C1/node/alpha", "publish", "failure", "error", "could not write")
            + Record("04", "2026-09-29T10:00:04.000000000Z", "xmip-playground-C2-node-beta",
                "xmip:///C2/node/beta", "start", "begin", "information", null)
            + Record("05", "2026-09-29T10:00:05.000000000Z", "Xmip", null,
                "Start-XmipTest", "begin", "warning", null));

        string fixture = Path.Combine(AppContext.BaseDirectory, "Fixture", "cluster.toml");
        Services.AddSingleton(ClusterSurfaces.Over(new SnapshotOperator(fixture)));
        Services.AddSingleton(new RoleContext(Role.Observer));
        Services.AddSingleton(new ProgramAudit("Xmip.Gui.Test", _directory));
    }

    private static string Record(
        string id, string at, string program, string? location, string action,
        string phase, string severity, string? message)
    {
        return "[[record]]\n"
            + $"audit_id = \"{id}\"\nat = \"{at}\"\nprogram = \"{program}\"\n"
            + "host = \"edge-01\"\nprocess = \"42\"\n"
            + (location is null ? string.Empty : $"location = \"{location}\"\n")
            + $"action = \"{action}\"\nphase = \"{phase}\"\nseverity = \"{severity}\"\n"
            + (message is null ? string.Empty : $"message = \"{message}\"\n")
            + "[record.properties]\n\"stress\" = \"calm\"\n\n";
    }

    void IDisposable.Dispose()
    {
        Directory.Delete(_directory, recursive: true);
        base.Dispose();
    }

    private IRenderedComponent<Audit> At(string address)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(address);
        return Render<Audit>();
    }

    private static string[] Column(IRenderedComponent<Audit> page, string column)
    {
        return
        [
            .. page.FindAll($"a.audit-row .audit-{column}").Select(cell => cell.TextContent.Trim()),
        ];
    }

    private static string[] Groups(IRenderedComponent<Audit> page)
    {
        return [.. page.FindAll("a.audit-group .label").Select(label => label.TextContent.Trim())];
    }

    [Fact]
    public void TheTopIsEveryClusterAndEveryHostNewestFirst()
    {
        IRenderedComponent<Audit> page = At("/audit");

        Assert.Equal(["C1", "C2", "edge-01"], Groups(page));
        Assert.Equal(["05", "04", "03", "02", "01"], Ids(page));
        Assert.Contains("5 of 5 record(s)", page.Find(".audit-count").TextContent,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AClusterDrillsToItsNodesAndANodeToItsPrograms()
    {
        IRenderedComponent<Audit> cluster = At(
            "/audit?location=" + Uri.EscapeDataString("xmip:///C1"));
        Assert.Equal(["alpha", "xmip-playground-C1-roll"], Groups(cluster));
        Assert.Equal(["alpha", "alpha", "—"], Column(cluster, "node"));
        Assert.Equal(
            ScopeLink.Audit(new AuditQuery { Location = "xmip:///C1/node/alpha" }),
            cluster.Find("a.audit-group").GetAttribute("href"));

        IRenderedComponent<Audit> node = At(
            "/audit?location=" + Uri.EscapeDataString("xmip:///C1/node/alpha"));
        Assert.Equal(["xmip-playground-C1-node-alpha"], Groups(node));
        Assert.Equal(
            ["audit", "cluster C1", "node alpha"],
            node.FindAll(".crumbs .crumb-btn").Select(crumb => crumb.TextContent.Trim()));
    }

    [Fact]
    public void ARecordOpensWholeWithEveryProperty()
    {
        IRenderedComponent<Audit> page = At("/audit?record=03");

        IElement detail = page.Find(".audit-detail");
        Assert.Contains("could not write", detail.TextContent, StringComparison.Ordinal);
        Assert.Contains("xmip:///C1/node/alpha", detail.TextContent, StringComparison.Ordinal);
        Assert.Contains("stress", detail.TextContent, StringComparison.Ordinal);
        Assert.Empty(page.FindAll("a.audit-row"));
    }

    [Fact]
    public void AHeadSortsByItsColumnAndAgainTheOtherWay()
    {
        IRenderedComponent<Audit> page = At("/audit?sort=severity&order=descending");

        Assert.Equal("error", Column(page, "severity")[0]);
        IElement head = page.Find("a.audit-sort.sorted");
        Assert.Equal("descending", head.GetAttribute("aria-sort"));
        Assert.Equal(
            ScopeLink.Audit(new AuditQuery { Sort = "severity", Order = "ascending" }),
            head.GetAttribute("href"));
    }

    [Fact]
    public void ThePatternSeverityAndTimeNarrowAndTheAddressReproducesThem()
    {
        IRenderedComponent<Audit> patterned = At("/audit?pattern=C1%2Fnode%2F*");
        Assert.Equal(["03", "02"], Ids(patterned));

        IRenderedComponent<Audit> severe = At("/audit?severity=error");
        Assert.Equal(["03"], Ids(severe));

        IRenderedComponent<Audit> timed = At(
            "/audit?from=2026-09-29T10:00:02&to=2026-09-29T10:00:04");
        Assert.Equal(["04", "03", "02"], Ids(timed));
    }

    [Fact]
    public void AQueryTheCapabilityRefusesIsSaidInItsWords()
    {
        IRenderedComponent<Audit> page = At("/audit?sort=colour");

        IElement said = page.Find(".audit-said.refused");
        Assert.StartsWith("REFUSED", said.TextContent.Trim(), StringComparison.Ordinal);
    }

    private static string[] Ids(IRenderedComponent<Audit> page)
    {
        return
        [
            .. page.FindAll("a.audit-row").Select(row =>
                (row.GetAttribute("href") ?? string.Empty).Split("record=")[1].Split('&')[0]),
        ];
    }
}
