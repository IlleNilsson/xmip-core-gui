using Android.App;
using Android.Runtime;

namespace Xmip.Operations;

/// <summary>The application on Android: it starts the host the way every
/// platform does, through <see cref="MauiProgram.CreateMauiApp"/>.</summary>
[Application]
public class MainApplication(IntPtr handle, JniHandleOwnership ownership)
    : MauiApplication(handle, ownership)
{
    protected override MauiApp CreateMauiApp()
    {
        return MauiProgram.CreateMauiApp();
    }
}
