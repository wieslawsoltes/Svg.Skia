using Svg.Editor.Skia.Uno;
using Uno.Foundation;
using Uno.UI;
using Uno.UI.Xaml.Media;
using Windows.UI.Text;
using FontWeights = Microsoft.UI.Text.FontWeights;

namespace UnoDraw;

public sealed partial class App
{
    partial void ConfigurePlatformWorkspace(SvgEditorWorkspace workspace)
    {
        if (Rendering.WebGpuSkiaCanvasBridge.UseSingleCanvas)
            workspace.Surface.UseSingleCanvasRendering(Rendering.WebGpuSkiaCanvasBridge.ReportSingleCanvasInitialization);
    }

    async partial void OnPlatformWindowReady()
    {
        try
        {
            // Uno's text fonts load independently of document.fonts. Await the
            // workspace's actual families before either backend exposes its first frame.
            var textFamily = FeatureConfiguration.Font.DefaultTextFontFamily;
            var fonts = new[]
            {
                (Name: "regular text", Family: textFamily, Weight: FontWeights.Normal),
                (Name: "semibold text", Family: textFamily, Weight: FontWeights.SemiBold),
                (Name: "bold text", Family: textFamily, Weight: FontWeights.Bold),
                (Name: "symbols", Family: FeatureConfiguration.Font.SymbolsFont, Weight: FontWeights.Normal)
            };
            var loaded = await Task.WhenAll(fonts.Select(font => FontFamilyHelper.PreloadAsync(
                font.Family, font.Weight, FontStretch.Normal, FontStyle.Normal)));
            for (var index = 0; index < loaded.Length; index++)
            {
                if (!loaded[index])
                    Console.Error.WriteLine($"UnoDraw could not preload {fonts[index].Name} font '{fonts[index].Family}'.");
            }
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"UnoDraw could not preload its workspace fonts: {error}");
        }
        finally
        {
            BrowserStartup.ReleaseInitialFrame();
        }
    }
}

internal static class BrowserStartup
{
    // Uno's browser host requests a resize frame while it is starting. Defer
    // presentation until the window is activated and its text fonts have loaded.
    // Uno 7 shares this scheduler between WebGPU and Skia. Restore it unchanged
    // after startup so rendering errors and subsequent frame scheduling remain visible.
    internal static void DeferInitialFrame() => WebAssemblyRuntime.InvokeJS("""
        (() => {
            const renderer = globalThis.Uno?.UI?.Runtime?.BrowserRenderer;
            if (!renderer || typeof renderer.invalidate !== 'function') return '';
            const original = renderer.invalidate;
            const pending = new Set();
            let ready = false;
            renderer.invalidate = function(instance) {
                if (ready) return original.call(renderer, instance);
                pending.add(instance);
            };
            globalThis.__unoDrawReleaseInitialFrame = () => {
                ready = true;
                renderer.invalidate = original;
                delete globalThis.__unoDrawReleaseInitialFrame;
                for (const instance of pending) original.call(renderer, instance);
                pending.clear();
            };
            return '';
        })()
        """);

    internal static void ReleaseInitialFrame() => WebAssemblyRuntime.InvokeJS("globalThis.__unoDrawReleaseInitialFrame?.(); ''");
}
