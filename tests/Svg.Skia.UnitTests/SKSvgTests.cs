using System.IO;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
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

    [WindowsTheory]
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
}
