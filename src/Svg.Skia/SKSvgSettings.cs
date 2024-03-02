using System.Collections.Generic;
using Svg.Skia.TypefaceProviders;

namespace Svg.Skia;

public class SKSvgSettings
{
    public SkiaSharp.SKAlphaType AlphaType { get; set; } = SkiaSharp.SKAlphaType.Unpremul;

    public SkiaSharp.SKColorType ColorType { get; set; } = SkiaSharp.SKImageInfo.PlatformColorType;

    public SkiaSharp.SKColorSpace SrgbLinear { get; set; } = SkiaSharp.SKColorSpace.CreateRgb(SkiaSharp.SKColorSpaceTransferFn.Linear, SkiaSharp.SKColorSpaceXyz.Srgb); // SkiaSharp.SKColorSpace.CreateSrgbLinear();

    public SkiaSharp.SKColorSpace Srgb { get; set; } = SkiaSharp.SKColorSpace.CreateRgb(SkiaSharp.SKColorSpaceTransferFn.Srgb, SkiaSharp.SKColorSpaceXyz.Srgb); // SkiaSharp.SKColorSpace.CreateSrgb();

    public IList<ITypefaceProvider>? TypefaceProviders  { get; set; } = new List<ITypefaceProvider>
    {
        new FontManagerTypefaceProvider(),
        new DefaultTypefaceProvider()
    };
}
