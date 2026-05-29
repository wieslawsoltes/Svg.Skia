using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Xunit;
using SkiaAlphaType = SkiaSharp.SKAlphaType;
using SkiaBitmap = SkiaSharp.SKBitmap;
using SkiaColor = SkiaSharp.SKColor;
using SkiaColors = SkiaSharp.SKColors;
using SkiaColorType = SkiaSharp.SKColorType;

namespace Svg.Skia.UnitTests;

public class SvgViewerResourceTests
{
    [Fact]
    public void Load_W3CGzippedSvgDataImageRendersEmbeddedStar()
    {
        using var svg = new SKSvg();
        svg.Load(GetW3CSvgPath("conform-viewers-02-f"));

        using var bitmap = RenderBitmap(svg);
        var starCenter = bitmap.GetPixel(240, 170);

        Assert.True(
            starCenter.Alpha > 0 && starCenter.Red > 100 && starCenter.Green > 100,
            $"Expected W3C gzipped data SVG image content near the center, but found {starCenter}.");
    }

    [Fact]
    public void FromSvg_GzippedSvgDataImageUsesStaticNestedDocumentWhenJavaScriptEnabled()
    {
        var nestedSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="nested-shape" width="20" height="20" fill="#00ff00">
                <set attributeName="fill" to="#ff0000" begin="0s" dur="10s" />
              </rect>
              <script>document.getElementById('nested-shape').setAttribute('fill', '#ff0000');</script>
            </svg>
            """;
        var nestedDataUri = "data:image/svg+xml;base64," + Convert.ToBase64String(CompressUtf8(nestedSvg));
        var parentSvg = $$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect width="20" height="20" fill="#ff0000" />
              <image href="{{nestedDataUri}}" width="20" height="20" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.FromSvg(parentSvg);

        using var bitmap = RenderBitmap(svg);

        AssertMostlyGreen(
            bitmap.GetPixel(10, 10),
            "Expected gzipped data SVG image to render as a static nested image document.");
    }

    [Fact]
    public void Load_CyclicNestedSvgImageUsesPlaceholderAndPreservesSiblingContent()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("SvgSkiaImageCycle");
        try
        {
            var parentPath = Path.Combine(tempDirectory.FullName, "parent.svg");
            var childPath = Path.Combine(tempDirectory.FullName, "child.svg");

            File.WriteAllText(
                parentPath,
                """
                <svg xmlns="http://www.w3.org/2000/svg" width="40" height="20">
                  <image href="child.svg" width="20" height="20" preserveAspectRatio="none"/>
                  <rect x="20" width="20" height="20" fill="#00ff00"/>
                </svg>
                """);
            File.WriteAllText(
                childPath,
                """
                <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
                  <rect width="20" height="10" fill="#ff0000"/>
                  <image y="10" width="20" height="10" href="parent.svg" preserveAspectRatio="none"/>
                </svg>
                """);

            using var svg = new SKSvg();
            using var _ = svg.Load(parentPath);
            using var bitmap = RenderBitmap(svg);

            AssertMostlyRed(
                bitmap.GetPixel(10, 5),
                "Expected non-recursive nested SVG image content to render before the cyclic edge.");
            AssertMostlyGreen(
                bitmap.GetPixel(30, 10),
                "Expected sibling content outside the cyclic image to stay rendered.");
            AssertNeutralPlaceholder(
                bitmap.GetPixel(10, 15),
                "Expected the recursive nested SVG image edge to render the deterministic broken-image placeholder.");
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Load_W3CBrokenAndCyclicImageFixtureUsesPlaceholdersAndPreservesSurroundingContent()
    {
        using var svg = new SKSvg();
        svg.Load(GetW3CSvgPath("struct-image-12-b"));

        using var bitmap = RenderBitmap(svg);

        AssertNeutralPlaceholder(
            bitmap.GetPixel(120, 120),
            "Expected W3C invalid/cyclic image references to use the deterministic placeholder policy.");
        AssertMostlyBlue(
            bitmap.GetPixel(360, 230),
            "Expected content surrounding the invalid/cyclic W3C image references to remain visible.");
    }

    private static string GetW3CSvgPath(string name)
    {
        return Path.Combine(
            "..",
            "..",
            "..",
            "..",
            "..",
            "externals",
            "W3C_SVG_11_TestSuite",
            "W3C_SVG_11_TestSuite",
            "svg",
            $"{name}.svg");
    }

    private static byte[] CompressUtf8(string text)
    {
        using var memoryStream = new MemoryStream();
        using (var gzipStream = new GZipStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true))
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            gzipStream.Write(bytes, 0, bytes.Length);
        }

        return memoryStream.ToArray();
    }

    private static SkiaBitmap RenderBitmap(SKSvg svg)
    {
        Assert.NotNull(svg.Picture);
        var bitmap = svg.Picture!.ToBitmap(
            SkiaColors.Transparent,
            1f,
            1f,
            SkiaColorType.Rgba8888,
            SkiaAlphaType.Unpremul,
            svg.Settings.Srgb);

        return Assert.IsType<SkiaBitmap>(bitmap);
    }

    private static void AssertMostlyGreen(SkiaColor pixel, string message)
    {
        Assert.True(
            pixel.Green > 200 && pixel.Red < 40 && pixel.Blue < 40 && pixel.Alpha > 200,
            $"{message} Pixel was {pixel}.");
    }

    private static void AssertMostlyRed(SkiaColor pixel, string message)
    {
        Assert.True(
            pixel.Red > 200 && pixel.Green < 40 && pixel.Blue < 40 && pixel.Alpha > 200,
            $"{message} Pixel was {pixel}.");
    }

    private static void AssertMostlyBlue(SkiaColor pixel, string message)
    {
        Assert.True(
            pixel.Blue > 180 && pixel.Red < 80 && pixel.Green < 80 && pixel.Alpha > 200,
            $"{message} Pixel was {pixel}.");
    }

    private static void AssertNeutralPlaceholder(SkiaColor pixel, string message)
    {
        Assert.True(
            pixel.Alpha > 200 &&
            pixel.Red > 80 &&
            pixel.Green > 80 &&
            pixel.Blue > 80 &&
            Math.Abs(pixel.Red - pixel.Green) <= 24 &&
            Math.Abs(pixel.Red - pixel.Blue) <= 24,
            $"{message} Pixel was {pixel}.");
    }
}
