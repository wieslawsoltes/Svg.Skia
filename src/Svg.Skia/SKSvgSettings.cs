﻿using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

namespace Svg.Skia
{
    public static class SKSvgSettings
    {
        public static SKAlphaType s_alphaType = SKAlphaType.Unpremul;

        public static SKColorType s_colorType = SKImageInfo.PlatformColorType;

        public static SKColorSpace s_srgbLinear = SKColorSpace.CreateRgb(SKColorSpaceTransferFn.Linear, SKColorSpaceXyz.Srgb); // SKColorSpace.CreateSrgbLinear();

        public static SKColorSpace s_srgb = SKColorSpace.CreateRgb(SKColorSpaceTransferFn.Srgb, SKColorSpaceXyz.Srgb); // SKColorSpace.CreateSrgb();

        public static CultureInfo? s_systemLanguageOverride = null;

        public static IList<ITypefaceProvider> s_typefaceProviders = new List<ITypefaceProvider>()
        {
            new FontManagerTypefacerovider(),
            new DefaultTypefaceProvider()
        };
    }
}
