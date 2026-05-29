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

    internal IList<ITypefaceProvider>? DocumentTypefaceProviders { get; set; }

    public SkiaSharp.SKRect? StandaloneViewport { get; set; }

    public bool EnableSvgFonts { get; set; }

    public bool EnableTextReferences { get; set; }

    public bool EnableFilterBackgroundInputs { get; set; }

    public bool EnableBrokenImagePlaceholders { get; set; }

    public Svg.ISvgSystemColorProvider? SystemColorProvider { get; set; }

    public bool EnableJavaScript { get; set; }

    public bool EnableTextSelectionRendering { get; set; }

    public SkiaSharp.SKColor TextSelectionColor { get; set; }

    public bool EnableExternalJavaScript { get; set; }

    public int JavaScriptTimeoutMilliseconds { get; set; }

    public int JavaScriptMaxStatements { get; set; }

    public bool ThrowOnJavaScriptError { get; set; }

    public ISKSvgJavaScriptRuntimeFactory? JavaScriptRuntimeFactory { get; set; }

    public ISKSvgNavigationHandler? NavigationHandler { get; set; }

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
        target.DocumentTypefaceProviders = null;
        target.StandaloneViewport = StandaloneViewport;
        target.EnableSvgFonts = EnableSvgFonts;
        target.EnableTextReferences = EnableTextReferences;
        target.EnableFilterBackgroundInputs = EnableFilterBackgroundInputs;
        target.EnableBrokenImagePlaceholders = EnableBrokenImagePlaceholders;
        target.SystemColorProvider = SystemColorProvider;
        target.EnableJavaScript = EnableJavaScript;
        target.EnableTextSelectionRendering = EnableTextSelectionRendering;
        target.TextSelectionColor = TextSelectionColor;
        target.EnableExternalJavaScript = EnableExternalJavaScript;
        target.JavaScriptTimeoutMilliseconds = JavaScriptTimeoutMilliseconds;
        target.JavaScriptMaxStatements = JavaScriptMaxStatements;
        target.ThrowOnJavaScriptError = ThrowOnJavaScriptError;
        target.JavaScriptRuntimeFactory = JavaScriptRuntimeFactory;
        target.NavigationHandler = NavigationHandler;
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
        DocumentTypefaceProviders = null;

        StandaloneViewport = null;
        EnableSvgFonts = true;
        EnableTextReferences = true;
        EnableFilterBackgroundInputs = true;
        EnableBrokenImagePlaceholders = true;
        SystemColorProvider = null;
        EnableJavaScript = false;
        EnableTextSelectionRendering = true;
        TextSelectionColor = new SkiaSharp.SKColor(0x00, 0x80, 0x00, 0xFF);
        EnableExternalJavaScript = true;
        JavaScriptTimeoutMilliseconds = 1000;
        JavaScriptMaxStatements = 10000;
        ThrowOnJavaScriptError = false;
    }
}
