using Android.App;
using Android.Runtime;
using Uno.UI.Hosting;

namespace UnoSvgSkiaSample.Droid;

[Application(
    Label = "UnoSvgSkiaSample",
    LargeHeap = true,
    HardwareAccelerated = true)]
public sealed class Application : Uno.UI.Runtime.Android.NativeApplication
{
    public Application(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override UnoPlatformHost CreateHost() =>
        UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseAndroid()
            .Build();
}
