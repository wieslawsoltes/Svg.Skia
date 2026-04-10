#pragma warning disable CS0618 // Typeface and FakeBoldText are deprecated on SKPaint; shim keeps the legacy surface for compatibility

using ShimSkiaSharp;
using Svg.Skia;
using Xunit;

namespace Svg.Skia.UnitTests;

public class Issue405Tests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SansSerifBold_ResolvesSingleTypefaceSpan(bool enableSvgFonts)
    {
        var settings = new SKSvgSettings { EnableSvgFonts = enableSvgFonts };
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(settings));
        var paint = new SKPaint
        {
            Typeface = SKTypeface.FromFamilyName(
                "sans-serif",
                SKFontStyleWeight.Bold,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright)
        };

        var spans = assetLoader.FindTypefaces("Bold Text 20px", paint);

        Assert.Single(spans);
        var span = spans[0];
        Assert.NotNull(span.Typeface);
        Assert.True(span.Typeface!.FontWeight >= SKFontStyleWeight.SemiBold,
            "Expected resolved typeface to be semi-bold or heavier.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FakeBoldMatchesDesiredWeight(bool enableSvgFonts)
    {
        var settings = new SKSvgSettings { EnableSvgFonts = enableSvgFonts };
        var model = new SkiaModel(settings);
        var paint = new SKPaint
        {
            Typeface = SKTypeface.FromFamilyName(
                "sans-serif",
                SKFontStyleWeight.ExtraBlack,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright)
        };

        using var localPaint = model.ToSKPaint(paint);
        Assert.NotNull(localPaint);

        var desiredWeight = (int)SkiaSharp.SKFontStyleWeight.ExtraBlack;
        var actualWeight = localPaint!.Typeface?.FontWeight ?? 0;
        var shouldFakeBold = actualWeight < desiredWeight;

        Assert.Equal(shouldFakeBold, localPaint.FakeBoldText);
    }
}

#pragma warning restore CS0618
