using System.IO;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Svg.Model;
using Svg.Model.Services;
using Svg.Skia.UnitTests.Common;
using Xunit;
using DrawingColor = System.Drawing.Color;

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
    public void Save_CssHexAlphaFill_RendersAlpha()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10">
              <style>#target { fill: #11223380; }</style>
              <rect id="target" width="10" height="10" />
            </svg>
            """;

        var svg = new SKSvg();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(svgMarkup));
        using var _ = svg.Load(input);
        using var output = new MemoryStream();

        Assert.True(svg.Save(output, SkiaSharp.SKColors.Transparent));

        output.Position = 0;
        using var image = Image.Load<Rgba32>(output);
        var pixel = image[5, 5];
        Assert.InRange(pixel.R, (byte)0x10, (byte)0x12);
        Assert.InRange(pixel.G, (byte)0x21, (byte)0x23);
        Assert.InRange(pixel.B, (byte)0x32, (byte)0x34);
        Assert.Equal((byte)0x80, pixel.A);
    }

    [Fact]
    public void Save_DownscaledMorphologyFilter_RetainsSubpixelInsideStroke()
    {
        const string pathData = "m228 91.1c-2.5 0.5-8.3 1.6-13 2.5-4.7 0.9-13.8 3.3-20.3 5.5-6.4 2.1-16.5 6.3-22.5 9.3-5.9 3-14.6 8-19.4 11.3-4.8 3.2-12.6 9.2-17.4 13.3-4.9 4.1-12.1 11.3-16.2 16-4.1 4.7-10.7 13.4-14.8 19.5-4 6-9.3 15.3-11.8 20.5-2.5 5.2-6.1 14.2-8 20-2 5.8-4.4 14.8-5.5 20-1.1 5.2-2.5 13.9-3 19.2-0.6 5.4-1.1 12.9-1.1 16.8 0 3.8 0.7 11.6 1.5 17.2 0.9 5.7 2.7 14.8 4.2 20.3 1.4 5.5 4.4 14.7 6.6 20.5 2.2 5.8 7 16.3 10.7 23.5 3.6 7.1 10.2 18.6 14.5 25.5 4.4 6.9 12.7 18.8 18.5 26.5 5.7 7.7 14.9 19.2 20.3 25.5 5.5 6.3 15.1 17.1 21.6 23.9 6.4 6.9 19 19.4 28.1 27.9 9.1 8.5 23.1 21 31.2 27.8 10.3 8.6 15.6 12.4 17.3 12.4 1.4 0 4-1 5.7-2.3 1.8-1.2 8-6.2 13.8-11 5.8-4.9 18.1-15.9 27.4-24.5 9.3-8.7 22.8-22 30-29.7 7.2-7.7 16.9-18.5 21.7-24 4.7-5.5 11.8-14.3 15.9-19.5 4.1-5.2 11.6-15.6 16.6-23 5-7.4 12.2-18.9 15.9-25.5 3.7-6.6 8.6-16.3 11-21.5 2.3-5.2 5.7-13.6 7.4-18.5 1.8-5 4.1-12.6 5.2-17 1.1-4.4 2.7-12.5 3.5-18 0.7-5.5 1.4-13.5 1.4-17.8 0-4.2-0.7-13-1.5-19.5-0.9-6.4-2.7-16.2-4.1-21.7-1.4-5.5-4.1-14.3-6.1-19.5-1.9-5.2-5.7-13.8-8.5-19-2.8-5.2-7.8-13.5-11.2-18.4-3.3-4.9-9.5-12.8-13.6-17.5-4.1-4.7-11.1-11.6-15.5-15.4-4.4-3.7-12.1-9.5-17-12.9-5-3.3-13.7-8.4-19.5-11.3-5.8-2.9-14.6-6.7-19.5-8.4-5-1.8-11.5-3.8-14.5-4.6-3-0.8-10.8-2.3-17.3-3.5-8.8-1.5-15.7-2-28-1.9-8.9 0.1-18.2 0.5-20.7 1z";
        var svgMarkup = $$"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 500 609" width="500" height="609">
              <defs>
                <filter id="shadow" style="color-interpolation-filters:sRGB;" x="-100%" y="-100%" width="300%" height="400%">
                  <feFlood flood-opacity="1" flood-color="rgb(31 40 50 / 1)" result="flood" />
                  <feComposite in="flood" in2="SourceGraphic" operator="in" result="comp" />
                  <feOffset dx="3" dy="15" result="offset" />
                  <feGaussianBlur in="offset" stdDeviation="20" result="blur" />
                  <feBlend in="SourceGraphic" in2="blur" mode="normal" />
                </filter>
                <filter id="stroke-inside" x="-50%" y="-50%" width="200%" height="200%">
                  <feFlood flood-color="#fff" result="inside-color"/>
                  <feComposite in="inside-color" in2="SourceAlpha" operator="in" result="inside-stroke"/>
                  <feMorphology in="SourceAlpha" radius="8"/>
                  <feComposite in="SourceGraphic" operator="in" result="fill-area"/>
                  <feMerge>
                    <feMergeNode in="inside-stroke"/>
                    <feMergeNode in="fill-area"/>
                  </feMerge>
                </filter>
              </defs>
              <style>
                .marker { fill:#000000; fill-opacity:1; filter:url(#stroke-inside); }
                .marker-shadow { filter:url(#shadow); stroke:#444; stroke-width:12px; }
              </style>
              <path class="marker-shadow" d="{{pathData}}"/>
              <path class="marker" d="{{pathData}}"/>
            </svg>
            """;

        var svg = new SKSvg();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(svgMarkup));
        using var _ = svg.Load(input);
        using var output = new MemoryStream();

        Assert.True(svg.Save(
            output,
            SkiaSharp.SKColors.Transparent,
            scaleX: 38f / 609f,
            scaleY: 38f / 609f));

        output.Position = 0;
        using var image = Image.Load<Rgba32>(output);
        var whitePixels = 0;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];
                if (pixel.A > 64 && pixel.R > 160 && pixel.G > 160 && pixel.B > 160)
                {
                    whitePixels++;
                }
            }
        }

        Assert.Equal(31, image.Width);
        Assert.Equal(38, image.Height);
        Assert.True(whitePixels > 0, "Expected visible white inside stroke pixels after downscaling.");
    }

    [Fact]
    public void Save_DownscaledTranslucentBackground_DoesNotCompositeBackgroundTwice()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
            </svg>
            """;

        var svg = new SKSvg();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(svgMarkup));
        using var _ = svg.Load(input);
        using var output = new MemoryStream();

        Assert.True(svg.Save(
            output,
            new SkiaSharp.SKColor(10, 20, 30, 128),
            scaleX: 0.5f,
            scaleY: 0.5f));

        output.Position = 0;
        using var image = Image.Load<Rgba32>(output);
        var pixel = image[25, 25];

        Assert.Equal(50, image.Width);
        Assert.Equal(50, image.Height);
        Assert.Equal((byte)10, pixel.R);
        Assert.Equal((byte)20, pixel.G);
        Assert.Equal((byte)30, pixel.B);
        Assert.Equal((byte)128, pixel.A);
    }

    [Fact]
    public void Save_LargeDownscaledDocument_UsesBoundedIntermediate()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100000" height="100000" viewBox="0 0 100000 100000">
              <rect width="100000" height="100000" fill="#ff0000" />
            </svg>
            """;

        var svg = new SKSvg();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(svgMarkup));
        using var _ = svg.Load(input);
        using var output = new MemoryStream();

        Assert.True(svg.Save(
            output,
            SkiaSharp.SKColors.Transparent,
            scaleX: 0.001f,
            scaleY: 0.001f));

        output.Position = 0;
        using var image = Image.Load<Rgba32>(output);
        Assert.Equal(100, image.Width);
        Assert.Equal(100, image.Height);
        Assert.Equal((byte)255, image[50, 50].R);
    }

    [Fact]
    public void Load_CurrentColorParameter_ProvidesRootCurrentColor()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <rect x="10" y="10" width="80" height="80" fill="currentColor" />
            </svg>
            """;

        var svg = new SKSvg();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(svgMarkup));
        using var _ = svg.Load(input, new SvgParameters(null, null, DrawingColor.FromArgb(255, 0, 128, 255)));
        using var output = new MemoryStream();

        Assert.True(svg.Save(output, SkiaSharp.SKColors.Transparent));

        output.Position = 0;
        using var image = Image.Load<Rgba32>(output);
        var pixel = image[50, 50];
        Assert.Equal((byte)0, pixel.R);
        Assert.Equal((byte)128, pixel.G);
        Assert.Equal((byte)255, pixel.B);
        Assert.Equal((byte)255, pixel.A);
    }

    [Fact]
    public void Load_CurrentColorParameter_DoesNotOverrideRootColor()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" color="red">
              <rect x="10" y="10" width="80" height="80" fill="currentColor" />
            </svg>
            """;

        var svg = new SKSvg();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(svgMarkup));
        using var _ = svg.Load(input, new SvgParameters(null, null, DrawingColor.FromArgb(255, 0, 128, 255)));
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
    public void Load_CurrentColorParameter_AppliesWhenRootColorIsCurrentColor()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" color="currentColor">
              <rect x="10" y="10" width="80" height="80" fill="currentColor" />
            </svg>
            """;

        var svg = new SKSvg();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(svgMarkup));
        using var _ = svg.Load(input, new SvgParameters(null, null, DrawingColor.FromArgb(255, 0, 128, 255)));
        using var output = new MemoryStream();

        Assert.True(svg.Save(output, SkiaSharp.SKColors.Transparent));

        output.Position = 0;
        using var image = Image.Load<Rgba32>(output);
        var pixel = image[50, 50];
        Assert.Equal((byte)0, pixel.R);
        Assert.Equal((byte)128, pixel.G);
        Assert.Equal((byte)255, pixel.B);
        Assert.Equal((byte)255, pixel.A);
    }

    [Fact]
    public void Load_CurrentColorParameter_DoesNotOverrideRootStyleColor()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" style="color:red">
              <rect x="10" y="10" width="80" height="80" fill="currentColor" />
            </svg>
            """;

        var svg = new SKSvg();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(svgMarkup));
        using var _ = svg.Load(input, new SvgParameters(null, null, DrawingColor.FromArgb(255, 0, 128, 255)));
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
    public void Save_CssVarFallbackPaint_UsesFallbackColor()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" viewBox="0 0 100 100">
              <path d="M10 50h80" style="fill:none;stroke:var(--color-text, #000);stroke-width:10;stroke-linecap:square" />
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
        Assert.Equal((byte)0, pixel.R);
        Assert.Equal((byte)0, pixel.G);
        Assert.Equal((byte)0, pixel.B);
        Assert.Equal((byte)255, pixel.A);
    }

    [Fact]
    public void Save_CssVarPaint_UsesInheritedCustomProperty()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" viewBox="0 0 100 100">
              <style>
                svg { --color-text: #00ff00; }
                path { stroke: var(--color-text, #000); }
              </style>
              <path d="M10 50h80" style="fill:none;stroke-width:10;stroke-linecap:square" />
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
        Assert.Equal((byte)0, pixel.R);
        Assert.Equal((byte)255, pixel.G);
        Assert.Equal((byte)0, pixel.B);
        Assert.Equal((byte)255, pixel.A);
    }

    [Fact]
    public void Save_CssVarPaint_CssWideInheritAndUnsetUseInheritedCustomProperty()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" viewBox="0 0 100 100">
              <style>
                svg { --color-text: #00ff00; }
                .inherit-target { --color-text: inherit; stroke: var(--color-text, #ff0000); }
                .unset-target { --color-text: unset; stroke: var(--color-text, #ff0000); }
              </style>
              <path class="inherit-target" d="M10 30h80" style="fill:none;stroke-width:10;stroke-linecap:square" />
              <path class="unset-target" d="M10 70h80" style="fill:none;stroke-width:10;stroke-linecap:square" />
            </svg>
            """;

        var svg = new SKSvg();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(svgMarkup));
        using var _ = svg.Load(input);
        using var output = new MemoryStream();

        Assert.True(svg.Save(output, SkiaSharp.SKColors.Transparent));

        output.Position = 0;
        using var image = Image.Load<Rgba32>(output);
        var inheritPixel = image[50, 30];
        Assert.Equal((byte)0, inheritPixel.R);
        Assert.Equal((byte)255, inheritPixel.G);
        Assert.Equal((byte)0, inheritPixel.B);
        Assert.Equal((byte)255, inheritPixel.A);

        var unsetPixel = image[50, 70];
        Assert.Equal((byte)0, unsetPixel.R);
        Assert.Equal((byte)255, unsetPixel.G);
        Assert.Equal((byte)0, unsetPixel.B);
        Assert.Equal((byte)255, unsetPixel.A);
    }

    [Fact]
    public void Save_CssVarPaint_CssWideInitialUsesFallback()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" viewBox="0 0 100 100">
              <style>
                svg { --color-text: #00ff00; }
                path { --color-text: initial; stroke: var(--color-text, #0000ff); }
              </style>
              <path d="M10 50h80" style="fill:none;stroke-width:10;stroke-linecap:square" />
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
        Assert.Equal((byte)0, pixel.R);
        Assert.Equal((byte)0, pixel.G);
        Assert.Equal((byte)255, pixel.B);
        Assert.Equal((byte)255, pixel.A);
    }

    [Fact]
    public void Save_CssVarPaint_AppliesCustomPropertyInsideActiveMediaRule()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" viewBox="0 0 100 100">
              <style>
                @media screen {
                  svg { --color-text: #00ff00; }
                }
                path { stroke: var(--color-text, #000); }
              </style>
              <path d="M10 50h80" style="fill:none;stroke-width:10;stroke-linecap:square" />
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
        Assert.Equal((byte)0, pixel.R);
        Assert.Equal((byte)255, pixel.G);
        Assert.Equal((byte)0, pixel.B);
        Assert.Equal((byte)255, pixel.A);
    }

    [Fact]
    public void Save_CssVarPaint_IgnoresCustomPropertyInsideInactiveMediaRule()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" viewBox="0 0 100 100">
              <style>
                @media print {
                  svg { --color-text: #00ff00; }
                }
                path { stroke: var(--color-text, #0000ff); }
              </style>
              <path d="M10 50h80" style="fill:none;stroke-width:10;stroke-linecap:square" />
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
        Assert.Equal((byte)0, pixel.R);
        Assert.Equal((byte)0, pixel.G);
        Assert.Equal((byte)255, pixel.B);
        Assert.Equal((byte)255, pixel.A);
    }

    [Fact]
    public void Save_CssVarPaint_UsesQualifiedRootCustomProperty()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" class="dark" width="100" height="100" viewBox="0 0 100 100">
              <style>
                :root.dark { --color-text: #00ff00; }
                path { stroke: var(--color-text, #000); }
              </style>
              <path d="M10 50h80" style="fill:none;stroke-width:10;stroke-linecap:square" />
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
        Assert.Equal((byte)0, pixel.R);
        Assert.Equal((byte)255, pixel.G);
        Assert.Equal((byte)0, pixel.B);
        Assert.Equal((byte)255, pixel.A);
    }

    [Fact]
    public void Save_CssVarPaint_ContinuesAfterMalformedCustomPropertyDeclaration()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" viewBox="0 0 100 100">
              <style>
                path { bad; --color-text: #00ff00; }
                path { stroke: var(--color-text, #000); }
              </style>
              <path d="M10 50h80" style="fill:none;stroke-width:10;stroke-linecap:square" />
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
        Assert.Equal((byte)0, pixel.R);
        Assert.Equal((byte)255, pixel.G);
        Assert.Equal((byte)0, pixel.B);
        Assert.Equal((byte)255, pixel.A);
    }

    [Fact]
    public void Save_CssVarPaint_UsesLastEqualSpecificityCustomProperty()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" viewBox="0 0 100 100">
              <style>
                path { --color-text: #ff0000; }
                path { --color-text: #00ff00; }
                path { stroke: var(--color-text, #000); }
              </style>
              <path d="M10 50h80" style="fill:none;stroke-width:10;stroke-linecap:square" />
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
        Assert.Equal((byte)0, pixel.R);
        Assert.Equal((byte)255, pixel.G);
        Assert.Equal((byte)0, pixel.B);
        Assert.Equal((byte)255, pixel.A);
    }

    [Fact]
    public void Save_CssVarPaint_DoesNotPromoteLowSpecificitySourceOrderTie()
    {
        var lowSpecificityRules = new StringBuilder();
        for (var i = 0; i < 40; i++)
        {
            lowSpecificityRules.AppendLine("path { --color-text: #ff0000; }");
        }

        var svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" viewBox="0 0 100 100">
              <style>
            """ + lowSpecificityRules + """
                g path { --color-text: #00ff00; }
                path { --color-text: #0000ff; }
                path { stroke: var(--color-text, #000); }
              </style>
              <g>
                <path d="M10 50h80" style="fill:none;stroke-width:10;stroke-linecap:square" />
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
        Assert.Equal((byte)0, pixel.R);
        Assert.Equal((byte)255, pixel.G);
        Assert.Equal((byte)0, pixel.B);
        Assert.Equal((byte)255, pixel.A);
    }

    [Fact]
    public void Save_CssVarPaint_StripsImportantAndHonorsPriority()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" viewBox="0 0 100 100">
              <style>
                path { --color-text: #ff0000 !important; }
                svg path { --color-text: #00ff00; }
                path { stroke: var(--color-text, #000); }
              </style>
              <path d="M10 50h80" style="fill:none;stroke-width:10;stroke-linecap:square" />
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
    public void Save_CssVarPaint_ResolvesInheritedCustomPropertyAtDeclaringElement()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" viewBox="0 0 100 100">
              <style>
                svg {
                  --theme-color: #00ff00;
                  --stroke-color: var(--theme-color);
                }
                path {
                  --theme-color: #ff0000;
                  stroke: var(--stroke-color, #000);
                }
              </style>
              <path d="M10 50h80" style="fill:none;stroke-width:10;stroke-linecap:square" />
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
        Assert.Equal((byte)0, pixel.R);
        Assert.Equal((byte)255, pixel.G);
        Assert.Equal((byte)0, pixel.B);
        Assert.Equal((byte)255, pixel.A);
    }

    [Fact]
    public void Write_CssVarPaint_DoesNotSerializeCustomPropertyAttributes()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" viewBox="0 0 100 100">
              <style>
                svg { --color-text: #00ff00; }
                path { stroke: var(--color-text, #000); }
              </style>
              <path d="M10 50h80" style="fill:none;stroke-width:10;stroke-linecap:square" />
            </svg>
            """;

        var document = SvgService.FromSvg(svgMarkup);
        Assert.NotNull(document);
        using var output = new MemoryStream();

        document!.Write(output, useBom: false);

        var xml = Encoding.UTF8.GetString(output.ToArray());
        Assert.DoesNotContain("--color-text=\"", xml);
        Assert.Contains("--color-text: #00ff00", xml);
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

    [Fact]
    public void Load_RelativeImageHrefWithoutBaseUri_SkipsMissingImage()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink"
                 width="32px" height="32px" viewBox="0 0 32 32">
              <rect width="32" height="32" fill="#232B34" />
              <g opacity="0.1">
                <image width="99" height="108" xlink:href="6F03BD87.png"
                       transform="matrix(1 0 0 1 -33.5 -44.6934)" />
              </g>
              <circle cx="16" cy="16" r="8" fill="#586871" />
            </svg>
            """;

        var svg = new SKSvg();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(svgMarkup));
        using var _ = svg.Load(input);
        using var output = new MemoryStream();

        Assert.True(svg.Save(output, SkiaSharp.SKColors.Transparent));

        output.Position = 0;
        using var image = Image.Load<Rgba32>(output);
        Assert.Equal(32, image.Width);
        Assert.Equal(32, image.Height);
        Assert.Equal(new Rgba32(0x23, 0x2B, 0x34), image[0, 0]);
        Assert.Equal(new Rgba32(0x58, 0x68, 0x71), image[16, 16]);
    }
}
