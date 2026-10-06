using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Gui.Pages;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// A selected relationship on the Topology is its id, not the record read
/// when it was selected: a new publication shows what passes over it now in
/// the inspector, and the selection ends only when the link is gone.
/// </summary>
public sealed class TopologySelectionTest : BunitContext, IDisposable
{
    private const string Links = "[[topology.links]]";

    private readonly string _place =
        Path.Combine(Path.GetTempPath(), $"xmip-gui-topology-{Guid.NewGuid():N}");

    private readonly string _snapshot;

    public TopologySelectionTest()
    {
        Directory.CreateDirectory(_place);
        _snapshot = Path.Combine(_place, "cluster.toml");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixture", "cluster.toml"), _snapshot);
        Services.AddSingleton(ClusterSurfaces.Over(new SnapshotOperator(_snapshot)));
        Services.AddSingleton(new RoleContext(Role.Observer, "tester"));
    }

    void IDisposable.Dispose()
    {
        base.Dispose();
        Directory.Delete(_place, recursive: true);
    }

    [Fact]
    public void ASelectedLinkIsReadAgainFromEachPublicationAndClearedOnlyWhenItIsGone()
    {
        IRenderedComponent<Topology> page = Render<Topology>();
        page.FindAll("g.topology-link path.link-hit")[0]
            .KeyDown(new KeyboardEventArgs { Key = "Enter" });
        string before = Said(page, "Evidence");
        Assert.DoesNotContain("now:", before, StringComparison.Ordinal);

        // Republished: every link's evidence says something new, ids kept.
        Publish(links => links.Replace(
            "\nevidence = \"", "\nevidence = \"now: ", StringComparison.Ordinal));

        // The view follows the publication; the inspector reads it as it lands.
        page.WaitForAssertion(() => Assert.Equal($"now: {before}", Said(page, "Evidence")));

        // Republished without it: every link under another id.
        Publish(links => links.Replace("\nid = \"", "\nid = \"gone/", StringComparison.Ordinal));

        page.WaitForAssertion(() => Assert.DoesNotContain(
            page.FindAll(".topology-inspector dt"), dt => dt.TextContent == "Protocol"));
    }

    // The value the inspector gives beside the term <paramref name="term"/>.
    private static string Said(IRenderedComponent<Topology> page, string term)
    {
        IElement dt = Assert.Single(
            page.FindAll(".topology-inspector dt"), dt => dt.TextContent == term);

        return dt.NextElementSibling!.TextContent;
    }

    // Replace the publication whole, its links section changed by
    // <paramref name="change"/>, as a publisher replaces it.
    private void Publish(Func<string, string> change)
    {
        string text = File.ReadAllText(_snapshot);
        int links = text.IndexOf(Links, StringComparison.Ordinal);
        string next = text[..links] + change(text[links..]);
        File.WriteAllText(_snapshot + ".next", next);
        File.Move(_snapshot + ".next", _snapshot, overwrite: true);
    }
}
