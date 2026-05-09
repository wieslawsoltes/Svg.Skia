#pragma warning disable CS0618 // Shim paint keeps deprecated SKPaint text/typeface surface for compatibility

using System.Collections.Generic;
using System.Linq;
using ShimSkiaSharp;
using Svg.Skia;
using Svg.Skia.TypefaceProviders;
using Xunit;
using NativeTypeface = SkiaSharp.SKTypeface;
using NativeTypefaceSlant = SkiaSharp.SKFontStyleSlant;
using NativeTypefaceWeight = SkiaSharp.SKFontStyleWeight;
using NativeTypefaceWidth = SkiaSharp.SKFontStyleWidth;

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
    public void SharedCaches_DoNotBypassCustomTypefaceProvidersAcrossModels()
    {
        var firstProvider = new CountingTypefaceProvider();
        var secondProvider = new CountingTypefaceProvider();
        var requestedTypeface = SKTypeface.FromFamilyName(
            "Missing Custom Family",
            SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            SKFontStyleSlant.Upright);

        var firstModel = new SkiaModel(new SKSvgSettings
        {
            TypefaceProviders = new List<ITypefaceProvider> { firstProvider }
        });
        var secondModel = new SkiaModel(new SKSvgSettings
        {
            TypefaceProviders = new List<ITypefaceProvider> { secondProvider }
        });

        firstModel.ToSKTypeface(requestedTypeface);
        secondModel.ToSKTypeface(requestedTypeface);

        Assert.True(firstProvider.CallCount > 0);
        Assert.True(secondProvider.CallCount > 0);
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

    private sealed class CountingTypefaceProvider : ITypefaceProvider
    {
        public int CallCount { get; private set; }

        public NativeTypeface? FromFamilyName(
            string fontFamily,
            NativeTypefaceWeight fontWeight,
            NativeTypefaceWidth fontWidth,
            NativeTypefaceSlant fontStyle)
        {
            CallCount++;
            return null;
        }
    }
}

#pragma warning restore CS0618
