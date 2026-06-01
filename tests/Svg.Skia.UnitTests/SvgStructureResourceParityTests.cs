using SkiaSharp;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgStructureResourceParityTests
{
    [Fact]
    public void Switch_SkipsUnsupportedRequiredFeaturesChild()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="40">
              <switch>
                <rect width="40" height="40" fill="#ff0000" requiredFeatures="http://example.invalid/unsupported" />
                <rect width="40" height="40" fill="#00ff00" />
              </switch>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(svgMarkup);

        using var bitmap = RenderBitmap(svg, 40, 40);
        AssertMostlyGreen(bitmap.GetPixel(20, 20));
    }

    [Fact]
    public void ConditionalProcessing_UsesDeterministicDefaultSystemLanguage()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="40">
              <switch>
                <rect width="40" height="40" fill="#00ff00" systemLanguage="en-US" />
                <rect width="40" height="40" fill="#ff0000" />
              </switch>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(svgMarkup);

        using var bitmap = RenderBitmap(svg, 40, 40);
        AssertMostlyGreen(bitmap.GetPixel(20, 20));
    }

    [Fact]
    public void ClipPath_WithInactiveSystemLanguage_IsIgnoredAsUnavailableResource()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="40">
              <defs>
                <clipPath id="clip" systemLanguage="ru-RU">
                  <circle cx="20" cy="20" r="8" />
                </clipPath>
              </defs>
              <rect width="40" height="40" fill="#00ff00" clip-path="url(#clip)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(svgMarkup);

        using var bitmap = RenderBitmap(svg, 40, 40);
        AssertMostlyGreen(bitmap.GetPixel(4, 4));
    }

    [Fact]
    public void PaintServer_WithInactiveSystemLanguage_UsesFallbackPaint()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="40">
              <defs>
                <linearGradient id="inactive" systemLanguage="ru-RU">
                  <stop offset="0" stop-color="#00ff00" />
                  <stop offset="1" stop-color="#00ff00" />
                </linearGradient>
              </defs>
              <rect width="40" height="40" fill="url(#inactive) #ff0000" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(svgMarkup);

        using var bitmap = RenderBitmap(svg, 40, 40);
        AssertMostlyRed(bitmap.GetPixel(20, 20));
    }

    [Fact]
    public void TransformOrigin_ContentBox_UsesElementGeometryReferenceBox()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <rect x="10" y="10" width="20" height="10" fill="#00ff00"
                    transform-box="content-box"
                    transform-origin="center"
                    transform="rotate(180)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(svgMarkup);

        using var bitmap = RenderBitmap(svg, 100, 100);
        AssertMostlyGreen(bitmap.GetPixel(15, 15));
        AssertTransparent(bitmap.GetPixel(75, 85));
    }

    private static SKBitmap RenderBitmap(SKSvg svg, int width, int height)
    {
        var picture = svg.Picture;
        Assert.NotNull(picture);

        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawPicture(picture);
        return bitmap;
    }

    private static void AssertMostlyGreen(SKColor color)
    {
        Assert.True(
            color.Green > 180 && color.Red < 80 && color.Blue < 80 && color.Alpha > 220,
            $"Expected mostly green, got {color}.");
    }

    private static void AssertMostlyRed(SKColor color)
    {
        Assert.True(
            color.Red > 180 && color.Green < 80 && color.Blue < 80 && color.Alpha > 220,
            $"Expected mostly red, got {color}.");
    }

    private static void AssertTransparent(SKColor color)
    {
        Assert.True(color.Alpha < 32, $"Expected transparent, got {color}.");
    }
}
