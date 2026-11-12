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
// capability decides; after, where gui.toml says.
ProgramAudit audit = new(Name);

try
{
    // Production unless the environment says otherwise: Development shows a
    // browser the exception page, stack traces included, and drops HSTS. A
    // host run from its build output rather than a publish reads its static
    // assets through the build's manifest, which is what UseStaticWebAssets
    // turns on; without it every script and stylesheet 500s.
    WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environments.Production,
    });

    if (File.Exists(Path.Combine(AppContext.BaseDirectory, $"{Name}.staticwebassets.runtime.json")))
    {
        builder.WebHost.UseStaticWebAssets();
    }

    // TOML, and only TOML. The host would read appsettings.json and its
    // environment variant by default; Xmip configures nothing in JSON anywhere,
    // so those sources go and gui.toml takes their place, through the one
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

    TomlDocument.Add(builder.Configuration, "gui.toml", optional: false);
    builder.Configuration.AddEnvironmentVariables();
    builder.Configuration.AddCommandLine(args);

    // Relative paths in the file resolve by the one rule the desktop shares:
    // the estate root in a development build, the host's own directory once
    // published — not the content root, which is the bin folder when the
    // built host is started and the project under `dotnet run`.
    string basePath = TomlDocument.BasePath(builder.Environment.ContentRootPath);

    // Everything this host does and every failure, audited through the audit
    // capability (ADR-0062): into the directory gui.toml names, else where
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
    // take it by — the run's statement, else the directory this configuration
    // names, else Observer. A browser nothing proved watches.
    builder.Services.AddSingleton(RoleAssignment.From(builder.Configuration));
    builder.Services.AddXmipProvenCaller();

    // The operator's own view — the bar's order and the theme — is each
    // browser's own, kept in its cookies (ADR-0052, amendments 2026-11-12):
    // read as a page is asked for, handed to the circuit by Routes, and
    // written at the address mapped below.
    builder.Services.AddScoped<PreferenceCookie>();
    builder.Services.AddScoped<IPreferenceStore>(
        services => services.GetRequiredService<PreferenceCookie>());

    // The surfaces every screen reads, chosen in gui.toml and never guessed
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

    // Xmip's own traffic is TLS (ADR-0063 clause 1). Every address this host
    // binds beyond this machine is HTTPS in Xmip's own TLS, the runtime's
    // (xmip_operate.h section 21; ADR-0009, amendment 2026-10-21), presenting
    // the certificate gui.toml names and asking a caller for one, verified
    // against its TrustAnchor; plain HTTP beyond loopback is refused here,
    // before anything listens. Loopback is the one exception, and it is said
    // below where it is bound. Kestrel's own HTTPS is never used.
    SecureSurface tls = SecureSurface.From(builder.Configuration, basePath);
    IReadOnlyList<string> plain = SurfaceBinding.Check(
        SurfaceBinding.Addresses(builder.Configuration), tls);
    builder.WebHost.UseSurface(Protection.Tls, tls, why => audit.Record(
        "tls", AuditPhase.Failure, AuditSeverity.Warning, why));

    WebApplication app = builder.Build();

    // What this process says of itself while it runs (ADR-0053): the surface it
    // reads, and the purpose what started it stated. Start-XmipOperationWeb says
    // test where it follows an Essentials roll. Declared through the node, in
    // the runtime library this host was told to load.
    ProcessDeclaration.Declare(
        Name,
        Located(SurfaceChoice.Snapshots(app.Configuration))
            ?? app.Configuration[SurfaceChoice.AddressKey]
            ?? app.Configuration[SurfaceChoice.SurfaceKey]
            ?? ScopeTree.Root,
        ProcessDeclaration.PurposeOf(app.Configuration),
        RuntimeLibrary.Find(app.Configuration, basePath));

    // Every response, an error page included, carries the Operation Web's
    // content security policy and a nonce of its own, which App.razor's
    // import map carries (ContentSecurity in Xmip.Surface.Relay).
    app.UseXmipContentSecurity();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    // No HTTPS redirection: Kestrel names every address the runtime's TLS
    // serves http:// (SecureTransport.Shown), so the middleware never found
    // an HTTPS port to send a request to and redirected nothing, and the one
    // plain address it could act on is loopback's, which is plain on purpose.
    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

    // Every request's user is the caller its connection proved — a client
    // certificate, else no one, over loopback as from anywhere — before a page
    // renders or a circuit opens, so each browser acts as itself and never as
    // this host's account (ADR-0009, amendments 2026-10-06 and 2026-10-21).
    app.UseXmipProvenCaller();

    app.MapStaticAssets();

    // A browser keeping what it chose — the order it arranged the bar in, or
    // its theme: each cookie asked for written, or deleted where the choice
    // is the default, and the browser sent back to the page it came from, on
    // this host only (PreferenceCookie.Back). A cookie holds a view of one's
    // own, nothing to protect, so it is the page's only and never sent across
    // sites.
    app.MapGet(
        "/" + PreferenceCookie.Address,
        (HttpContext context, string? bar, string? theme, string? back) =>
        {
            if (bar is not null)
            {
                Kept(context, BarOrder.Cookie, BarOrder.Kept(bar));
            }

            if (theme is not null)
            {
                Kept(context, ThemeChoice.Cookie, ThemeChoice.Kept(theme));
            }

            return Results.LocalRedirect(PreferenceCookie.Back(back));
        });

    // The pages are in Xmip.Gui, the shared library. Both the endpoint mapping
    // here and the <Router> in Routes.razor have to be told so; the router alone
    // finds the page and the endpoint alone serves it, and either without the
    // other is a 404 that looks like a missing route (2026-09-05).
    app.MapRazorComponents<App>()
        .AddAdditionalAssemblies(typeof(Xmip.Gui.Pages.Cluster).Assembly)
        .AddInteractiveServerRenderMode();

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
            ["urls"] = string.Join(", ", app.Services.GetRequiredService<SecureTransport>()
                .Shown(app.Urls)),
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

// One of the operator's choices kept in its cookie, or the cookie deleted
// where the choice is the default (null).
static void Kept(HttpContext context, string cookie, string? value)
{
    if (value is null)
    {
        context.Response.Cookies.Delete(cookie);
        return;
    }

    context.Response.Cookies.Append(cookie, value, new CookieOptions
    {
        Path = "/",
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        IsEssential = true,
        MaxAge = TimeSpan.FromDays(400),
    });
}
