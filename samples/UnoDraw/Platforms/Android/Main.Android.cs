using Android.Runtime;

namespace UnoDraw.Droid;

[global::Android.App.Application(
    Label = "@string/ApplicationName", Icon = "@mipmap/icon",
    HardwareAccelerated = true, Theme = "@style/Theme.App.Starting")]
public sealed class Application : Microsoft.UI.Xaml.NativeApplication
{
    public Application(IntPtr javaReference, JniHandleOwnership transfer)
        : base(() => new App(), javaReference, transfer) { }
}
