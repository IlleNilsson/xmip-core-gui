using System.Reflection;
using Microsoft.Extensions.Logging;
using Tomlyn.Extensions.Configuration;
using Xmip.Gui.Surface;

namespace Xmip.Gui.Desktop;

public static class MauiProgram
{
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
        // nothing in JSON. The file is deployed as content next to the app.
        string here = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
        builder.Configuration.AddTomlFile(Path.Combine(here, "xmip.gui.toml"), optional: true, reloadOnChange: true);

        // Relative paths in the config resolve against the repository root in a
        // development build, and against the app's own directory once packaged.
        // A developer runs from a bin folder several levels down, so pointing at
        // the runtime's target/debug needs the repo root, not the exe's folder.
        string basePath = RepositoryRoot(here) ?? here;

        // The one surface every screen reads. Same NativeOperator as the web
        // host: load the runtime's library, read its table; a stand-in answers
        // when it cannot, and says so.
        builder.Services.AddSingleton<IOperatorSurface>(_ =>
        {
            string configured = builder.Configuration["Xmip:RuntimeLibrary"] ?? "xmip_core_runtime.dll";
            string path = Resolve(configured, basePath);

            NativeOperator? native = NativeOperator.Load(path, out string reason);

            if (native is not null)
            {
                string? node = builder.Configuration["Xmip:NodeConfiguration"];

                if (!string.IsNullOrWhiteSpace(node))
                {
                    native.Start(Resolve(node, basePath));
                }

                return native;
            }

            return new SampleOperator(reason);
        });

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    /// <summary>An absolute path is taken as is; a relative one is joined to
    /// <paramref name="basePath"/>.</summary>
    private static string Resolve(string path, string basePath)
    {
        return Path.IsPathRooted(path) ? path : Path.Combine(basePath, path);
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
