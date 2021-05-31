using System.Collections.Generic;
using Svg.Skia.TypefaceProviders;

namespace Svg.Skia
{
    public static class SKSvgSettings
    {
        public static SkiaSharp.SKAlphaType s_alphaType = SkiaSharp.SKAlphaType.Unpremul;

        public static SkiaSharp.SKColorType s_colorType = SkiaSharp.SKImageInfo.PlatformColorType;

        public static readonly SkiaSharp.SKColorSpace s_srgbLinear = SkiaSharp.SKColorSpace.CreateRgb(SkiaSharp.SKColorSpaceTransferFn.Linear, SkiaSharp.SKColorSpaceXyz.Srgb); // SkiaSharp.SKColorSpace.CreateSrgbLinear();

        public static readonly SkiaSharp.SKColorSpace s_srgb = SkiaSharp.SKColorSpace.CreateRgb(SkiaSharp.SKColorSpaceTransferFn.Srgb, SkiaSharp.SKColorSpaceXyz.Srgb); // SkiaSharp.SKColorSpace.CreateSrgb();

        public static readonly IList<ITypefaceProvider>? s_typefaceProviders = new List<ITypefaceProvider>
        {
            new FontManagerTypefaceProvider(),
            new DefaultTypefaceProvider()
        };
    }
}
