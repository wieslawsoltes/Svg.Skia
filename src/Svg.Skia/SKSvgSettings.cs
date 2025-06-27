// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Collections.Generic;
using Svg.Skia.TypefaceProviders;

namespace Svg.Skia;

public class SKSvgSettings
{
    public SkiaSharp.SKAlphaType AlphaType { get; set; }

    public SkiaSharp.SKColorType ColorType { get; set; }

    public SkiaSharp.SKColorSpace SrgbLinear { get; set; }

    public SkiaSharp.SKColorSpace Srgb { get; set; }

    public IList<ITypefaceProvider>? TypefaceProviders  { get; set; }

    public SKSvgSettings()
    {
        AlphaType = SkiaSharp.SKAlphaType.Unpremul;

        ColorType = SkiaSharp.SKImageInfo.PlatformColorType;

        SrgbLinear = SkiaSharp.SKColorSpace.CreateRgb(SkiaSharp.SKColorSpaceTransferFn.Linear, SkiaSharp.SKColorSpaceXyz.Srgb); // SkiaSharp.SKColorSpace.CreateSrgbLinear();

        Srgb = SkiaSharp.SKColorSpace.CreateRgb(SkiaSharp.SKColorSpaceTransferFn.Srgb, SkiaSharp.SKColorSpaceXyz.Srgb); // SkiaSharp.SKColorSpace.CreateSrgb();

        TypefaceProviders = new List<ITypefaceProvider>
        {
            new FontManagerTypefaceProvider(),
            new DefaultTypefaceProvider()
        };
    }
}
