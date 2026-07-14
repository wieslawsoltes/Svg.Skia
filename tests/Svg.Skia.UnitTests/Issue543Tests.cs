using System;
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg.Skia.TypefaceProviders;
using Svg.Skia.UnitTests.Common;
using Xunit;

namespace Svg.Skia.UnitTests;

public class Issue543Tests : SvgUnitTest
{
    [Fact]
    public void VariableFontProviderAppliesRequestedWeightAxis()
    {
        using var typeface = SkiaSharp.SKTypeface.FromFile(GetFontsPath("RobotoFlex.subset.ttf"));
        Assert.NotNull(typeface);

        var settings = new SKSvgSettings
        {
            TypefaceProviders = new List<ITypefaceProvider>
            {
                new AliasTypefaceProvider("Issue543-Variable", typeface!)
            }
        };
        var model = new SkiaModel(settings);
        var paint = new SKPaint
        {
            TextSize = 117.333f,
            Typeface = SKTypeface.FromFamilyName(
                "Issue543-Variable",
                SKFontStyleWeight.Black,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright)
        };

        using var skFont = model.ToSKFont(paint);

        Assert.NotNull(skFont.Typeface);
        Assert.Equal((int)SkiaSharp.SKFontStyleWeight.Black, skFont.Typeface!.FontWeight);
        var weight = Assert.Single(
            skFont.Typeface.VariationDesignPosition,
            static coordinate => coordinate.Axis == SkiaSharp.SKFourByteTag.Parse("wght"));
        Assert.Equal(900f, weight.Value);
        Assert.False(skFont.Embolden);

        var loader = new SkiaSvgAssetLoader(model);
        Assert.True(loader.TryShapeGlyphRun("EK", paint, out var heavyRun));

        paint.Typeface = SKTypeface.FromFamilyName(
            "Issue543-Variable",
            SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            SKFontStyleSlant.Upright);
        Assert.True(loader.TryShapeGlyphRun("EK", paint, out var normalRun));
        Assert.True(heavyRun.Advance > normalRun.Advance);
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
