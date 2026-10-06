using Microsoft.AspNetCore.Components.Server.Circuits;
using Xmip.Abi.Operate;
using Xmip.Gui.Hosting;
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

    // Relative paths in the file resolve by the one rule the desktop shares:
    // the estate root in a development build, the host's own directory once
    // published — not the content root, which is the bin folder when the
    // built host is started and the project under `dotnet run`.
    string basePath = TomlDocument.BasePath(builder.Environment.ContentRootPath);

    // Everything this host does and every failure, audited through the audit
    // capability (ADR-0062): into the directory xmip.gui.toml names, else where
    // the capability decides. Every error the host logs is a record — a circuit
    // that dies, which the browser shows as "an unhandled error has occurred",
    // among them — and so is every exception nothing handled.
    audit = new ProgramAudit(
        Name, ProgramAudit.Stated(builder.Configuration, basePath));
    audit.WatchUnhandled();
    builder.Logging.AddProvider(new AuditLoggerProvider(audit));
    builder.Services.AddSingleton(audit);
    builder.Services.AddScoped<CircuitHandler, AuditCircuitHandler>();

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    // The web offers what the desktop offers, by role (ADR-0014 and ADR-0052,
    // amendments of 2026-09-14): an Observer watches, an Operator also acts.
    // The role is each browser's own (ADR-0009, amendment 2026-10-06): the
    // caller its connection proved, granted a role by the one rule both hosts
    // and the hub take it by — the run's statement, else the directory this
    // configuration names, else Observer. A browser nothing proved watches.
    builder.Services.AddSingleton(RoleAssignment.From(builder.Configuration));
    builder.Services.AddXmipProvenCaller();

    // The surfaces every screen reads, chosen in xmip.gui.toml and never guessed
    // (ADR-0052 clause 3): native over the runtime's library, or a snapshot at a
    // path — or, since the amendment of 2026-09-20, one snapshot per cluster, so
    // one host serves two rolls and the views move between them. This host loads
    // no node and starts nothing — a node is started from the desktop or the CLI.
    // Relative paths in the file resolve against basePath, above.
    builder.Services.AddSingleton(services =>
    {
        ClusterSurfaces held = SurfaceChoice.OpenAll(
            services.GetRequiredService<IConfiguration>(), basePath);

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
    // An act asked over it is taken only where the role the assignment above
    // grants its caller may act, and as the client certificate's subject;
    // refused in words and audited otherwise (ADR-0009, amendments 2026-10-03
    // and 2026-10-06).
    builder.Services.AddXmipSurfaceRelay();

    // Xmip's own traffic is TLS (ADR-0063 clause 1). Every address this host
    // binds beyond this machine is HTTPS with the certificate xmip.gui.toml
    // names, asking a caller for one and checking it; plain HTTP beyond
    // loopback is refused here, before anything listens. Loopback is the one
    // exception, and it is said below where it is bound.
    SurfaceTls tls = SurfaceTls.From(builder.Configuration, basePath);
    IReadOnlyList<string> plain = SurfaceBinding.Check(
        SurfaceBinding.Addresses(builder.Configuration), tls);
    builder.WebHost.UseXmipTls(tls, why => audit.Record(
        "tls", AuditPhase.Failure, AuditSeverity.Warning, why));

    WebApplication app = builder.Build();

    // What this process says of itself while it runs (ADR-0053): the surface it
    // reads, and the purpose what started it stated. Start-XmipOperationWeb says
    // test where it follows a Playground roll. Declared through the node, in
    // the runtime library this host was told to load.
    ProcessDeclaration.Declare(
        Name,
        Located(SurfaceChoice.Snapshots(app.Configuration))
            ?? app.Configuration[SurfaceChoice.UrlKey]
            ?? app.Configuration[SurfaceChoice.SurfaceKey]
            ?? ScopeTree.Root,
        ProcessDeclaration.PurposeOf(app.Configuration),
        RuntimeLibrary.Find(app.Configuration, basePath));

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    app.UseHttpsRedirection();

    // Every request's user is the caller its connection proved — a client
    // certificate, else this machine's loopback, else no one — before a page
    // renders or a circuit opens, so each browser acts as itself and never as
    // this host's account (ADR-0009, amendment 2026-10-06).
    app.UseXmipProvenCaller();

    app.MapStaticAssets();
    // The pages are in Xmip.Gui, the shared library. Both the endpoint mapping
    // here and the <Router> in Routes.razor have to be told so; the router alone
    // finds the page and the endpoint alone serves it, and either without the
    // other is a 404 that looks like a missing route (2026-09-05).
    app.MapRazorComponents<App>()
        .AddAdditionalAssemblies(typeof(Xmip.Gui.Pages.Cluster).Assembly)
        .AddInteractiveServerRenderMode();
    app.MapXmipSurfaceHub(why => audit.Record(
        "tls", AuditPhase.Failure, AuditSeverity.Warning, why));

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
    // The one exception, said where it is made (ADR-0063 clause 1): a log
    // line and an audit record for every plain address, which is loopback.
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        foreach (string address in plain)
        {
            app.Logger.PlainLoopback(address);
            audit.Record(
                "tls", AuditPhase.Execute, AuditSeverity.Information,
                $"{address} is plain HTTP on loopback, ADR-0063 clause 1's one exception",
                new Dictionary<string, string> { ["url"] = address });
        }
    });
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
