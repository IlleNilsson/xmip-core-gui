using System.Reflection;
using Microsoft.Extensions.Logging;
using Xmip.Abi.Operate;
using Xmip.Gui.Hosting;
using Xmip.Gui.Surface;
using Xmip.Operations.Configuration;
using Xmip.Surface;

namespace Xmip.Operations;

public static class MauiProgram
{
    /// <summary>The System Process (ADR-0053), and the program every audit
    /// record of this host names.</summary>
    public const string Name = "xmip-operations";

    public static MauiApp CreateMauiApp()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

        // TOML beside the executable, like the web host — Xmip configures
        // nothing in JSON — through the one reader every surface uses. The
        // file is deployed as content next to the app.
        string here = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? AppContext.BaseDirectory;
        TomlDocument.Add(
            builder.Configuration, Path.Combine(here, "xmip.gui.toml"), optional: true);

        // Relative paths in the config resolve by the one rule the web host
        // shares: the estate root in a development build, the app's own
        // directory once packaged.
        string basePath = TomlDocument.BasePath(here);

        // What this process says of itself while it runs (ADR-0053), through
        // the runtime library this desktop was told to load. The desktop
        // configures and monitors a real node: runtime.
        ProcessDeclaration.Declare(
            Name,
            ScopeTree.Root,
            ProcessDeclaration.Runtime,
            RuntimeLibrary.Find(builder.Configuration, basePath));

        // Everything this host does and every failure, audited through the
        // audit capability (ADR-0062): into the directory xmip.gui.toml names,
        // else where the capability decides. Every error the host logs is a
        // record, and so is every exception nothing handled.
        ProgramAudit audit = new(Name, ProgramAudit.Stated(builder.Configuration, basePath));
        audit.WatchUnhandled();
        builder.Logging.AddProvider(new AuditLoggerProvider(audit));
        builder.Services.AddSingleton(audit);

        // The role is assigned, never chosen in the UI (ADR-0009): a person
        // cannot promote themselves. The one rule both hosts take it by.
        builder.Services.AddSingleton(
            new RoleContext(RoleContext.Assigned(builder.Configuration)));

        // The configurations the Configure page lists: the directory
        // xmip.gui.toml names, else one under app data; the one the desktop
        // starts is the NodeConfiguration it declares, never a file's name.
        string? configured = builder.Configuration["Xmip:ConfigDirectory"];
        string? starts = builder.Configuration["Xmip:NodeConfiguration"];
        builder.Services.AddSingleton(new ConfigStore(
            string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, "xmip", "config")
                : TomlDocument.Resolve(configured, basePath),
            string.IsNullOrWhiteSpace(starts) ? null : TomlDocument.Resolve(starts, basePath)));

        // The surfaces every screen reads, chosen in xmip.gui.toml and never
        // guessed (ADR-0052 clause 3), by the web host's one rule: one
        // snapshot per cluster where several are named, so the desktop moves
        // between clusters as the web does (the owner, 2026-10-02: "make it so
        // that the desktop version opens multiple clusters too"). The desktop
        // configures (ADR-0014, amendment of 2026-09-05), so when the surface
        // is the runtime and a node is named, it starts that node — the one
        // thing a browser cannot do.
        builder.Services.AddSingleton(services =>
        {
            ClusterSurfaces held = SurfaceChoice.OpenAll(builder.Configuration, basePath);
            string? node = builder.Configuration["Xmip:NodeConfiguration"];

            if (held.First is NativeOperator native && !string.IsNullOrWhiteSpace(node))
            {
                ConfigurationVerdict started = native.Start(TomlDocument.Resolve(node, basePath));

                // Started at launch rather than by a button, and audited the
                // same way (ADR-0062).
                audit.Record(
                    "start node",
                    started.Ok ? AuditPhase.Finished : AuditPhase.Failure,
                    started.Ok ? AuditSeverity.Information : AuditSeverity.Error,
                    started.Said,
                    new Dictionary<string, string> { ["configuration"] = started.Path });
            }

            return held;
        });

        // Configure's commands answer one surface: the first cluster's
        // (SurfaceChoice.OpenFirst's rule), the one a node is started from.
        builder.Services.AddSingleton(services =>
            services.GetRequiredService<ClusterSurfaces>().First);

        // Validate and Start on the Configure page go through the runtime the
        // board reads when that is the native one; over a snapshot, the
        // runtime is found by the one rule and loaded for the commands alone.
        builder.Services.AddSingleton(services => new RuntimeCommands(
            services.GetRequiredService<IOperatorSurface>(),
            RuntimeLibrary.Find(builder.Configuration, basePath),
            audit));

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        MauiApp app = builder.Build();
        audit.Record("start", AuditPhase.Begin, AuditSeverity.Information);

        return app;
    }
}
