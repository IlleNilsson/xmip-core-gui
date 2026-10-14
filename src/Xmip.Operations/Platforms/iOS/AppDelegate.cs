using Foundation;

namespace Xmip.Operations;

/// <summary>The application on iOS: it starts the host the way every
/// platform does. Written, not built or tested: there is no Mac at hand
/// (ADR-0014, amendment 2026-10-14).</summary>
[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp()
    {
        return MauiProgram.CreateMauiApp();
    }
}
