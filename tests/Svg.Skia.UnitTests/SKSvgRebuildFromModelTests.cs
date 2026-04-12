using System.Linq;
using ShimSkiaSharp;
using ShimSkiaSharp.Editing;
using SkiaSharp;
using Xunit;
using SkiaBitmap = SkiaSharp.SKBitmap;
using SkiaColor = SkiaSharp.SKColor;
using SkiaColorSpace = SkiaSharp.SKColorSpace;
using SkiaPicture = SkiaSharp.SKPicture;

namespace Svg.Skia.UnitTests;

public class SKSvgRebuildFromModelTests
{
    private const string SampleSvg = "<svg width=\"10\" height=\"10\"><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" fill=\"red\" /></svg>";
    private const string GradientSvg = """
        <svg width="10" height="10" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="g" x1="0" y1="0" x2="10" y2="0" gradientUnits="userSpaceOnUse">
              <stop offset="0" stop-color="red" />
              <stop offset="1" stop-color="blue" />
            </linearGradient>
          </defs>
          <rect x="0" y="0" width="10" height="10" fill="url(#g)" />
        </svg>
        """;

    [Fact]
    public void RebuildFromModel_RecreatesPicture()
    {
        var svg = new SKSvg();
        svg.FromSvg(SampleSvg);

        var original = svg.Picture;
        Assert.NotNull(original);

        var command = svg.Model?.FindCommands<DrawPathCanvasCommand>().FirstOrDefault();
        Assert.NotNull(command);

        if (command?.Paint is { } paint)
        {
            paint.Color = new ShimSkiaSharp.SKColor(0, 0, 0, 255);
        }

        var rebuilt = svg.RebuildFromModel();

        Assert.NotNull(rebuilt);
        Assert.NotSame(original, rebuilt);
        Assert.Same(rebuilt, svg.Picture);
    }

    [Fact]
    public void RebuildFromModel_ReturnsNull_WhenModelMissing()
    {
        var svg = new SKSvg();

        Assert.Null(svg.RebuildFromModel());
    }

    [Fact]
    public void RebuildFromModel_ReflectsMutatedPaintAfterInitialNativeBuild()
    {
        var svg = new SKSvg();
        svg.FromSvg(SampleSvg);

        Assert.NotNull(svg.Picture);
        using var originalBitmap = RenderBitmap(svg.Picture!);
        Assert.Equal(new SkiaColor(255, 0, 0, 255), originalBitmap.GetPixel(5, 5));

        var command = Assert.IsType<DrawPathCanvasCommand>(svg.Model?.FindCommands<DrawPathCanvasCommand>().First());
        Assert.NotNull(command.Paint);
        command.Paint!.Color = new ShimSkiaSharp.SKColor(0, 0, 0, 255);

        var rebuilt = svg.RebuildFromModel();

        Assert.NotNull(rebuilt);
        using var rebuiltBitmap = RenderBitmap(rebuilt!);
        Assert.Equal(new SkiaColor(0, 0, 0, 255), rebuiltBitmap.GetPixel(5, 5));
    }

    [Fact]
    public void RebuildFromModel_ReflectsMutatedPathAfterInitialNativeBuild()
    {
        var svg = new SKSvg();
        svg.FromSvg(SampleSvg);

        Assert.NotNull(svg.Picture);
        using var originalBitmap = RenderBitmap(svg.Picture!);
        Assert.Equal(new SkiaColor(255, 0, 0, 255), originalBitmap.GetPixel(8, 8));

        var command = Assert.IsType<DrawPathCanvasCommand>(svg.Model?.FindCommands<DrawPathCanvasCommand>().First());
        Assert.NotNull(command.Path);
        command.Path!.Commands!.Clear();
        command.Path.AddRect(ShimSkiaSharp.SKRect.Create(0f, 0f, 4f, 4f));

        var rebuilt = svg.RebuildFromModel();

        Assert.NotNull(rebuilt);
        using var rebuiltBitmap = RenderBitmap(rebuilt!);
        Assert.Equal(new SkiaColor(255, 0, 0, 255), rebuiltBitmap.GetPixel(2, 2));
        Assert.Equal(SKColors.White, rebuiltBitmap.GetPixel(8, 8));
    }

    [Fact]
    public void RebuildFromModel_ReflectsMutatedGradientShaderAfterInitialNativeBuild()
    {
        var svg = new SKSvg();
        svg.FromSvg(GradientSvg);

        Assert.NotNull(svg.Picture);
        using var originalBitmap = RenderBitmap(svg.Picture!);
        Assert.NotEqual(new SkiaColor(0, 0, 0, 255), originalBitmap.GetPixel(5, 5));

        var command = Assert.IsType<DrawPathCanvasCommand>(svg.Model?.FindCommands<DrawPathCanvasCommand>().First());
        var shader = Assert.IsType<LinearGradientShader>(command.Paint?.Shader);
        Assert.NotNull(shader.Colors);
        shader.Colors![0] = new ShimSkiaSharp.SKColorF(0f, 0f, 0f, 1f);
        shader.Colors[1] = new ShimSkiaSharp.SKColorF(0f, 0f, 0f, 1f);

        var rebuilt = svg.RebuildFromModel();

        Assert.NotNull(rebuilt);
        using var rebuiltBitmap = RenderBitmap(rebuilt!);
        Assert.Equal(new SkiaColor(0, 0, 0, 255), rebuiltBitmap.GetPixel(5, 5));
    }

    private static SkiaBitmap RenderBitmap(SkiaPicture picture)
    {
        var bitmap = picture.ToBitmap(
            SKColors.White,
            1f,
            1f,
            SKColorType.Rgba8888,
            SKAlphaType.Unpremul,
            SkiaColorSpace.CreateSrgb());

        return Assert.IsType<SkiaBitmap>(bitmap);
    }
}
