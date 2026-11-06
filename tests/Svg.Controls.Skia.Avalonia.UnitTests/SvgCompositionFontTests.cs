using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using SkiaSharp;
using Svg.Skia;
using Svg.Skia.TypefaceProviders;
using Xunit;

namespace Avalonia.Svg.Skia.UnitTests;

[Collection("SvgSource default settings")]
public class SvgCompositionFontTests
{
    [AvaloniaFact]
    public async Task NativeCompositionUsesLoadedSourceFontSettingsAfterDefaultsChange()
    {
        var originalSettings = SvgSource.DefaultSettings.Clone();
        var sourceProvider = new SnapshotTypefaceProvider(SKFontStyleWeight.Normal);
        var replacementProvider = new SnapshotTypefaceProvider(SKFontStyleWeight.Bold);
        Window? window = null;
        Svg? control = null;

        try
        {
            SvgSource.ConfigureDefaultSettings(settings =>
                settings.TypefaceProviders = new List<ITypefaceProvider> { sourceProvider });
            using var source = SvgSource.LoadFromSvg("""
                <svg xmlns="http://www.w3.org/2000/svg" width="260" height="80">
                  <text x="10" y="42" font-family="SvgSettingsSnapshot" font-size="32">MMWW Inter 565</text>
                  <rect x="1" y="68" width="4" height="4" fill="blue">
                    <animate attributeName="x" from="1" to="2" dur="2s" repeatCount="indefinite" />
                  </rect>
                </svg>
                """);
            Assert.True(source.Svg!.HasAnimations);
            Assert.True(source.Svg.SupportsNativeComposition);

            control = new Svg(new Uri("avares://Svg.Controls.Skia.Avalonia.UnitTests/"))
            {
                SvgSource = source,
                AnimationBackend = SvgAnimationHostBackend.Manual,
                AnimationPlaybackRate = 0,
                Stretch = Stretch.None,
                Width = 260,
                Height = 80
            };
            window = new Window
            {
                Content = control,
                Width = 260,
                Height = 80,
                Background = Brushes.White
            };
            window.Show();
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (control.Picture is null && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            }
            Assert.NotNull(control.Picture);
            Assert.True(sourceProvider.Calls > 0);

            // The control has already cloned and loaded the source. Converting its native
            // composition layers must keep that snapshot instead of consulting new defaults.
            SvgSource.ConfigureDefaultSettings(settings =>
                settings.TypefaceProviders = new List<ITypefaceProvider> { replacementProvider });
            control.AnimationBackend = SvgAnimationHostBackend.NativeComposition;

            using var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            Assert.Equal(SvgAnimationHostBackend.NativeComposition, control.ActualAnimationBackend);
            Assert.Equal(0, replacementProvider.Calls);
            using var stream = new MemoryStream();
            frame.Save(stream);
            stream.Position = 0;
            using var bitmap = SKBitmap.Decode(stream);
            Assert.NotNull(bitmap);
            var textPixels = 0;
            for (var y = 0; y < Math.Min(bitmap.Height, 60); y++)
            {
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    if (pixel.Red < 100 && pixel.Green < 100 && pixel.Blue < 100)
                    {
                        textPixels++;
                    }
                }
            }
            Assert.True(textPixels > 100, $"Expected rendered text, found {textPixels} dark pixels.");
        }
        finally
        {
            window?.Close();
            if (control is not null)
            {
                control.SvgSource = null;
            }
            SvgSource.ConfigureDefaultSettings(settings => originalSettings.CopyTo(settings));
        }
    }

    private sealed class SnapshotTypefaceProvider(SKFontStyleWeight weight) : ITypefaceProvider
    {
        private readonly AvaloniaTypefaceProvider _provider = new(false);
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public SKTypeface? FromFamilyName(string fontFamily, SKFontStyleWeight fontWeight, SKFontStyleWidth fontWidth, SKFontStyleSlant fontStyle)
        {
            Interlocked.Increment(ref _calls);
            return fontFamily == "SvgSettingsSnapshot"
                ? _provider.FromFamilyName("fonts:Inter#Inter", weight, fontWidth, fontStyle)
                : null;
        }
    }
}
