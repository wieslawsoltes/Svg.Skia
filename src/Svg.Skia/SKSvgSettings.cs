// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Collections.Generic;
using Svg.Skia.TypefaceProviders;

namespace Svg.Skia;

public class SKSvgSettings
{
    public static ISKSvgJavaScriptRuntimeFactory? DefaultJavaScriptRuntimeFactory { get; set; }

    public SkiaSharp.SKAlphaType AlphaType { get; set; }

    public SkiaSharp.SKColorType ColorType { get; set; }

    public SkiaSharp.SKColorSpace SrgbLinear { get; set; }

    public SkiaSharp.SKColorSpace Srgb { get; set; }

    public IList<ITypefaceProvider>? TypefaceProviders { get; set; }

    public SkiaSharp.SKRect? StandaloneViewport { get; set; }

    public bool EnableSvgFonts { get; set; }

    public bool EnableTextReferences { get; set; }

    public bool EnableJavaScript { get; set; }

    public bool EnableExternalJavaScript { get; set; }

    public int JavaScriptTimeoutMilliseconds { get; set; }

    public int JavaScriptMaxStatements { get; set; }

    public bool ThrowOnJavaScriptError { get; set; }

    public ISKSvgJavaScriptRuntimeFactory? JavaScriptRuntimeFactory { get; set; }

    public SKSvgSettings Clone()
    {
        var clone = new SKSvgSettings();
        CopyTo(clone);
        return clone;
    }

    public void CopyTo(SKSvgSettings target)
    {
        if (target is null)
        {
            throw new System.ArgumentNullException(nameof(target));
        }

        target.AlphaType = AlphaType;
        target.ColorType = ColorType;
        target.SrgbLinear = SrgbLinear;
        target.Srgb = Srgb;
        target.TypefaceProviders = TypefaceProviders is null
            ? null
            : new List<ITypefaceProvider>(TypefaceProviders);
        target.StandaloneViewport = StandaloneViewport;
        target.EnableSvgFonts = EnableSvgFonts;
        target.EnableTextReferences = EnableTextReferences;
        target.EnableJavaScript = EnableJavaScript;
        target.EnableExternalJavaScript = EnableExternalJavaScript;
        target.JavaScriptTimeoutMilliseconds = JavaScriptTimeoutMilliseconds;
        target.JavaScriptMaxStatements = JavaScriptMaxStatements;
        target.ThrowOnJavaScriptError = ThrowOnJavaScriptError;
        target.JavaScriptRuntimeFactory = JavaScriptRuntimeFactory;
    }

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

        StandaloneViewport = null;
        EnableSvgFonts = true;
        EnableTextReferences = true;
        EnableJavaScript = false;
        EnableExternalJavaScript = true;
        JavaScriptTimeoutMilliseconds = 1000;
        JavaScriptMaxStatements = 10000;
        ThrowOnJavaScriptError = false;
    }
}
