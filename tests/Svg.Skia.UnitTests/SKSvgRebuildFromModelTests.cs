using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ShimSkiaSharp;
using ShimSkiaSharp.Editing;
using SkiaSharp;
using Svg;
using Svg.Model.Services;
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
    public void FromSvg_LeavesModelDeferredUntilRequested()
    {
        var svg = new SKSvg();

        var picture = svg.FromSvg(SampleSvg);

        Assert.NotNull(picture);
        Assert.Null(GetCachedModel(svg));

        var model = svg.Model;

        Assert.NotNull(model);
        Assert.Same(model, GetCachedModel(svg));
    }

    [Fact]
    public void FromSvgDocument_LeavesModelDeferredUntilRequested()
    {
        var svg = new SKSvg();
        var document = SvgService.FromSvg(SampleSvg);

        var picture = svg.FromSvgDocument(document);

        Assert.NotNull(picture);
        Assert.Same(picture, svg.Picture);
        Assert.NotNull(svg.RetainedSceneGraph);
        Assert.Null(GetCachedModel(svg));

        var model = svg.Model;

        Assert.NotNull(model);
        Assert.Same(model, GetCachedModel(svg));
    }

    [Fact]
    public void FromSvgDocument_SameStaticDocumentReload_LeavesModelDeferredUntilRequested()
    {
        var svg = new SKSvg();
        svg.FromSvg(SampleSvg);

        var document = Assert.IsType<SvgDocument>(svg.SourceDocument);
        var rect = Assert.IsType<SvgRectangle>(document.Children[0]);
        rect.Fill = new SvgColourServer(System.Drawing.Color.Blue);

        var picture = svg.FromSvgDocument(document);

        Assert.NotNull(picture);
        Assert.Same(picture, svg.Picture);
        Assert.NotNull(svg.RetainedSceneGraph);
        Assert.Null(GetCachedModel(svg));

        using var bitmap = RenderBitmap(Assert.IsType<SkiaPicture>(picture));
        Assert.Equal(new SkiaColor(0, 0, 255, 255), bitmap.GetPixel(5, 5));

        var model = svg.Model;

        Assert.NotNull(model);
        Assert.Same(model, GetCachedModel(svg));
    }

    [Fact]
    public void TryApplyRetainedSceneMutationAndRender_LeavesModelDeferredUntilRequested()
    {
        var svg = new SKSvg();
        var picture = svg.FromSvg(SampleSvg);

        Assert.NotNull(picture);
        Assert.Null(GetCachedModel(svg));

        var document = Assert.IsType<SvgDocument>(svg.SourceDocument);
        var rect = Assert.IsType<SvgRectangle>(document.Children[0]);
        rect.Fill = new SvgColourServer(System.Drawing.Color.Blue);

        Assert.True(svg.TryApplyRetainedSceneMutationAndRender(rect, new[] { "fill" }, out var result));
        Assert.NotNull(result);
        Assert.True(result!.CompilationRootCount >= 1);
        Assert.NotNull(svg.Picture);
        Assert.Null(GetCachedModel(svg));

        using var bitmap = RenderBitmap(Assert.IsType<SkiaPicture>(svg.Picture));
        Assert.Equal(new SkiaColor(0, 0, 255, 255), bitmap.GetPixel(5, 5));

        var model = svg.Model;

        Assert.NotNull(model);
        Assert.Same(model, GetCachedModel(svg));
    }

    [Fact]
    public void RebuildFromModel_MaterializesDeferredModel()
    {
        var svg = new SKSvg();
        svg.FromSvg(SampleSvg);

        Assert.Null(GetCachedModel(svg));

        var rebuilt = svg.RebuildFromModel();

        Assert.NotNull(rebuilt);
        Assert.NotNull(svg.Model);
        Assert.Same(svg.Model, GetCachedModel(svg));
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

    [Fact]
    public void ToSKPicture_ReflectsInPlaceMutatedImageDataAfterInitialNativeBuild()
    {
        var initialData = CreateEncodedSolidBitmap(new SkiaColor(255, 0, 0, 255));
        var mutatedData = CreateEncodedSolidBitmap(new SkiaColor(0, 0, 255, 255));
        Assert.Equal(mutatedData.Length, initialData.Length);

        var image = new ShimSkiaSharp.SKImage
        {
            Data = initialData,
            Width = 1,
            Height = 1
        };
        var picture = new ShimSkiaSharp.SKPicture(
            ShimSkiaSharp.SKRect.Create(0, 0, 1, 1),
            new CanvasCommand[]
            {
                new DrawImageCanvasCommand(
                    image,
                    ShimSkiaSharp.SKRect.Create(0, 0, 1, 1),
                    ShimSkiaSharp.SKRect.Create(0, 0, 1, 1))
            });
        var skiaModel = new SkiaModel(new SKSvgSettings());

        using var originalPicture = skiaModel.ToSKPicture(picture);
        using var originalBitmap = RenderBitmap(Assert.IsType<SkiaPicture>(originalPicture));
        Assert.Equal(new SkiaColor(255, 0, 0, 255), originalBitmap.GetPixel(0, 0));

        Array.Copy(mutatedData, initialData, initialData.Length);

        using var rebuiltPicture = skiaModel.ToSKPicture(picture);
        using var rebuiltBitmap = RenderBitmap(Assert.IsType<SkiaPicture>(rebuiltPicture));
        Assert.Equal(new SkiaColor(0, 0, 255, 255), rebuiltBitmap.GetPixel(0, 0));
    }

    [Fact]
    public void ToSKPicture_ReflectsInPlaceMutatedPolyPointsAfterInitialNativeBuild()
    {
        var points = new List<ShimSkiaSharp.SKPoint>
        {
            new(0f, 0f),
            new(4f, 0f),
            new(4f, 10f),
            new(0f, 10f)
        };
        var path = new ShimSkiaSharp.SKPath();
        path.Commands!.Add(new AddPolyPathCommand(points, true));

        var picture = new ShimSkiaSharp.SKPicture(
            ShimSkiaSharp.SKRect.Create(0, 0, 10, 10),
            new CanvasCommand[]
            {
                new DrawPathCanvasCommand(
                    path,
                    new ShimSkiaSharp.SKPaint
                    {
                        Style = ShimSkiaSharp.SKPaintStyle.Fill,
                        Color = new ShimSkiaSharp.SKColor(255, 0, 0, 255)
                    })
            });
        var skiaModel = new SkiaModel(new SKSvgSettings());

        using var originalPicture = skiaModel.ToSKPicture(picture);
        using var originalBitmap = RenderBitmap(Assert.IsType<SkiaPicture>(originalPicture));
        Assert.Equal(new SkiaColor(255, 0, 0, 255), originalBitmap.GetPixel(2, 5));
        Assert.Equal(SKColors.White, originalBitmap.GetPixel(8, 5));

        points[0] = new ShimSkiaSharp.SKPoint(6f, 0f);
        points[1] = new ShimSkiaSharp.SKPoint(10f, 0f);
        points[2] = new ShimSkiaSharp.SKPoint(10f, 10f);
        points[3] = new ShimSkiaSharp.SKPoint(6f, 10f);

        using var rebuiltPicture = skiaModel.ToSKPicture(picture);
        using var rebuiltBitmap = RenderBitmap(Assert.IsType<SkiaPicture>(rebuiltPicture));
        Assert.Equal(SKColors.White, rebuiltBitmap.GetPixel(2, 5));
        Assert.Equal(new SkiaColor(255, 0, 0, 255), rebuiltBitmap.GetPixel(8, 5));
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

    private static byte[] CreateEncodedSolidBitmap(SkiaColor color)
    {
        return new byte[]
        {
            0x42, 0x4D,
            0x3A, 0x00, 0x00, 0x00,
            0x00, 0x00,
            0x00, 0x00,
            0x36, 0x00, 0x00, 0x00,
            0x28, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
            0x01, 0x00,
            0x20, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x04, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            color.Blue,
            color.Green,
            color.Red,
            color.Alpha
        };
    }

    private static ShimSkiaSharp.SKPicture? GetCachedModel(SKSvg svg)
    {
        var field = typeof(SKSvg).GetField("_model", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return field!.GetValue(svg) as ShimSkiaSharp.SKPicture;
    }
}
