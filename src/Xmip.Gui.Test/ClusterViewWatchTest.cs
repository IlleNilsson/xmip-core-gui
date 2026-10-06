using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Gui.Surface;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// A view's watch that ended on its own — a surface that said its first view
/// and gave up — is begun again on the next parameter change, rather than the
/// view staying silent for as long as it is open.
/// </summary>
public sealed class ClusterViewWatchTest : BunitContext
{
    private readonly ScriptedSurface _surface = new();

    public ClusterViewWatchTest()
    {
        Services.AddSingleton(ClusterSurfaces.Over(_surface));
    }

    [Fact]
    public void AWatchThatEndedIsBegunAgainOnTheNextParameterChange()
    {
        IRenderedComponent<Watching> view = Render<Watching>();
        Assert.Equal(1, _surface.Watches);

        view.WaitForAssertion(() =>
        {
            view.Render();
            Assert.True(_surface.Watches >= 2, $"{_surface.Watches} watch(es) begun");
        });
    }

    /// <summary>The least a view is: it reads, and renders nothing.</summary>
    private sealed class Watching : ClusterView
    {
        protected override void Read()
        {
        }
    }
}
