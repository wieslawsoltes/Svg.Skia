using System.IO;
using SkiaSharp;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SKPictureExtensionsTests
{
    [Fact]
    public void ToBitmap_WithReusableBitmapAndCanvas_RendersIntoSuppliedBitmap()
    {
        using var svg = new SKSvg();
        using var picture = svg.FromSvg(SimpleSvg);

        Assert.NotNull(picture);

        using var bitmap = new SKBitmap(new SKImageInfo(20, 20, SKColorType.Rgba8888, SKAlphaType.Unpremul, svg.Settings.Srgb));
        using var canvas = new SKCanvas(bitmap);

        var rendered = picture!.ToBitmap(bitmap, canvas, SKColors.Transparent, 1f, 1f);

        Assert.True(rendered);

        var pixel = bitmap.GetPixel(10, 10);
        Assert.Equal((byte)255, pixel.Alpha);
        Assert.True(pixel.Green > 200);
    }

    [Fact]
    public void ToBitmap_WithMismatchedBitmapSize_ReturnsFalse()
    {
        using var svg = new SKSvg();
        using var picture = svg.FromSvg(SimpleSvg);

        Assert.NotNull(picture);

        using var bitmap = new SKBitmap(new SKImageInfo(10, 10, SKColorType.Rgba8888, SKAlphaType.Unpremul, svg.Settings.Srgb));
        using var canvas = new SKCanvas(bitmap);

        var rendered = picture!.ToBitmap(bitmap, canvas, SKColors.Transparent, 1f, 1f);

        Assert.False(rendered);
    }

    [Fact]
    public void ToImage_WithReusableSurface_WritesEncodedImage()
    {
        using var svg = new SKSvg();
        using var picture = svg.FromSvg(SimpleSvg);

        Assert.NotNull(picture);

        using var surface = SKSurface.Create(new SKImageInfo(20, 20, SKColorType.Rgba8888, SKAlphaType.Premul, svg.Settings.Srgb));
        using var stream = new MemoryStream();

        var saved = picture!.ToImage(stream, surface!, SKColors.Transparent, SKEncodedImageFormat.Png, 100, 1f, 1f);

        Assert.True(saved);
        Assert.True(stream.Length > 0);

        stream.Position = 0;
        using var bitmap = SKBitmap.Decode(stream);
        Assert.NotNull(bitmap);

        var pixel = bitmap!.GetPixel(10, 10);
        Assert.Equal((byte)255, pixel.Alpha);
        Assert.True(pixel.Green > 200);
    }

    [Fact]
    public void ToImage_WithAllocatedBitmapPath_WritesEncodedImage()
    {
        using var svg = new SKSvg();
        using var picture = svg.FromSvg(SimpleSvg);

        Assert.NotNull(picture);

        using var stream = new MemoryStream();

        var saved = picture!.ToImage(
            stream,
            SKColors.Transparent,
            SKEncodedImageFormat.Png,
            100,
            1f,
            1f,
            SKColorType.Rgba8888,
            SKAlphaType.Premul,
            svg.Settings.Srgb);

        Assert.True(saved);
        Assert.True(stream.Length > 0);

        stream.Position = 0;
        using var bitmap = SKBitmap.Decode(stream);
        Assert.NotNull(bitmap);

        var pixel = bitmap!.GetPixel(10, 10);
        Assert.Equal((byte)255, pixel.Alpha);
        Assert.True(pixel.Green > 200);
    }

    [Fact]
    public void ToImage_WithAllocatedBitmapPath_PreservesTransparentAlpha()
    {
        using var svg = new SKSvg();
        using var picture = svg.FromSvg(TransparentSvg);

        Assert.NotNull(picture);

        using var stream = new MemoryStream();

        var saved = picture!.ToImage(
            stream,
            SKColors.Transparent,
            SKEncodedImageFormat.Png,
            100,
            1f,
            1f,
            SKColorType.Rgba8888,
            SKAlphaType.Premul,
            svg.Settings.Srgb);

        Assert.True(saved);
        Assert.True(stream.Length > 0);

        stream.Position = 0;
        using var bitmap = SKBitmap.Decode(stream);
        Assert.NotNull(bitmap);

        var pixel = bitmap!.GetPixel(10, 10);
        Assert.InRange(pixel.Alpha, (byte)120, (byte)135);
        Assert.True(pixel.Red > 200);
        Assert.Equal((byte)0, pixel.Green);
        Assert.Equal((byte)0, pixel.Blue);
    }

    private const string SimpleSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 20 20">
          <rect x="0" y="0" width="20" height="20" fill="#00ff00" />
        </svg>
        """;

    private const string TransparentSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 20 20">
          <rect x="0" y="0" width="20" height="20" fill="#ff0000" fill-opacity="0.5" />
        </svg>
        """;
}
