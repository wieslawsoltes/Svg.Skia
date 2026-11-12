using Android.Runtime;
using Uno.UI.Hosting;

namespace UnoDraw.Droid;

[global::Android.App.Application(
    Label = "@string/ApplicationName", Icon = "@mipmap/icon",
    HardwareAccelerated = true, Theme = "@style/Theme.App.Starting")]
public sealed class Application : Uno.UI.Runtime.Android.NativeApplication
{
    public Application(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer) { }

    protected override UnoPlatformHost CreateHost() =>
        UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseAndroid()
            .Build();
}
