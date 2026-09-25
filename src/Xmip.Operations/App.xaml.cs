using Microsoft.AspNetCore.Components.WebView.Maui;
using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Operations;

public partial class App : Application
{
    private readonly ProgramAudit _audit;

    public App(ProgramAudit audit)
    {
        _audit = audit;
        InitializeComponent();
    }

    /// <summary>The one window: a BlazorWebView over the shared screens. The
    /// template wrote this as a MainPage.xaml; a page holding one control is
    /// three lines, not a file.</summary>
    protected override Window CreateWindow(IActivationState? activationState)
    {
        BlazorWebView view = new() { HostPage = "wwwroot/index.html" };
        view.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(Components.Routes),
        });

        Window window = new(new ContentPage { Content = view }) { Title = "Xmip Operations" };

        // The desktop stops when its one window goes (ADR-0062: what a
        // program started and stopped).
        window.Destroying += (_, _) =>
            _audit.Record("stop", AuditPhase.Finished, AuditSeverity.Information);

        return window;
    }
}
