using System.Collections.Generic;
using SkiaSharp;
using Svg.Skia.TypefaceProviders;

namespace Svg.Skia
{
    public static class SKSvgSettings
    {
        public static SKAlphaType s_alphaType = SKAlphaType.Unpremul;

        public static SKColorType s_colorType = SKImageInfo.PlatformColorType;

        public static readonly SKColorSpace s_srgbLinear = SKColorSpace.CreateRgb(SKColorSpaceTransferFn.Linear, SKColorSpaceXyz.Srgb); // SKColorSpace.CreateSrgbLinear();

        public static readonly SKColorSpace s_srgb = SKColorSpace.CreateRgb(SKColorSpaceTransferFn.Srgb, SKColorSpaceXyz.Srgb); // SKColorSpace.CreateSrgb();

        public static readonly IList<ITypefaceProvider>? s_typefaceProviders = new List<ITypefaceProvider>()
        {
            new FontManagerTypefacerovider(),
            new DefaultTypefaceProvider()
        };
    }
}
