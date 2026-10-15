using Microsoft.Extensions.Configuration;
using Xmip.Surface;

namespace Xmip.Operations;

/// <summary>
/// The phone's part of the start (ADR-0014, amendment 2026-10-14): the same
/// host on Android and iOS, following nodes remotely over their operate
/// listener, inside Xmip's mutual TLS. The phone is an Observer: it reads,
/// it does not act, and it configures nothing — the Configure page and what
/// it calls are not compiled into it. Compiled for Android and iOS only.
/// </summary>
public static partial class MauiProgram
{
    /// <summary>The document shipped inside the app
    /// (<c>Resources/Raw/xmip.gui.toml</c>).</summary>
    private const string Document = "xmip.gui.toml";

    private static partial ProgramAudit Platform(MauiAppBuilder builder)
    {
        // The app's own data: the certificate files the document names, and
        // every relative path in it, resolve here.
        string data = FileSystem.AppDataDirectory;

        TomlDocument.Add(builder.Configuration, Shipped(), optional: false);

        // Into the directory the document names, else audit/ under the
        // app's data (ADR-0062).
        ProgramAudit audit = new(
            Name,
            ProgramAudit.Stated(builder.Configuration, data) ?? Path.Combine(data, "audit"));

        // Observer, and not configurable: the phone watches and offers no
        // act and no configuration (the owner, 2026-10-14). Nothing on the
        // phone proved who holds it.
        builder.Services.AddSingleton(new RoleContext(Role.Observer, null));

        // The operator's key, kept by the phone's own keeper and never read
        // here: the request is signed there and so is every handshake
        // (ADR-0034, amendment 2026-10-15). The document names the chain the
        // cluster's authority signs and no private key.
#if ANDROID
        IOperatorKeeper keeper = new AndroidKeeper();
#else
        IOperatorKeeper keeper = new EnclaveKeeper();
#endif
        builder.Services.AddSingleton(keeper);
        builder.Services.AddSingleton(services => SurfaceTls.From(
            services.GetRequiredService<IConfiguration>(), data, keeper));

        // The surfaces every screen reads: a node on another machine, at the
        // operate Address the document names (ADR-0052 clause 3).
        builder.Services.AddSingleton(services =>
            Remote(services.GetRequiredService<IConfiguration>(), data, keeper));

        return audit;
    }

    // A document shipped inside the app is no file on disk on Android, so the
    // one reader reads its copy, written afresh at every start: what was
    // shipped is what is read.
    private static string Shipped()
    {
        string copy = Path.Combine(FileSystem.CacheDirectory, Document);

        using Stream shipped =
            FileSystem.OpenAppPackageFileAsync(Document).GetAwaiter().GetResult();
        using FileStream written = File.Create(copy);
        shipped.CopyTo(written);

        return copy;
    }

    // Remote, and only remote: a phone has no node of its own to reach at a
    // local endpoint and no snapshot file to read.
    private static ClusterSurfaces Remote(
        IConfiguration configuration, string basePath, IOperatorKeeper keeper)
    {
        string chosen = configuration[SurfaceChoice.SurfaceKey]?.Trim() ?? string.Empty;

        return string.Equals(chosen, SurfaceChoice.Remote, StringComparison.OrdinalIgnoreCase)
            ? SurfaceChoice.OpenAll(configuration, basePath, keeper)
            : throw new InvalidOperationException(
                $"{SurfaceChoice.SurfaceKey} is \"{chosen}\"; on a phone it is " +
                $"\"{SurfaceChoice.Remote}\", a node followed at its operate " +
                $"{SurfaceChoice.AddressKey}");
    }
}
