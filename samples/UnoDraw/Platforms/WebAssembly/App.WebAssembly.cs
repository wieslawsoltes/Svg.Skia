using Uno.Foundation;
using Uno.UI;
using Uno.UI.Xaml.Media;
using Windows.UI.Text;
using FontWeights = Microsoft.UI.Text.FontWeights;

namespace UnoDraw;

public sealed partial class App
{
    async partial void OnPlatformWindowReady()
    {
        try
        {
            // Native Skia fonts load independently of document.fonts. Await the
            // workspace's actual families before exposing its first frame.
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
    // Uno 6.5 schedules a resize frame while its browser host is still starting,
    // before Window.CurrentSafe exists. Defer the startup interval until the
    // window is activated and its native fonts have loaded. Restore the original
    // scheduler; rendering errors remain visible and later frames use Uno's WebGL path.
    internal static void DeferInitialFrame() => WebAssemblyRuntime.InvokeJS("""
        (() => {
            const renderer = globalThis.Uno?.UI?.Runtime?.Skia?.BrowserRenderer;
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
