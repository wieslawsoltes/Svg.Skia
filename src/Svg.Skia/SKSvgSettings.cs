// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Collections.Generic;
using Svg.Skia.TypefaceProviders;

namespace Svg.Skia;

public class SKSvgSettings
{
    private SkiaSharp.SKColorSpace? _srgbLinear;
    private SkiaSharp.SKColorSpace? _srgb;
    private IList<ITypefaceProvider>? _typefaceProviders;

    public SkiaSharp.SKAlphaType AlphaType { get; set; }

    public SkiaSharp.SKColorType ColorType { get; set; }

    public SkiaSharp.SKColorSpace SrgbLinear
    {
        get => _srgbLinear ??= CreateSrgbLinearColorSpace();
        set => _srgbLinear = value;
    }

    public SkiaSharp.SKColorSpace Srgb
    {
        get => _srgb ??= CreateSrgbColorSpace();
        set => _srgb = value;
    }

    public IList<ITypefaceProvider>? TypefaceProviders
    {
        get => _typefaceProviders ??= CreateDefaultTypefaceProviders();
        set => _typefaceProviders = value;
    }

    public SkiaSharp.SKRect? StandaloneViewport { get; set; }

    public bool EnableSvgFonts { get; set; }

    public bool EnableTextReferences { get; set; }

    internal IList<ITypefaceProvider>? ConfiguredTypefaceProviders => _typefaceProviders;

    public SKSvgSettings()
    {
        AlphaType = SkiaSharp.SKAlphaType.Unpremul;

        ColorType = SkiaSharp.SKImageInfo.PlatformColorType;

        StandaloneViewport = null;
        EnableSvgFonts = true;
        EnableTextReferences = true;
    }

    private static SkiaSharp.SKColorSpace CreateSrgbLinearColorSpace()
    {
        return SkiaSharp.SKColorSpace.CreateRgb(SkiaSharp.SKColorSpaceTransferFn.Linear, SkiaSharp.SKColorSpaceXyz.Srgb);
    }

    private static SkiaSharp.SKColorSpace CreateSrgbColorSpace()
    {
        return SkiaSharp.SKColorSpace.CreateRgb(SkiaSharp.SKColorSpaceTransferFn.Srgb, SkiaSharp.SKColorSpaceXyz.Srgb);
    }

    private static IList<ITypefaceProvider> CreateDefaultTypefaceProviders()
    {
        return new List<ITypefaceProvider>
        {
            new FontManagerTypefaceProvider(),
            new DefaultTypefaceProvider()
        };
    }
}
