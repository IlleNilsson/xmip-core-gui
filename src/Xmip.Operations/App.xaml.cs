using Microsoft.AspNetCore.Components.WebView.Maui;

namespace Xmip.Operations;

public partial class App : Application
{
    public App()
    {
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

        return new Window(new ContentPage { Content = view }) { Title = "Xmip Operations" };
    }
}
