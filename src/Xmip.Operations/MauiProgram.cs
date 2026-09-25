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
        // What this process says of itself while it runs (ADR-0053). The
        // desktop configures and monitors a real node: runtime.
        Xmip.Surface.ProcessDeclaration.Declare(
            Name,
            Xmip.Surface.ScopeTree.Root,
            Xmip.Surface.ProcessDeclaration.Runtime);

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

        // Relative paths in the config resolve against the repository root in a
        // development build, and against the app's own directory once packaged.
        // A developer runs from a bin folder several levels down, so pointing at
        // the runtime's target/debug needs the repo root, not the exe's folder.
        string basePath = RepositoryRoot(here) ?? here;

        // Everything this host does and every failure, audited through the
        // audit capability (ADR-0062): into the directory xmip.gui.toml names,
        // else where the capability decides. Every error the host logs is a
        // record, and so is every exception nothing handled.
        ProgramAudit audit = new(Name, ProgramAudit.Stated(builder.Configuration, basePath));
        audit.WatchUnhandled();
        builder.Logging.AddProvider(new AuditLoggerProvider(audit));
        builder.Services.AddSingleton(audit);

        // The role is assigned, never chosen in the UI (ADR-0009): a person
        // cannot promote themselves. It comes from the config file or the
        // XMIP_ROLE environment variable and defaults to Observer, so a missing
        // or wrong value grants nothing. A real identity supersedes this later
        // (ADR-0022/ADR-0027).
        string? assignedRole =
            builder.Configuration["Xmip:Role"] ?? Environment.GetEnvironmentVariable("XMIP_ROLE");
        builder.Services.AddSingleton(new RoleContext(RoleContext.Parse(assignedRole)));

        builder.Services.AddSingleton<ConfigStore>();

        // The one surface every screen reads, chosen in xmip.gui.toml and never
        // guessed (ADR-0052 clause 3): the same selection as the web host, from
        // the same keys. The desktop configures (ADR-0014, amendment of
        // 2026-09-05), so when the surface is the runtime and a node is named,
        // it starts that node — the one thing a browser cannot do.
        builder.Services.AddSingleton<IOperatorSurface>(services =>
        {
            IOperatorSurface surface = SurfaceChoice.Open(builder.Configuration, basePath);
            string? node = builder.Configuration["Xmip:NodeConfiguration"];

            if (surface is NativeOperator native && !string.IsNullOrWhiteSpace(node))
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

            return surface;
        });

        // The desktop routes to the same pages, and a page reads the set its
        // host holds (ADR-0052, amendment 2026-09-20). The desktop starts a
        // node from the surface it configures, so its set is that one surface;
        // a desktop over a list of snapshots is the web host's shape and is
        // not what the desktop is for.
        builder.Services.AddSingleton(services =>
            ClusterSurfaces.Over(services.GetRequiredService<IOperatorSurface>()));

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

    /// <summary>The repository root above a bin directory, found by walking up
    /// to the folder that holds <c>architecture.toml</c>. Null when packaged,
    /// where there is no repository and paths resolve against the app instead.</summary>
    private static string? RepositoryRoot(string start)
    {
        DirectoryInfo? directory = new(start);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "architecture.toml")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
