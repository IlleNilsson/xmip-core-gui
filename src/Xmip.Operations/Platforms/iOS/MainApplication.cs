using Foundation;

namespace Xmip.Operations;

/// <summary>The application on iOS: it starts the host the way every
/// platform does. Registered as <c>AppDelegate</c>, the name iOS looks for;
/// the C# name says what it is. Built for the simulator by the CI's macOS
/// machine, not signed, not run (ADR-0014, amendment 2026-10-14).</summary>
[Register("AppDelegate")]
public class MainApplication : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp()
    {
        return MauiProgram.CreateMauiApp();
    }
}
