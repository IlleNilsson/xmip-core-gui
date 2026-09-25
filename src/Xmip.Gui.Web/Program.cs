using Microsoft.AspNetCore.Components.Server.Circuits;
using Xmip.Abi.Operate;
using Xmip.Gui.Hosting;
using Xmip.Gui.Surface;
using Xmip.Gui.Web;
using Xmip.Gui.Web.Components;
using Xmip.Surface;
using Xmip.Surface.Relay;

// The System Process is xmip-gui-web (ADR-0053), and so is the program every
// audit record of this host names.
const string Name = "xmip-gui-web";

// Audited from the first line: until the configuration is read, where the
// capability decides; after, where xmip.gui.toml says.
ProgramAudit audit = new(Name);

try
{
    // Development unless the environment says otherwise. launchSettings.json used
    // to set this and it is gone with the rest of the JSON; without it the host
    // assumes Production, serves the development static-asset manifest as if it
    // were published, and every script and stylesheet 500s — which the browser
    // reports as "an unhandled error has occurred". A deployment sets
    // ASPNETCORE_ENVIRONMENT in its service definition, per registration.rs.
    WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environments.Development,
    });

    // TOML, and only TOML. The host would read appsettings.json and its
    // environment variant by default; Xmip configures nothing in JSON anywhere,
    // so those sources go and xmip.gui.toml takes their place, through the one
    // reader every surface uses. Command line and environment variables stay,
    // because an operator overriding one key at launch is not a configuration
    // file.
    // Later sources win. The TOML file goes after the removed JSON ones, and the
    // environment and command line are added again after it, so that
    // `--Kestrel:Endpoints:Http:Url=...` at launch still overrides the file.
    // Adding a source twice is harmless; inserting one by position is not — a
    // source inserted rather than added never had its file provider set, and read
    // nothing (2026-09-05).
    foreach (IConfigurationSource source in builder.Configuration.Sources.ToArray())
    {
        if (source is Microsoft.Extensions.Configuration.Json.JsonConfigurationSource)
        {
            builder.Configuration.Sources.Remove(source);
        }
    }

    TomlDocument.Add(builder.Configuration, "xmip.gui.toml", optional: false);
    builder.Configuration.AddEnvironmentVariables();
    builder.Configuration.AddCommandLine(args);

    // Everything this host does and every failure, audited through the audit
    // capability (ADR-0062): into the directory xmip.gui.toml names, else where
    // the capability decides. Every error the host logs is a record — a circuit
    // that dies, which the browser shows as "an unhandled error has occurred",
    // among them — and so is every exception nothing handled.
    audit = new ProgramAudit(
        Name, ProgramAudit.Stated(builder.Configuration, builder.Environment.ContentRootPath));
    audit.WatchUnhandled();
    builder.Logging.AddProvider(new AuditLoggerProvider(audit));
    builder.Services.AddSingleton(audit);
    builder.Services.AddScoped<CircuitHandler, AuditCircuitHandler>();

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    // The web GUI monitors and does nothing else (ADR-0014, amendment of
    // 2026-09-05; ADR-0052 clause 3): its role is Observer, and that is not
    // configurable — no key, no environment variable. What a person may do on
    // the desktop is the desktop's to decide.
    builder.Services.AddSingleton(new RoleContext(Role.Observer));

    // The surfaces every screen reads, chosen in xmip.gui.toml and never guessed
    // (ADR-0052 clause 3): native over the runtime's library, or a snapshot at a
    // path — or, since the amendment of 2026-09-20, one snapshot per cluster, so
    // one host serves two rolls and the views move between them. This host loads
    // no node and starts nothing — a node is started from the desktop or the CLI.
    // Relative paths in the file resolve against the content root, which is where
    // the file itself is: the project directory under `dotnet run`, the
    // application's own directory once published.
    builder.Services.AddSingleton(services =>
    {
        ClusterSurfaces held = SurfaceChoice.OpenAll(
            services.GetRequiredService<IConfiguration>(), builder.Environment.ContentRootPath);

        services.GetRequiredService<ILogger<Program>>().ReadingSurface(held.Source);

        return held;
    });

    // The relay serves one host's surface (ADR-0052, amendment 2026-09-15), and a
    // surface is one cluster's publication. Where this host holds several, what it
    // serves over the hub is the first — a remote surface reads one tree, and a
    // tree of two clusters is a scope that is in neither.
    builder.Services.AddSingleton(
        services => services.GetRequiredService<ClusterSurfaces>().First);

    // This host's surface, served: the CLI, the PowerShell module and a GUI on
    // another machine follow it over SignalR and are told when it changes, never
    // asking (ADR-0052, amendment 2026-09-15). The same surface the pages read.
    builder.Services.AddXmipSurfaceRelay();

    WebApplication app = builder.Build();

    // What this process says of itself while it runs (ADR-0053): the surface it
    // reads, and the purpose what started it stated. Start-XmipOperationWeb says
    // test where it follows a Playground roll.
    ProcessDeclaration.Declare(
        Name,
        Located(SurfaceChoice.Snapshots(app.Configuration))
            ?? app.Configuration[SurfaceChoice.UrlKey]
            ?? app.Configuration[SurfaceChoice.SurfaceKey]
            ?? ScopeTree.Root,
        ProcessDeclaration.PurposeOf(app.Configuration));

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    app.UseHttpsRedirection();

    app.MapStaticAssets();
    // The pages are in Xmip.Gui, the shared library. Both the endpoint mapping
    // here and the <Router> in Routes.razor have to be told so; the router alone
    // finds the page and the endpoint alone serves it, and either without the
    // other is a 404 that looks like a missing route (2026-09-05).
    app.MapRazorComponents<App>()
        .AddAdditionalAssemblies(typeof(Xmip.Gui.Pages.Cluster).Assembly)
        .AddInteractiveServerRenderMode();
    app.MapXmipSurfaceHub();

#if DEBUG
    // A Debug build's own unhandled error, so the path from a failure to its
    // audit record can be shown on the real host rather than claimed
    // (ADR-0062 clause 4). A Release build has no such route.
    app.MapGet("/debug/unhandled", string () => throw new InvalidOperationException(
        "forced by /debug/unhandled, a Debug build's own route"));
#endif

    app.Lifetime.ApplicationStarted.Register(() => audit.Record(
        "start", AuditPhase.Begin, AuditSeverity.Information, "listening",
        new Dictionary<string, string>
        {
            ["urls"] = string.Join(", ", app.Urls),
            ["surface"] = Located(SurfaceChoice.Snapshots(app.Configuration))
                ?? app.Configuration[SurfaceChoice.SurfaceKey] ?? ScopeTree.Root,
            ["purpose"] = ProcessDeclaration.PurposeOf(app.Configuration),
        }));
    app.Lifetime.ApplicationStopping.Register(() => audit.Record(
        "stop", AuditPhase.Finished, AuditSeverity.Information));

    app.Run();
}
catch (Exception failure) when (failure is not HostAbortedException)
{
    // A host that could not start or stopped on an exception says so in the
    // audit, not only on a console nobody kept (ADR-0062 clause 4).
    audit.Failed("host", failure);
    throw;
}

// Where this host reads: the one snapshot it follows, or every one of them
// where it holds several clusters (ADR-0052, amendment 2026-09-20).
static string? Located(IReadOnlyList<string> snapshots)
{
    return snapshots.Count == 0 ? null : string.Join(", ", snapshots);
}
