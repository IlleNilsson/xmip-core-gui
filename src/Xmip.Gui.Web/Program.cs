using Xmip.Gui.Web;
using Xmip.Gui.Web.Components;
using Xmip.Gui.Web.Surface;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// One surface for every screen. The real one loads the runtime's native
// library and reads its operator table; when that cannot happen, a stand-in
// takes its place and says so on every page. ADR-0027.
builder.Services.AddSingleton<IOperatorSurface>(services =>
{
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
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
