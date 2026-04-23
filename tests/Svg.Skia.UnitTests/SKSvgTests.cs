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

    [Theory]
    [InlineData("", 1, 3)]
    [InlineData("vector-effect=\"non-scaling-stroke\"", 3, 6)]
    [InlineData("style=\"vector-effect: non-scaling-stroke\"", 3, 6)]
    public void Save_DownScaledVectorEffectNonScalingStroke_PreservesStrokeWidth(string vectorEffect, int minOpaquePixels, int maxOpaquePixels)
    {
        var svg = new SKSvg();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(CreateNonScalingStrokeSvgMarkup(vectorEffect)));
        using var _ = svg.Load(input);
        using var output = new MemoryStream();

        Assert.True(svg.Save(output, SkiaSharp.SKColors.Transparent, SkiaSharp.SKEncodedImageFormat.Png, 100, 0.5f, 0.5f));

        output.Position = 0;
        using var image = Image.Load<Rgba32>(output);
        Assert.Equal(50, image.Width);
        Assert.Equal(20, image.Height);

        var opaquePixels = CountOpaquePixelsInColumn(image, 25);
        Assert.InRange(opaquePixels, minOpaquePixels, maxOpaquePixels);
    }

    [Theory]
    [InlineData("", 1, 3)]
    [InlineData("vector-effect=\"non-scaling-stroke\"", 3, 6)]
    [InlineData("style=\"vector-effect: non-scaling-stroke\"", 3, 6)]
    public void Draw_DownScaledVectorEffectNonScalingStroke_PreservesStrokeWidthAndRaisesOnDraw(string vectorEffect, int minOpaquePixels, int maxOpaquePixels)
    {
        var svg = new SKSvg();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(CreateNonScalingStrokeSvgMarkup(vectorEffect)));
        using var _ = svg.Load(input);
        using var bitmap = new SkiaSharp.SKBitmap(new SkiaSharp.SKImageInfo(50, 20, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul));
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        var drawCount = 0;
        svg.OnDraw += (_, _) => drawCount++;

        canvas.Clear(SkiaSharp.SKColors.Transparent);
        canvas.Scale(0.5f, 0.5f);
        svg.Draw(canvas);

        using var skImage = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = skImage.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        Assert.NotNull(data);

        using var output = new MemoryStream(data.ToArray());
        using var image = Image.Load<Rgba32>(output);
        var opaquePixels = CountOpaquePixelsInColumn(image, 25);
        Assert.InRange(opaquePixels, minOpaquePixels, maxOpaquePixels);
        Assert.Equal(1, drawCount);
    }

    [Fact]
    public void Draw_AnimationLayerCachingWithNonScalingStroke_PreservesStrokeWidth()
    {
        var svg = new SKSvg();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(CreateAnimationLayerNonScalingStrokeSvgMarkup()));
        using var _ = svg.Load(input);
        using var bitmap = new SkiaSharp.SKBitmap(new SkiaSharp.SKImageInfo(50, 20, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul));
        using var canvas = new SkiaSharp.SKCanvas(bitmap);

        Assert.True(svg.UsesAnimationLayerCaching);

        canvas.Clear(SkiaSharp.SKColors.Transparent);
        canvas.Scale(0.5f, 0.5f);
        svg.Draw(canvas);

        using var skImage = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = skImage.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        Assert.NotNull(data);

        using var output = new MemoryStream(data.ToArray());
        using var image = Image.Load<Rgba32>(output);
        var opaquePixels = CountOpaquePixelsInColumn(image, 25);
        Assert.InRange(opaquePixels, 3, 6);
    }

    private static string CreateNonScalingStrokeSvgMarkup(string vectorEffect)
    {
        return $$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="40" viewBox="0 0 100 40">
              <line x1="10" y1="20" x2="90" y2="20" stroke="black" stroke-width="4" {{vectorEffect}} />
            </svg>
            """;
    }

    private static string CreateAnimationLayerNonScalingStrokeSvgMarkup()
    {
        return """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="40" viewBox="0 0 100 40">
              <line x1="10" y1="20" x2="90" y2="20" stroke="black" stroke-width="4" vector-effect="non-scaling-stroke" />
              <g id="animated-root">
                <rect id="moving" x="0" y="34" width="5" height="4" fill="red">
                  <animate attributeName="x" from="0" to="10" dur="1s" fill="freeze" />
                </rect>
              </g>
            </svg>
            """;
    }

    private static int CountOpaquePixelsInColumn(Image<Rgba32> image, int x)
    {
        var count = 0;
        for (var y = 0; y < image.Height; y++)
        {
            if (image[x, y].A > 200)
            {
                count++;
            }
        }

        return count;
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
}
