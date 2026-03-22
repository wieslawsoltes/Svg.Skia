using Android.App;
using Android.Runtime;

namespace UnoSvgEditorSample.Droid;

[Application(
    Label = "UnoSvgEditorSample",
    LargeHeap = true,
    HardwareAccelerated = true)]
public sealed class Application : Microsoft.UI.Xaml.NativeApplication
{
    public Application(IntPtr javaReference, JniHandleOwnership transfer)
        : base(() => new App(), javaReference, transfer)
    {
    }
}
