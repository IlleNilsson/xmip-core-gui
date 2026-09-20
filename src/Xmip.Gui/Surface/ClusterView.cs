using Microsoft.AspNetCore.Components;
using Xmip.Surface;

namespace Xmip.Gui.Surface;

/// <summary>
/// What each of the three views is: a face over one cluster of the set the
/// host holds (ADR-0052, amendment 2026-09-20). Which cluster is in the
/// address — <c>?cluster=C2</c> — so a link carries it, a reload keeps it and
/// two browser tabs can watch two clusters at once. A view told nothing, or
/// told a cluster this host does not hold, is on the first: a roll that ended
/// leaves a link behind, and the answer to that is the other cluster, never an
/// error page.
/// </summary>
/// <remarks>
/// The base exists so the pages stay what ADR-0052 clause 1 says they are —
/// thin faces. Choosing from the set, naming what was chosen and following
/// that cluster's change feed are one thing, written once; a page says how to
/// read a publication and nothing else. The watch moves with the address: a
/// view that kept watching the cluster it left would redraw on the wrong
/// publisher's tick, which is the silent kind of wrong this record exists to
/// stop.
/// </remarks>
public abstract class ClusterView : ComponentBase, IDisposable
{
    /// <summary>The name the address gives the cluster in view.</summary>
    public const string Query = "cluster";

    private CancellationTokenSource? stop;

    private string? watched;

    /// <summary>Every cluster this host holds.</summary>
    [Inject]
    protected ClusterSurfaces Surfaces { get; set; } = default!;

    /// <summary>The cluster the address asked for; null where it asked for
    /// none. <see cref="Cluster"/> is what came of it.</summary>
    [SupplyParameterFromQuery(Name = Query)]
    public string? Asked { get; set; }

    /// <summary>The publication this view reads: the asked cluster's, or the
    /// first where the host does not hold it.</summary>
    protected IOperatorSurface Surface => Surfaces.For(Asked);

    /// <summary>The cluster this view is on, as the chooser marks it.</summary>
    protected string Cluster => Surfaces.Showing(Asked);

    /// <summary>What a link out of this view carries: the cluster where the
    /// host holds more than one, nothing where it holds one. A host with one
    /// cluster writes the addresses it always wrote.</summary>
    protected string? Carried => Surfaces.Several ? Cluster : null;

    /// <summary>Read the publication into whatever this view renders. Called
    /// on every parameter change and on every notice from the cluster in
    /// view; a view reads its index once here and answers from it.</summary>
    protected abstract void Read();

    /// <inheritdoc />
    public void Dispose()
    {
        End();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        Read();

        if (stop is not null && string.Equals(watched, Cluster, StringComparison.Ordinal))
        {
            return;
        }

        watched = Cluster;
        End();
        stop = new CancellationTokenSource();
        _ = ObserveAsync(Surface, stop.Token);
    }

    private async Task ObserveAsync(IOperatorSurface surface, CancellationToken ending)
    {
        try
        {
            await foreach (SurfaceChange _ in surface.WatchAsync(ending))
            {
                await InvokeAsync(() =>
                {
                    Read();
                    StateHasChanged();
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Leaving the page, or moving to another cluster, ends the watch.
        }
    }

    private void End()
    {
        stop?.Cancel();
        stop?.Dispose();
        stop = null;
    }
}
