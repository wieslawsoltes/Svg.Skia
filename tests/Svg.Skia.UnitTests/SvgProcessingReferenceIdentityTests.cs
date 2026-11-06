using System;
using SkiaSharp;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgProcessingReferenceIdentityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AbsoluteClipReferencesWithDistinctFragmentsAllApply(bool nestedOnShape)
    {
        var shapeClip = nestedOnShape ? "clip-path='url(https://example.invalid/scene.svg#inner)'" : string.Empty;
        var middleClip = nestedOnShape ? string.Empty : "clip-path='url(https://example.invalid/scene.svg#inner)'";
        var source = $"""
            <svg xmlns='http://www.w3.org/2000/svg' width='32' height='16'>
              <defs>
                <clipPath id='outer' clip-path='url(https://EXAMPLE.invalid:443/nested/../scene.svg#middle)'>
                  <rect width='32' height='16' {shapeClip}/>
                </clipPath>
                <clipPath id='middle' {middleClip}><rect width='24' height='16'/></clipPath>
                <clipPath id='inner'><rect width='8' height='16'/></clipPath>
              </defs>
              <rect width='32' height='16' fill='red' clip-path='url(#outer)'/>
            </svg>
            """;

        using var bitmap = Render(source);

        Assert.Equal(SKColors.Red, bitmap.GetPixel(4, 8));
        Assert.Equal((byte)0, bitmap.GetPixel(12, 8).Alpha);
        Assert.Equal((byte)0, bitmap.GetPixel(28, 8).Alpha);
    }

    [Fact]
    public void SharedAbsoluteClipInSiblingBranchesPreservesBothRegions()
    {
        const string source = """
            <svg xmlns='http://www.w3.org/2000/svg' width='32' height='16'>
              <defs>
                <clipPath id='shared'>
                  <rect width='6' height='16'/>
                  <rect x='16' width='6' height='16'/>
                </clipPath>
                <clipPath id='outer'>
                  <rect width='12' height='16' clip-path='url(https://example.invalid/scene.svg#shared)'/>
                  <rect x='16' width='12' height='16' clip-path='url(https://example.invalid/scene.svg#shared)'/>
                </clipPath>
              </defs>
              <rect width='32' height='16' fill='red' clip-path='url(#outer)'/>
            </svg>
            """;

        using var bitmap = Render(source);

        Assert.Equal(SKColors.Red, bitmap.GetPixel(3, 8));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(19, 8));
        Assert.Equal((byte)0, bitmap.GetPixel(9, 8).Alpha);
        Assert.Equal((byte)0, bitmap.GetPixel(25, 8).Alpha);
    }

    [Fact]
    public void AbsoluteAndLocalClipCycleTerminatesAndPreservesUnrelatedSibling()
    {
        const string source = """
            <svg xmlns='http://www.w3.org/2000/svg' width='32' height='16'>
              <defs>
                <clipPath id='a' clip-path='url(https://example.invalid/scene.svg#b)'>
                  <rect width='16' height='16'/>
                </clipPath>
                <clipPath id='b' clip-path='url(#a)'><rect width='16' height='16'/></clipPath>
              </defs>
              <rect width='16' height='16' fill='red' clip-path='url(#a)'/>
              <rect x='24' width='8' height='16' fill='lime'/>
            </svg>
            """;

        using var bitmap = Render(source);

        Assert.Equal(SKColors.Red, bitmap.GetPixel(4, 8));
        Assert.Equal((byte)0, bitmap.GetPixel(20, 8).Alpha);
        Assert.Equal(SKColors.Lime, bitmap.GetPixel(28, 8));
    }

    private static SKBitmap Render(string source)
    {
        var document = SvgDocument.FromSvg<SvgDocument>(source);
        document.BaseUri = new Uri("https://example.invalid/scene.svg");
        using var svg = new SKSvg();
        var picture = svg.FromSvgDocument(document);
        Assert.NotNull(picture);

        var bitmap = new SKBitmap(32, 16);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawPicture(picture!);
        canvas.Flush();
        return bitmap;
    }
}
