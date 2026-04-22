using System.IO;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Svg;
using Svg.Skia.UnitTests.Common;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SKSvgTests : SvgUnitTest
{
    private static string GetSvgPath(string name)
        => Path.Combine("..", "..", "..", "..", "Tests", name);

    private static string GetExpectedPngPath(string name)
        => Path.Combine("..", "..", "..", "..", "Tests", name);

    private static string GetActualPngPath(string name)
        => Path.Combine("..", "..", "..", "..", "Tests", name);

    [OSXTheory]
    [InlineData("Sign in", 0.04)]
    public void Test(string name, double errorThreshold)
    {
        var svgPath = GetSvgPath($"{name}.svg");
        var expectedPng = GetExpectedPngPath($"{name}.png");
        var actualPng = GetActualPngPath($"{name} (Actual).png");

        var svg = new SKSvg();
        svg.Settings.EnableSvgFonts = true;
        using var _ = svg.Load(svgPath);
        svg.Save(actualPng, SkiaSharp.SKColors.Transparent);

        ImageHelper.CompareImages(name, actualPng, expectedPng, errorThreshold);

        File.Delete(actualPng);
    }

    [Fact]
    public void Save_EmptyRootDocument_WritesBlankViewportPng()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100%" height="100%" viewBox="0 0 480 360">
            </svg>
            """;

        var svg = new SKSvg();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(svgMarkup));
        using var _ = svg.Load(input);
        using var output = new MemoryStream();

        Assert.True(svg.Save(output, SkiaSharp.SKColors.Transparent));

        output.Position = 0;
        using var image = Image.Load<Rgba32>(output);
        Assert.Equal(480, image.Width);
        Assert.Equal(360, image.Height);
        Assert.Equal(0, image[0, 0].A);
        Assert.Equal(0, image[479, 359].A);
    }

    [Fact]
    public void Save_StandaloneDocumentWithoutExplicitSize_UsesViewBoxDimensions()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 200">
              <rect width="200" height="200" fill="red" />
            </svg>
            """;

        var svg = new SKSvg();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(svgMarkup));
        using var _ = svg.Load(input);
        using var output = new MemoryStream();

        Assert.True(svg.Save(output, SkiaSharp.SKColors.Transparent));

        output.Position = 0;
        using var image = Image.Load<Rgba32>(output);
        Assert.Equal(200, image.Width);
        Assert.Equal(200, image.Height);
    }

    [Fact]
    public void Save_StandaloneDocumentWithExplicitPercentSize_UsesViewBoxWithoutConfiguredViewport()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100%" height="100%" viewBox="0 0 100 100">
              <rect width="100" height="100" fill="red" />
            </svg>
            """;

        var svg = new SKSvg();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(svgMarkup));
        using var _ = svg.Load(input);
        using var output = new MemoryStream();

        Assert.True(svg.Save(output, SkiaSharp.SKColors.Transparent));

        output.Position = 0;
        using var image = Image.Load<Rgba32>(output);
        Assert.Equal(100, image.Width);
        Assert.Equal(100, image.Height);
    }

    [Fact]
    public void Save_StandaloneDocumentWithExplicitPercentSize_UsesConfiguredStandaloneViewport()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100%" height="100%" viewBox="0 0 100 100">
              <rect width="100" height="100" fill="red" />
            </svg>
            """;

        var svg = new SKSvg();
        svg.Settings.StandaloneViewport = SkiaSharp.SKRect.Create(0f, 0f, 480f, 360f);
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(svgMarkup));
        using var _ = svg.Load(input);
        using var output = new MemoryStream();

        Assert.True(svg.Save(output, SkiaSharp.SKColors.Transparent));

        output.Position = 0;
        using var image = Image.Load<Rgba32>(output);
        Assert.Equal(480, image.Width);
        Assert.Equal(360, image.Height);
    }

    [Fact]
    public void Save_ProgrammaticTextDecorationLineThrough_RendersDecoration()
    {
        using var plain = RenderProgrammaticTextDecoration(SvgTextDecoration.None);
        using var decorated = RenderProgrammaticTextDecoration(SvgTextDecoration.LineThrough);

        var changedPixels = CountDifferingPixels(plain, decorated);

        Assert.True(
            changedPixels > 100,
            $"Expected line-through decoration to alter the rendered text, but only {changedPixels} pixels changed.");
    }

    [Fact]
    public void Save_InheritedCurrentColor_UsesConsumingElementsColor()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <g fill="currentColor" color="lime">
                <rect x="10" y="10" width="80" height="80" color="red" />
              </g>
            </svg>
            """;

        var svg = new SKSvg();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(svgMarkup));
        using var _ = svg.Load(input);
        using var output = new MemoryStream();

        Assert.True(svg.Save(output, SkiaSharp.SKColors.Transparent));

        output.Position = 0;
        using var image = Image.Load<Rgba32>(output);
        var pixel = image[50, 50];
        Assert.Equal((byte)255, pixel.R);
        Assert.Equal((byte)0, pixel.G);
        Assert.Equal((byte)0, pixel.B);
        Assert.Equal((byte)255, pixel.A);
    }

    [Fact]
    public void Load_CssFontFaceWithExternalFontUrl_DoesNotCrash()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="40" viewBox="0 0 120 40">
              <defs>
                <style type="text/css"><![CDATA[
                  @font-face {
                    font-family: 'OpenGOST Type A';
                    src: url('../fonts/OpenGostTypeA-Regular.ttf') format('truetype');
                  }

                  text {
                    fill: #000;
                    font-family: 'OpenGOST Type A', sans-serif;
                    font-size: 24px;
                  }
                ]]></style>
              </defs>
              <text x="4" y="28">A</text>
            </svg>
            """;

        var svg = new SKSvg();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(svgMarkup));
        using var _ = svg.Load(input);
        using var output = new MemoryStream();

        Assert.True(svg.Save(output, SkiaSharp.SKColors.Transparent));

        output.Position = 0;
        using var image = Image.Load<Rgba32>(output);
        Assert.Equal(120, image.Width);
        Assert.Equal(40, image.Height);
    }

    private static Image<Rgba32> RenderProgrammaticTextDecoration(SvgTextDecoration textDecoration)
    {
        var document = new SvgDocument
        {
            Width = new SvgUnit(SvgUnitType.User, 260f),
            Height = new SvgUnit(SvgUnitType.User, 90f),
            ViewBox = new SvgViewBox(0f, 0f, 260f, 90f)
        };
        var text = new SvgText("Text decoration")
        {
            X = new SvgUnitCollection { new SvgUnit(SvgUnitType.User, 12f) },
            Y = new SvgUnitCollection { new SvgUnit(SvgUnitType.User, 58f) },
            FontFamily = "sans-serif",
            FontSize = new SvgUnit(SvgUnitType.User, 36f),
            Fill = new SvgColourServer(System.Drawing.Color.Black),
            TextDecoration = textDecoration
        };
        document.Children.Add(text);

        using var svg = new SKSvg();
        using var _ = svg.FromSvgDocument(document);
        using var output = new MemoryStream();

        Assert.True(svg.Save(output, SkiaSharp.SKColors.Transparent));

        output.Position = 0;
        return Image.Load<Rgba32>(output);
    }

    private static int CountDifferingPixels(Image<Rgba32> first, Image<Rgba32> second)
    {
        Assert.Equal(first.Width, second.Width);
        Assert.Equal(first.Height, second.Height);

        var count = 0;
        for (var y = 0; y < first.Height; y++)
        {
            for (var x = 0; x < first.Width; x++)
            {
                if (!first[x, y].Equals(second[x, y]))
                {
                    count++;
                }
            }
        }

        return count;
    }
}
