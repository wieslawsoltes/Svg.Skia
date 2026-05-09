#pragma warning disable CS0618 // Typeface and FakeBoldText are deprecated on SKPaint; shim keeps the legacy surface for compatibility

using System;
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg.Skia.TypefaceProviders;
using Svg.Skia.UnitTests.Common;
using Xunit;

namespace Svg.Skia.UnitTests;

public class Issue429Tests : SvgUnitTest
{
    [Fact]
    public void CustomProviderTypefaceDoesNotTriggerSyntheticBold()
    {
        using var suppliedTypeface = SkiaSharp.SKTypeface.FromFile(GetFontsPath("NotoSans-Regular.ttf"));
        Assert.NotNull(suppliedTypeface);

        var settings = new SKSvgSettings
        {
            TypefaceProviders = new List<ITypefaceProvider>
            {
                new AliasTypefaceProvider("Issue429-Regular", suppliedTypeface!)
            }
        };
        var model = new SkiaModel(settings);
        var paint = new SKPaint
        {
            Typeface = SKTypeface.FromFamilyName(
                "Issue429-Regular",
                SKFontStyleWeight.ExtraBlack,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright)
        };

        using var skPaint = model.ToSKPaint(paint);

        Assert.NotNull(skPaint);
        Assert.Equal(suppliedTypeface!.Handle, skPaint!.Typeface?.Handle);
        Assert.True(skPaint.Typeface!.FontWeight < (int)SkiaSharp.SKFontStyleWeight.ExtraBlack);
        Assert.False(skPaint.FakeBoldText);
    }

    private sealed class AliasTypefaceProvider : ITypefaceProvider
    {
        private readonly string _familyName;
        private readonly SkiaSharp.SKTypeface _typeface;

        public AliasTypefaceProvider(string familyName, SkiaSharp.SKTypeface typeface)
        {
            _familyName = familyName;
            _typeface = typeface;
        }

        public SkiaSharp.SKTypeface? FromFamilyName(
            string fontFamily,
            SkiaSharp.SKFontStyleWeight fontWeight,
            SkiaSharp.SKFontStyleWidth fontWidth,
            SkiaSharp.SKFontStyleSlant fontStyle)
        {
            return string.Equals(fontFamily, _familyName, StringComparison.Ordinal)
                ? _typeface
                : null;
        }
    }
}

#pragma warning restore CS0618
