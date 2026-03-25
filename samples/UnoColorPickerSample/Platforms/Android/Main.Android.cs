using Android.App;
using Android.Runtime;

namespace UnoColorPickerSample.Droid;

[Application(
    Label = "UnoColorPickerSample",
    LargeHeap = true,
    HardwareAccelerated = true)]
public sealed class Application : Microsoft.UI.Xaml.NativeApplication
{
    public Application(IntPtr javaReference, JniHandleOwnership transfer)
        : base(() => new App(), javaReference, transfer)
    {
    }
}
