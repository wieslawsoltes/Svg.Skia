#pragma warning disable CS0618 // Typeface and FakeBoldText are deprecated on SKPaint; shim keeps the legacy surface for compatibility

using ShimSkiaSharp;
using Svg.Skia;
using Xunit;

namespace Svg.Skia.UnitTests;

public class Issue405Tests
{
    private readonly SkiaModel _model = new(new SKSvgSettings());
    private readonly SkiaSvgAssetLoader _assetLoader;

    public Issue405Tests()
    {
        _assetLoader = new SkiaSvgAssetLoader(_model);
    }

    [Fact]
    public void SansSerifBold_ResolvesSingleTypefaceSpan()
    {
        var paint = new SKPaint
        {
            Typeface = SKTypeface.FromFamilyName(
                "sans-serif",
                SKFontStyleWeight.Bold,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright)
        };

        var spans = _assetLoader.FindTypefaces("Bold Text 20px", paint);

        Assert.Single(spans);
        var span = spans[0];
        Assert.NotNull(span.Typeface);
        Assert.True(span.Typeface!.FontWeight >= SKFontStyleWeight.SemiBold,
            "Expected resolved typeface to be semi-bold or heavier.");
    }

    [Fact]
    public void FakeBoldMatchesDesiredWeight()
    {
        var paint = new SKPaint
        {
            Typeface = SKTypeface.FromFamilyName(
                "sans-serif",
                SKFontStyleWeight.ExtraBlack,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright)
        };

        using var skPaint = _model.ToSKPaint(paint);
        Assert.NotNull(skPaint);

        var desiredWeight = (int)SkiaSharp.SKFontStyleWeight.ExtraBlack;
        var actualWeight = skPaint!.Typeface?.FontWeight ?? 0;
        var shouldFakeBold = actualWeight < desiredWeight;

        Assert.Equal(shouldFakeBold, skPaint.FakeBoldText);
    }
}

#pragma warning restore CS0618
