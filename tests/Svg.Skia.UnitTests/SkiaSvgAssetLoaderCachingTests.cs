#pragma warning disable CS0618 // Shim paint keeps deprecated SKPaint text/typeface surface for compatibility

using System;
using System.Linq;
using ShimSkiaSharp;
using Svg.Skia;
using Svg.Skia.TypefaceProviders;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SkiaSvgAssetLoaderCachingTests
{
    [Fact]
    public void MeasureText_RecomputesAfterPaintMutation()
    {
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));
        var paint = CreateTextPaint(12f);

        var firstBounds = default(SKRect);
        var firstAdvance = assetLoader.MeasureText("Scale Me", paint, ref firstBounds);

        var repeatedBounds = new SKRect(-1f, -1f, -1f, -1f);
        var repeatedAdvance = assetLoader.MeasureText("Scale Me", paint, ref repeatedBounds);

        Assert.Equal(firstAdvance, repeatedAdvance, 3);
        Assert.Equal(firstBounds.Left, repeatedBounds.Left, 3);
        Assert.Equal(firstBounds.Top, repeatedBounds.Top, 3);
        Assert.Equal(firstBounds.Right, repeatedBounds.Right, 3);
        Assert.Equal(firstBounds.Bottom, repeatedBounds.Bottom, 3);

        paint.TextSize = 36f;

        var mutatedBounds = default(SKRect);
        var mutatedAdvance = assetLoader.MeasureText("Scale Me", paint, ref mutatedBounds);

        Assert.True(mutatedAdvance > firstAdvance * 2f);
        Assert.True(mutatedBounds.Width > firstBounds.Width * 2f);
    }

    [Fact]
    public void FindTypefaces_ReturnsIndependentResultsAndRecomputesAfterPaintMutation()
    {
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));
        var paint = CreateTextPaint(14f);
        const string text = "Bold Text 20px";

        var first = assetLoader.FindTypefaces(text, paint);
        var firstAdvance = first.Sum(static span => span.Advance);

        first.Clear();

        var repeated = assetLoader.FindTypefaces(text, paint);
        var repeatedAdvance = repeated.Sum(static span => span.Advance);

        Assert.NotEmpty(repeated);
        Assert.Equal(firstAdvance, repeatedAdvance, 3);

        paint.TextSize = 42f;

        var mutated = assetLoader.FindTypefaces(text, paint);
        var mutatedAdvance = mutated.Sum(static span => span.Advance);

        Assert.Equal(repeated.Count, mutated.Count);
        Assert.True(mutatedAdvance > repeatedAdvance * 2f);
    }

    [Fact]
    public void GetFontMetrics_RecomputesAfterPaintMutation()
    {
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));
        var paint = CreateTextPaint(12f);

        var firstMetrics = assetLoader.GetFontMetrics(paint);
        var repeatedMetrics = assetLoader.GetFontMetrics(paint);

        Assert.Equal(firstMetrics.Ascent, repeatedMetrics.Ascent, 3);
        Assert.Equal(firstMetrics.Descent, repeatedMetrics.Descent, 3);

        paint.TextSize = 36f;

        var mutatedMetrics = assetLoader.GetFontMetrics(paint);

        Assert.True(Math.Abs(mutatedMetrics.Ascent) > Math.Abs(firstMetrics.Ascent) * 2f);
        Assert.True(Math.Abs(mutatedMetrics.Descent) > Math.Abs(firstMetrics.Descent) * 2f);
    }

    [Fact]
    public void TryShapeGlyphRun_RecomputesAfterPaintMutation()
    {
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));
        var paint = CreateTextPaint(12f);
        const string text = "Scale Me";

        Assert.True(assetLoader.TryShapeGlyphRun(text, paint, out var firstRun));
        Assert.True(assetLoader.TryShapeGlyphRun(text, paint, out var repeatedRun));

        Assert.Equal(firstRun.Advance, repeatedRun.Advance, 3);

        paint.TextSize = 36f;

        Assert.True(assetLoader.TryShapeGlyphRun(text, paint, out var mutatedRun));
        Assert.True(mutatedRun.Advance > firstRun.Advance * 2f);
    }

    [Fact]
    public void TextMeasurementCacheKey_IsShared_ForFreshDefaultPathLoaders()
    {
        var firstSettings = new SKSvgSettings
        {
            EnableSvgFonts = false,
            EnableTextReferences = false
        };
        var secondSettings = new SKSvgSettings
        {
            EnableSvgFonts = false,
            EnableTextReferences = false
        };

        var firstLoader = (Svg.Model.ISvgTextMeasurementCacheKeyProvider)new SkiaSvgAssetLoader(new SkiaModel(firstSettings));
        var secondLoader = (Svg.Model.ISvgTextMeasurementCacheKeyProvider)new SkiaSvgAssetLoader(new SkiaModel(secondSettings));

        Assert.Equal(firstLoader.TextMeasurementCacheKey, secondLoader.TextMeasurementCacheKey);
    }

    [Fact]
    public void TextMeasurementCacheKey_RemainsInstanceScoped_ForCustomProviderLoaders()
    {
        var firstSettings = new SKSvgSettings
        {
            TypefaceProviders = [new FontManagerTypefaceProvider()]
        };
        var secondSettings = new SKSvgSettings
        {
            TypefaceProviders = [new FontManagerTypefaceProvider()]
        };

        var firstLoader = (Svg.Model.ISvgTextMeasurementCacheKeyProvider)new SkiaSvgAssetLoader(new SkiaModel(firstSettings));
        var secondLoader = (Svg.Model.ISvgTextMeasurementCacheKeyProvider)new SkiaSvgAssetLoader(new SkiaModel(secondSettings));

        Assert.NotEqual(firstLoader.TextMeasurementCacheKey, secondLoader.TextMeasurementCacheKey);
    }

    private static SKPaint CreateTextPaint(float textSize)
    {
        return new SKPaint
        {
            TextSize = textSize,
            Typeface = SKTypeface.FromFamilyName(
                "sans-serif",
                SKFontStyleWeight.Bold,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright)
        };
    }
}

#pragma warning restore CS0618
