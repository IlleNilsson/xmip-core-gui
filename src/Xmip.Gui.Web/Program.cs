using Xmip.Gui.Surface;
using Xmip.Gui.Web;
using Xmip.Gui.Web.Components;
using Xmip.Surface;

// Development unless the environment says otherwise. launchSettings.json used
// to set this and it is gone with the rest of the JSON; without it the host
// assumes Production, serves the development static-asset manifest as if it
// were published, and every script and stylesheet 500s — which the browser
// reports as "an unhandled error has occurred". A deployment sets
// ASPNETCORE_ENVIRONMENT in its service definition, per registration.rs.
WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    EnvironmentName =
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environments.Development,
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

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// The web GUI monitors and does nothing else (ADR-0014, amendment of
// 2026-09-05; ADR-0052 clause 3): its role is Observer, and that is not
// configurable — no key, no environment variable. What a person may do on
// the desktop is the desktop's to decide.
builder.Services.AddSingleton(new RoleContext(Role.Observer));

// One surface for every screen, chosen in xmip.gui.toml and never guessed
// (ADR-0052 clause 3): native over the runtime's library, or a snapshot at a
// path. This host loads no node and starts nothing — a node is started from
// the desktop or the CLI. Relative paths in the file resolve against the
// content root, which is where the file itself is: the project directory
// under `dotnet run`, the application's own directory once published.
builder.Services.AddSingleton<IOperatorSurface>(services =>
{
    IOperatorSurface surface = SurfaceChoice.Open(
        services.GetRequiredService<IConfiguration>(), builder.Environment.ContentRootPath);

    services.GetRequiredService<ILogger<Program>>().ReadingSurface(surface.Source);

    return surface;
});

WebApplication app = builder.Build();

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

app.Run();
