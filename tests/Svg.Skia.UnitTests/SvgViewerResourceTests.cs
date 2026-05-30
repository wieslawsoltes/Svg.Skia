using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
            using var secondBitmap = RenderBitmap(svg);

            AssertMostlyRed(
                bitmap.GetPixel(10, 5),
                "Expected non-recursive nested SVG image content to render before the cyclic edge.");
            AssertMostlyGreen(
                bitmap.GetPixel(30, 10),
                "Expected sibling content outside the cyclic image to stay rendered.");
            AssertNeutralPlaceholder(
                bitmap.GetPixel(10, 15),
                "Expected the recursive nested SVG image edge to render the deterministic broken-image placeholder.");
            AssertSameBitmap(bitmap, secondBitmap);
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
        SetStandaloneW3CViewport(svg);
        svg.Load(GetW3CSvgPath("struct-image-12-b"));

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        var fixtureImageNodes = scene!.Traverse()
            .Where(static node =>
                node.Kind == SvgSceneNodeKind.Image &&
                IsClose(node.GeometryBounds.Left, 60f) &&
                IsClose(node.GeometryBounds.Top, 50f) &&
                IsClose(node.GeometryBounds.Width, 240f) &&
                IsClose(node.GeometryBounds.Height, 240f))
            .ToArray();
        Assert.True(fixtureImageNodes.Length >= 3);
        Assert.All(fixtureImageNodes, static node =>
        {
            Assert.True(node.IsRenderable);
            Assert.True(node.LocalModel is not null || node.Children.Count > 0);
        });
        Assert.True(fixtureImageNodes.Count(static node => node.LocalModel is not null) >= 2);

        using var bitmap = RenderBitmap(svg);
        using var secondBitmap = RenderBitmap(svg);

        AssertNeutralPlaceholder(
            bitmap.GetPixel(120, 120),
            "Expected W3C invalid/cyclic image references to use the deterministic placeholder policy.");
        AssertMostlyBlue(
            bitmap.GetPixel(360, 230),
            "Expected content surrounding the invalid/cyclic W3C image references to remain visible.");
        AssertSameBitmap(bitmap, secondBitmap);
    }

    [Fact]
    public void Load_W3CRecursiveUseFixtureSuppressesRecursiveReferencesAndKeepsOutputStable()
    {
        using var svg = new SKSvg();
        SetStandaloneW3CViewport(svg);
        svg.Load(GetW3CSvgPath("struct-use-08-b"));

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        var recursiveUseNodes = scene!.Traverse()
            .Where(static node => node.ElementId is "use-elm-1" or "use-elm-2")
            .OrderBy(static node => node.ElementId, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(2, recursiveUseNodes.Length);
        var rootReferenceUse = recursiveUseNodes[0];
        Assert.False(rootReferenceUse.IsRenderable);
        Assert.Empty(rootReferenceUse.Children);

        var imageReferenceUse = recursiveUseNodes[1];
        Assert.True(imageReferenceUse.IsRenderable);
        Assert.NotEmpty(imageReferenceUse.Children);
        Assert.True(imageReferenceUse.Children.Count <= 1);
        Assert.True(scene.Traverse().Count() < 80);

        using var bitmap = RenderBitmap(svg);
        using var secondBitmap = RenderBitmap(svg);

        AssertTransparent(
            bitmap.GetPixel(120, 110),
            "Expected recursive <use> content referencing the external root to be suppressed.");
        AssertNotMostlyRed(
            bitmap.GetPixel(360, 110),
            "Expected bounded recursive image expansion not to paint recursive red content.");
        AssertContainsMostlyGreenPixel(
            bitmap,
            170,
            245,
            140,
            35,
            "Expected non-recursive pass text in struct-use-08-b to remain visible.");
        AssertSameBitmap(bitmap, secondBitmap);
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

    private static void SetStandaloneW3CViewport(SKSvg svg)
    {
        svg.Settings.StandaloneViewport = SkiaSharp.SKRect.Create(0f, 0f, 480f, 360f);
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

    private static void AssertTransparent(SkiaColor pixel, string message)
    {
        Assert.True(pixel.Alpha <= 8, $"{message} Pixel was {pixel}.");
    }

    private static void AssertNotMostlyRed(SkiaColor pixel, string message)
    {
        Assert.False(
            pixel.Red > 160 && pixel.Green < 80 && pixel.Blue < 80 && pixel.Alpha > 120,
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

    private static void AssertContainsMostlyGreenPixel(
        SkiaBitmap bitmap,
        int startX,
        int startY,
        int width,
        int height,
        string message)
    {
        for (var y = startY; y < startY + height; y++)
        {
            for (var x = startX; x < startX + width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.Green > 100 && pixel.Red < 80 && pixel.Blue < 80 && pixel.Alpha > 120)
                {
                    return;
                }
            }
        }

        Assert.Fail(message);
    }

    private static void AssertSameBitmap(SkiaBitmap expected, SkiaBitmap actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);

        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                Assert.Equal(expected.GetPixel(x, y), actual.GetPixel(x, y));
            }
        }
    }

    private static bool IsClose(float actual, float expected)
    {
        return Math.Abs(actual - expected) <= 0.01f;
    }
}
