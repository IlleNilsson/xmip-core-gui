using System.Reflection;
using Microsoft.Extensions.Logging;
using Xmip.Abi.Operate;
using Xmip.Gui.Hosting;
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
        // cannot promote themselves. The caller is the operating system user
        // running the desktop, granted a role by the one rule both hosts and
        // the hub take it by (ADR-0009, amendment 2026-10-06).
        builder.Services.AddSingleton(
            RoleAssignment.From(builder.Configuration).For(GatedOperator.HostUser));

        // The cluster's xmip.toml the Configure page edits — the one file
        // anyone edits (ADR-0031, amendment 2026-10-05) — where each node's
        // slice is written on save, and the node of it configured here:
        // all three as xmip.gui.toml declares them, never read from a name.
        string? cluster = builder.Configuration["Xmip:ClusterConfiguration"];
        string? slices = builder.Configuration["Xmip:SliceDirectory"];
        string? node = builder.Configuration["Xmip:Node"];
        string data = Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, "xmip");
        SliceDelivery delivery = new(
            string.IsNullOrWhiteSpace(cluster)
                ? Path.Combine(data, "xmip.toml")
                : TomlDocument.Resolve(cluster, basePath),
            string.IsNullOrWhiteSpace(slices)
                ? Path.Combine(data, "slices")
                : TomlDocument.Resolve(slices, basePath),
            string.IsNullOrWhiteSpace(node) ? null : node);
        string library = RuntimeLibrary.Find(builder.Configuration, basePath);

        // The surfaces every screen reads, chosen in xmip.gui.toml and never
        // guessed (ADR-0052 clause 3), by the web host's one rule: one
        // snapshot per cluster where several are named, so the desktop moves
        // between clusters as the web does (the owner, 2026-10-02: "make it so
        // that the desktop version opens multiple clusters too"). Nothing is
        // planned at launch: nothing starts itself, and a node is planned only
        // when an operator presses Plan on the Configure page.
        builder.Services.AddSingleton(_ => SurfaceChoice.OpenAll(builder.Configuration, basePath));

        // Configure's commands answer one surface: the first cluster's
        // (SurfaceChoice.OpenFirst's rule), the one a node is planned on.
        builder.Services.AddSingleton(services =>
            services.GetRequiredService<ClusterSurfaces>().First);

        // Validate, Save and Plan on the Configure page go through the
        // runtime the board reads when that is the native one; over a
        // snapshot, the runtime is found by the one rule and loaded for the
        // commands alone.
        builder.Services.AddSingleton(services => new RuntimeCommands(
            services.GetRequiredService<IOperatorSurface>(), library, audit, delivery));

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        MauiApp app = builder.Build();
        audit.Record("start", AuditPhase.Begin, AuditSeverity.Information);

        return app;
    }
}
