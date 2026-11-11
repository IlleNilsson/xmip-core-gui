using Microsoft.Extensions.DependencyInjection;
using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Gui.Surface;

/// <summary>
/// What the Reports view is hosted with (ADR-0072, amendment 2026-11-10):
/// the runtime's section 25 it reads every Report through, and, on a host
/// that edits a cluster's <c>cluster.xmip</c>, the file its Quests probe. One
/// place for both, so the desktop registers the view through
/// <see cref="Add"/> and a test hosts it the same way. A host that registers
/// none — the web — reads the runtime this process loaded and probes
/// nothing (<see cref="Loaded"/>); on the phone, a remote build, reading the
/// runtime refuses in its words, and the view says so.
/// </summary>
/// <param name="reports">The runtime's section 25: asked each time the view
/// reads, so a refusal is the view's to say, never the host's to crash
/// on.</param>
/// <param name="probed">The cluster's file the Quests probe, with the node
/// configured here; null on a host that edits none.</param>
public sealed class ReportHosting(Func<RuntimeReports> reports, SliceDelivery? probed)
{
    /// <summary>The runtime this process loaded, and no cluster's file: how
    /// a host that registers nothing hosts the view.</summary>
    public static ReportHosting Loaded { get; } =
        new(() => RuntimeLibrary.Rules.Reports, null);

    /// <summary>The runtime's section 25.</summary>
    /// <exception cref="InvalidOperationException">The runtime is not loaded,
    /// or is a remote build: in its words.</exception>
    public RuntimeReports Reports => reports();

    /// <summary>The cluster's file the Quests probe; null where this host
    /// edits none.</summary>
    public SliceDelivery? Probed { get; } = probed;

    /// <summary>Host the Reports view on <paramref name="services"/> as the
    /// desktop does: the runtime this process loaded, the Quests probing the
    /// <c>cluster.xmip</c> <paramref name="delivery"/> delivers, on the node
    /// configured there unless the address names another.</summary>
    public static IServiceCollection Add(IServiceCollection services, SliceDelivery delivery)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(delivery);

        return services.AddSingleton(
            new ReportHosting(() => RuntimeLibrary.Rules.Reports, delivery));
    }
}
