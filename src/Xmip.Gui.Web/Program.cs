using Tomlyn.Extensions.Configuration;
using Xmip.Gui.Web;
using Xmip.Gui.Web.Components;
using Xmip.Gui.Surface;

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
// so those sources go and xmip.gui.toml takes their place. Command line and
// environment variables stay, because an operator overriding one key at
// launch is not a configuration file.
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

builder.Configuration.AddTomlFile("xmip.gui.toml", optional: false, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// One surface for every screen. The real one loads the runtime's native
// library and reads its operator table; when that cannot happen, a stand-in
// takes its place and says so on every page. ADR-0027.
builder.Services.AddSingleton<IOperatorSurface>(services =>
{
    // The Xmip Playground, if it is rolling. Its snapshot file is the live
    // matrix an operator watches over time; when it is present, show it. The
    // path is configurable and defaults to the one the playground writes to.
    string snapshot = builder.Configuration["Xmip:PlaygroundSnapshot"] ?? FileOperator.DefaultPath;

    if (File.Exists(snapshot))
    {
        services.GetRequiredService<ILogger<Program>>().ShowingPlayground(snapshot);

        return new FileOperator(snapshot);
    }

    string? configured = builder.Configuration["Xmip:RuntimeLibrary"];
    string path = string.IsNullOrWhiteSpace(configured)
        ? Path.Combine(AppContext.BaseDirectory, "xmip_core_runtime.dll")
        : configured;

    NativeOperator? native = NativeOperator.Load(path, out string reason);

    if (native is not null)
    {
        // A node to start, if one is configured. Without it the runtime says
        // so itself, on the page, and says what to do.
        string? node = builder.Configuration["Xmip:NodeConfiguration"];

        if (!string.IsNullOrWhiteSpace(node))
        {
            string outcome = native.Start(node);

            services.GetRequiredService<ILogger<Program>>().NodeStarted(outcome);
        }

        return native;
    }

    services.GetRequiredService<ILogger<Program>>().ShowingSample(reason);

    return new SampleOperator(reason);
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
    .AddAdditionalAssemblies(typeof(IOperatorSurface).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();
