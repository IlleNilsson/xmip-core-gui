using Microsoft.Extensions.Configuration;
using Xmip.Gui.Surface;

namespace Xmip.Gui.Desktop.Configuration;

/// <summary>
/// The commands the desktop can run that a browser cannot: loading the native
/// runtime and asking it to validate or start a node configuration. A browser is
/// sandboxed — no native library, no local file to hand it — which is exactly
/// why configuration and node control live in the desktop GUI, not the web one
/// (ADR-0014). It loads the runtime lazily and keeps it; when it cannot, it says
/// so, and says that only the desktop could even try.
/// </summary>
public sealed class RuntimeCommands(IConfiguration configuration)
{
    private NativeOperator? _runtime;
    private string _reason = "";

    /// <summary>Validate a node configuration through the native runtime.</summary>
    public string Validate(string configurationPath)
    {
        return Runtime() is { } runtime
            ? runtime.Validate(configurationPath)
            : $"no runtime loaded ({_reason}) — only the desktop can even attempt this; a browser cannot";
    }

    /// <summary>Start a node from its configuration through the native runtime.</summary>
    public string Start(string configurationPath)
    {
        return Runtime() is { } runtime
            ? runtime.Start(configurationPath)
            : $"no runtime loaded ({_reason}) — only the desktop can even attempt this; a browser cannot";
    }

    private NativeOperator? Runtime()
    {
        if (_runtime is not null)
        {
            return _runtime;
        }

        string configured = configuration["Xmip:RuntimeLibrary"] ?? "xmip_core_runtime.dll";
        string path = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(AppContext.BaseDirectory, configured);

        _runtime = NativeOperator.Load(path, out _reason);
        return _runtime;
    }
}
