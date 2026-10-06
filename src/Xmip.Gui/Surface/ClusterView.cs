using Microsoft.AspNetCore.Components;
using Xmip.Surface;

namespace Xmip.Gui.Surface;

/// <summary>
/// What each of the three views is: a face over one cluster of the set the
/// host holds (ADR-0052, amendment 2026-09-20). Which cluster is in the
/// address — <c>?cluster=&lt;name&gt;</c>, whatever the cluster was named — so
/// a link carries it, a reload keeps it and two browser tabs can watch two
/// clusters at once. A view told nothing, or
/// told a cluster this host does not hold, is on the first: a roll that ended
/// leaves a link behind, and the answer to that is the other cluster, never an
/// error page. Whether the view shows test clusters — those whose run declared
/// itself hidden — is in the address too, <c>?hidden=include</c>, off unless
/// said (the owner, 2026-09-29; ADR-0052, amendment 2026-09-30): off, a
/// hidden cluster is not listed, not reached and not linked to; on, it is,
/// marked as test.
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

    // The watch running for the cluster in view; one that ended on its own —
    // a surface that gave up, or a feed that closed — is begun again on the
    // next parameter change rather than left silent.
    private Task? watching;

    /// <summary>Every cluster this host holds.</summary>
    [Inject]
    protected ClusterSurfaces Surfaces { get; set; } = default!;

    /// <summary>The cluster the address asked for; null where it asked for
    /// none. <see cref="Cluster"/> is what came of it.</summary>
    [SupplyParameterFromQuery(Name = Query)]
    public string? Asked { get; set; }

    /// <summary>What the address says of the "show test clusters" box: its
    /// word, or null where it says nothing.</summary>
    [SupplyParameterFromQuery(Name = Carry.Query)]
    public string? AskedHidden { get; set; }

    /// <summary>Whether this view shows test clusters: the clusters, and the
    /// audit records, of a run that declared itself hidden.</summary>
    protected bool IncludeHidden => Carry.Says(AskedHidden);

    /// <summary>The publication this view reads: the asked cluster's, or the
    /// first listed where the host does not list it.</summary>
    protected IOperatorSurface Surface => Surfaces.For(Asked, IncludeHidden);

    /// <summary>The cluster this view is on, as the chooser marks it; empty
    /// where the host lists none.</summary>
    protected string Cluster => Surfaces.Showing(Asked, IncludeHidden);

    /// <summary>What a link out of this view carries: the cluster where the
    /// host lists more than one, and the box where it is ticked. A host with
    /// one cluster and the box unticked writes the addresses it always
    /// wrote.</summary>
    protected Carry Carried =>
        new(Surfaces.Several(IncludeHidden) ? Cluster : null, IncludeHidden);

    /// <summary>What the view's filter box says: a pattern over the scopes,
    /// empty for none.</summary>
    protected string Pattern { get; private set; } = string.Empty;

    /// <summary>The filter over the publication last read. It narrows what is
    /// shown, never what the cluster is: a banner or a tile says the whole
    /// cluster whatever is typed, so no pattern can make a troubled estate
    /// look fine (ADR-0052, amendment 2026-09-19).</summary>
    protected ScopeFilter Filter { get; private set; } = ScopeFilter.None;

    /// <summary>The filter box changed: read again under the new
    /// pattern.</summary>
    protected void Filtered(string pattern)
    {
        Pattern = pattern;
        Read();
    }

    /// <summary>Set <see cref="Filter"/> over a publication just read; each
    /// view calls it from <see cref="Read"/> with its index.</summary>
    protected void Refilter(ScopeIndex index)
    {
        Filter = ScopeFilter.Over(index, Pattern);
    }

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

        if (stop is not null
            && watching is { IsCompleted: false }
            && string.Equals(watched, Cluster, StringComparison.Ordinal))
        {
            return;
        }

        watched = Cluster;
        End();
        stop = new CancellationTokenSource();
        watching = ObserveAsync(Surface, stop.Token);
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
        watching = null;
    }
}
