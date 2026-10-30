using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Gui.Components;
using Xmip.Gui.Pages;
using Xmip.Gui.Surface;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// The Secrets view (ADR-0014, amendment 2026-10-30; ADR-0019, amendment
/// 2026-10-29), over a surface the test composes: the secrets a node keeps by
/// name, kind, when — where the person is, the UTC in the title — and by
/// whom, and never a value; a secret set from a password field and from a
/// chosen file, through the one gate, audited by its name and never its value,
/// and the value held by nothing on the page once handed on; an Observer told
/// the listing is an Operator's and shown neither it nor the act; and a
/// surface that reaches no node said in words.
/// </summary>
public sealed class SecretsViewTest : BunitContext, IDisposable
{
    private const string Value = "a-value-never-shown-4711";

    private static readonly DateTimeOffset Then = new(2026, 10, 30, 12, 5, 0, TimeSpan.Zero);

    private readonly string _place =
        Path.Combine(Path.GetTempPath(), $"xmip-gui-secrets-{Guid.NewGuid():N}");

    private readonly ScriptedSurface _surface = new()
    {
        Kept = [new SecretRecord("partner-x-sftp", "key", Then, "CN=operator")],
    };

    public SecretsViewTest()
    {
        Directory.CreateDirectory(_place);
        Services.AddSingleton(ClusterSurfaces.Over(_surface));
        Services.AddSingleton(new ProgramAudit("Xmip.Gui.Test", _place));
    }

    void IDisposable.Dispose()
    {
        Directory.Delete(_place, recursive: true);
        base.Dispose();
    }

    private string Audit => File.ReadAllText(Path.Combine(_place, "audit.toml"));

    private IRenderedComponent<Secrets> At(string address, Role role)
    {
        Services.AddSingleton(new RoleContext(role, "tester"));
        Services.GetRequiredService<NavigationManager>().NavigateTo(address);
        return Render<Secrets>();
    }

    private static string[] Column(IRenderedComponent<Secrets> page, string column)
    {
        return
        [
            .. page.FindAll($".secret-records div.subs-row:not(.subs-head) .secret-{column}")
                .Select(cell => cell.TextContent.Trim()),
        ];
    }

    [Fact]
    public void EachSecretIsListedByNameKindWhenAndWhoAndNeverByValue()
    {
        IRenderedComponent<Secrets> page = At("/secrets", Role.Operator);

        Assert.Equal(["partner-x-sftp"], Column(page, "name"));
        Assert.Equal(["key"], Column(page, "kind"));
        Assert.Equal(["CN=operator"], Column(page, "by"));
        Assert.Equal([PersonTime.Said(Then)], Column(page, "at"));
        Assert.Equal("2026-10-30 12:05:00Z", page.Find(".secret-at").GetAttribute("title"));
        Assert.Equal(
            ["name", "kind", "set", "set by"],
            page.FindAll(".secret-records .subs-head span").Select(head => head.TextContent));
        Assert.Equal([string.Empty], _surface.Listings);
        Assert.Equal(Secrets.Named, page.Find(".subs-configured").TextContent);
    }

    [Fact]
    public void ATimeIsSaidWhereThePersonIsWithItsOffset()
    {
        TimeZoneInfo east = TimeZoneInfo.CreateCustomTimeZone(
            "east", TimeSpan.FromHours(2), "east", "east");

        Assert.Equal(
            "10/30/2026 14:05:00 UTC+02:00",
            PersonTime.Said(Then, east, CultureInfo.InvariantCulture));
        Assert.Equal(
            "10/30/2026 12:05:00 UTC",
            PersonTime.Said(Then, TimeZoneInfo.Utc, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void AnOperatorSetsOneFromThePasswordFieldAndNothingKeepsTheValue()
    {
        IRenderedComponent<Secrets> page = At("/secrets", Role.Operator);

        Assert.True(page.Find(".secret-set button").HasAttribute("disabled"));
        page.Find("input.secret-set-name").Input("partner-x-https");
        page.Find("select.secret-set-kind").Change("password");
        Assert.Equal("password", page.Find("input.secret-set-value").GetAttribute("type"));
        page.Find("input.secret-set-value").Input(Value);
        page.Find(".secret-set button").Click();

        Assert.Equal([$" partner-x-https password {Value} tester"], _surface.SecretsSet);
        Assert.Equal(["partner-x-https", "partner-x-sftp"], Column(page, "name").Order());
        Assert.Contains(
            "the secret 'partner-x-https', a password, is set",
            page.Find(".subs-said").TextContent,
            StringComparison.Ordinal);

        // Never echoed, never kept: the field is empty again and the value is
        // nowhere on the page or in the audit, which names the secret.
        Assert.DoesNotContain(Value, page.Markup, StringComparison.Ordinal);
        Assert.True(
            string.IsNullOrEmpty(page.Find("input.secret-set-value").GetAttribute("value")));
        Assert.Equal("partner-x-https", page.Find("input.secret-set-name").GetAttribute("value"));
        Assert.Contains("action = \"secret.set\"", Audit, StringComparison.Ordinal);
        Assert.Contains("\"secret\" = \"partner-x-https\"", Audit, StringComparison.Ordinal);
        Assert.DoesNotContain(Value, Audit, StringComparison.Ordinal);
    }

    [Fact]
    public void AValueIsReadFromAChosenFile()
    {
        const string Pem = "-----BEGIN OPENSSH PRIVATE KEY-----\nAAAA\n"
            + "-----END OPENSSH PRIVATE KEY-----\n";
        IRenderedComponent<Secrets> page = At("/secrets", Role.Operator);

        page.Find("input.secret-set-name").Input("partner-x-sftp");
        page.Find("select.secret-set-from").Change("file");
        Assert.Empty(page.FindAll("input.secret-set-value"));
        page.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText(Pem, "partner-x.key"));
        page.Find(".secret-set button").Click();

        Assert.Equal([$" partner-x-sftp password {Pem} tester"], _surface.SecretsSet);
        Assert.DoesNotContain("BEGIN OPENSSH", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("BEGIN OPENSSH", Audit, StringComparison.Ordinal);
        Assert.True(page.Find(".secret-set button").HasAttribute("disabled"));
    }

    [Fact]
    public void AnObserverIsShownNoSecretAndNoAct()
    {
        IRenderedComponent<Secrets> page = At("/secrets", Role.Observer);

        Assert.Equal(Secrets.ForOperators, page.Find(".secret-unlisted").TextContent);
        Assert.Empty(page.FindAll(".secret-records"));
        Assert.Empty(page.FindAll("button"));
        Assert.Empty(page.FindAll(".secret-set"));
        Assert.Empty(_surface.Listings);
    }

    [Fact]
    public void ASurfaceThatReachesNoNodeSaysSoInWords()
    {
        _surface.Kept = null;

        IRenderedComponent<Secrets> page = At("/secrets", Role.Operator);

        Assert.Equal(
            "REFUSED: scripted reaches no node, so lists no secrets",
            page.Find(".subs-said.refused").TextContent.Trim());
        // Refused is not empty: nothing claims the node keeps none.
        Assert.Empty(page.FindAll(".secret-records"));
    }

    [Fact]
    public void TheNodeInTheAddressIsTheOneAskedAndATickAsksNothingAgain()
    {
        TestCluster names = TestCluster.Read();
        string node = $"{names.Scope}/node/{names.WithRole("sending")}";
        IRenderedComponent<Secrets> page =
            At($"/secrets?location={Uri.EscapeDataString(node)}", Role.Operator);

        page.Render();

        Assert.Equal([node], _surface.Listings);
        Assert.Equal($"/secrets?location={Uri.EscapeDataString(node)}", ScopeLink.Secrets(node));
        Assert.Equal("/secrets", ScopeLink.Secrets(names.Scope));
    }
}
