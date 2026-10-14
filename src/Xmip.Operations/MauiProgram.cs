using Microsoft.Extensions.Logging;
using Xmip.Abi.Operate;
using Xmip.Gui.Hosting;
using Xmip.Surface;

namespace Xmip.Operations;

/// <summary>
/// The host's start, shared by every platform it is built for. What differs —
/// where its document is, what its paths resolve against, the role, the
/// surfaces and what configures a cluster — is each platform's part:
/// <c>MauiProgram.Desktop.cs</c> on Windows, <c>MauiProgram.Phone.cs</c> on
/// Android and iOS, one of them compiled into any build (ADR-0014, amendment
/// 2026-10-14).
/// </summary>
public static partial class MauiProgram
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

        // The platform's part reads its document and registers the role and
        // the surfaces every screen reads; it answers where this host audits.
        ProgramAudit audit = Platform(builder);

        // Everything this host does and every failure, audited through the
        // audit capability (ADR-0062): every error the host logs is a record,
        // and so is every exception nothing handled.
        audit.WatchUnhandled();
        builder.Logging.AddProvider(new AuditLoggerProvider(audit));
        builder.Services.AddSingleton(audit);

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        MauiApp app = builder.Build();
        audit.Record("start", AuditPhase.Begin, AuditSeverity.Information);

        return app;
    }

    /// <summary>The platform's part of the start: its document read into
    /// <paramref name="builder"/>'s configuration, its role and surfaces
    /// registered, and the audit it records through.</summary>
    private static partial ProgramAudit Platform(MauiAppBuilder builder);
}
