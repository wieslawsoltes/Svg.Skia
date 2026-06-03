using System;
using System.Collections.Generic;
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
    private const string SourceMappedSvg = """
        <svg width="20" height="10" xmlns="http://www.w3.org/2000/svg">
          <rect id="left" x="0" y="0" width="10" height="10" fill="red" />
          <rect id="right" x="10" y="0" width="10" height="10" fill="blue" />
        </svg>
        """;
    private const string SourceMappedStrokeSvg = """
        <svg width="20" height="20" xmlns="http://www.w3.org/2000/svg">
          <rect id="target" x="2" y="2" width="16" height="16" fill="red" stroke="blue" stroke-width="2" />
        </svg>
        """;
    private const string SourceMappedTextRunsSvg = """
        <svg width="80" height="24" xmlns="http://www.w3.org/2000/svg">
          <text id="text-root" x="2" y="18" font-family="sans-serif" font-size="16" fill="black"><tspan id="a">A</tspan><tspan id="b">B</tspan></text>
        </svg>
        """;
    private const string SourceMappedSimpleFilteredTextPathSvg = """
        <svg width="100" height="50" xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink">
          <defs>
            <path id="curve" d="M 5 30 C 25 5 55 55 85 30" />
            <filter id="blur" x="-20%" y="-20%" width="140%" height="140%">
              <feGaussianBlur in="SourceGraphic" stdDeviation="1" />
            </filter>
          </defs>
          <text font-family="sans-serif" font-size="14" fill="black">
            <textPath id="path-text" xlink:href="#curve" filter="url(#blur)">Arc</textPath>
          </text>
        </svg>
        """;
    private const string SourceMappedComplexFilteredTextPathSvg = """
        <svg width="100" height="50" xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink">
          <defs>
            <path id="curve" d="M 5 30 C 25 5 55 55 85 30" />
            <filter id="offset" x="-20%" y="-20%" width="140%" height="140%">
              <feOffset in="SourceGraphic" dx="1" dy="1" />
            </filter>
          </defs>
          <text font-family="sans-serif" font-size="14" fill="black">
            <textPath id="path-text" xlink:href="#curve" filter="url(#offset)">Arc</textPath>
          </text>
        </svg>
        """;
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
    public void ModelCommands_ExposeSourceElementMetadata()
    {
        var svg = new SKSvg();
        svg.FromSvg(SourceMappedStrokeSvg);

        var commands = svg.Model!
            .FindCommandsBySourceElementId<DrawPathCanvasCommand>("target")
            .ToList();

        Assert.Equal(2, commands.Count);
        Assert.All(commands, static command =>
        {
            Assert.Equal("target", command.SourceElementId);
            Assert.Equal("SvgRectangle", command.SourceElementTypeName);
            Assert.False(string.IsNullOrWhiteSpace(command.SourceElementAddress));
        });

        var address = commands[0].SourceElementAddress!;
        var commandsByAddress = svg.Model!
            .FindCommandsBySourceElementAddress<DrawPathCanvasCommand>(address)
            .ToList();

        Assert.Equal(commands.Count, commandsByAddress.Count);
        Assert.All(commandsByAddress, command => Assert.Equal(address, command.SourceElementAddress));
    }

    [Fact]
    public void ModelCommands_PreserveChildTextRunSourceMetadata()
    {
        var svg = new SKSvg();
        svg.FromSvg(SourceMappedTextRunsSvg);

        var firstRunCommands = svg.Model!
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("a")
            .ToList();
        var secondRunCommands = svg.Model!
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("b")
            .ToList();
        var parentTextRunCommands = svg.Model!
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("text-root")
            .Where(static command => command.Text is "A" or "B")
            .ToList();

        var firstRunCommand = Assert.Single(firstRunCommands);
        var secondRunCommand = Assert.Single(secondRunCommands);
        Assert.Equal("A", firstRunCommand.Text);
        Assert.Equal("B", secondRunCommand.Text);
        Assert.Equal("SvgTextSpan", firstRunCommand.SourceElementTypeName);
        Assert.Equal("SvgTextSpan", secondRunCommand.SourceElementTypeName);
        Assert.False(string.IsNullOrWhiteSpace(firstRunCommand.SourceElementAddress));
        Assert.False(string.IsNullOrWhiteSpace(secondRunCommand.SourceElementAddress));
        Assert.Empty(parentTextRunCommands);
    }

    [Fact]
    public void ModelCommands_PreserveSimpleFilteredTextPathWrapperSourceMetadata()
    {
        var svg = new SKSvg();
        svg.FromSvg(SourceMappedSimpleFilteredTextPathSvg);

        var sourceCommands = svg.Model!
            .FindCommandsBySourceElementId("path-text")
            .ToList();

        Assert.Contains(sourceCommands, static command => command is SaveCanvasCommand);
        Assert.Contains(sourceCommands, static command => command is ClipRectCanvasCommand);
        Assert.Contains(sourceCommands, static command => command is SaveLayerCanvasCommand);
        Assert.Contains(sourceCommands, static command => command is RestoreCanvasCommand);
        Assert.All(
            sourceCommands.Where(static command =>
                command is SaveCanvasCommand or ClipRectCanvasCommand or SaveLayerCanvasCommand or RestoreCanvasCommand),
            AssertTextPathCommandSource);
    }

    [Fact]
    public void ModelCommands_PreserveComplexFilteredTextPathWrapperSourceMetadata()
    {
        var svg = new SKSvg();
        svg.FromSvg(SourceMappedComplexFilteredTextPathSvg);

        var sourceCommands = svg.Model!
            .FindCommandsBySourceElementId("path-text")
            .ToList();

        Assert.Contains(sourceCommands, static command => command is SaveCanvasCommand);
        Assert.Contains(sourceCommands, static command => command is ClipRectCanvasCommand);
        Assert.Contains(sourceCommands, static command => command is SaveLayerCanvasCommand);
        Assert.Contains(sourceCommands, static command => command is DrawPictureCanvasCommand);
        Assert.Contains(sourceCommands, static command => command is RestoreCanvasCommand);
        Assert.All(
            sourceCommands.Where(static command =>
                command is SaveCanvasCommand or ClipRectCanvasCommand or SaveLayerCanvasCommand or DrawPictureCanvasCommand or RestoreCanvasCommand),
            AssertTextPathCommandSource);
    }

    [Fact]
    public void RebuildFromModel_CanUpdateCommandsForSourceElementId()
    {
        var svg = new SKSvg();
        svg.FromSvg(SourceMappedSvg);

        Assert.NotNull(svg.Picture);
        using var originalBitmap = RenderBitmap(svg.Picture!);
        Assert.Equal(new SkiaColor(255, 0, 0, 255), originalBitmap.GetPixel(5, 5));
        Assert.Equal(new SkiaColor(0, 0, 255, 255), originalBitmap.GetPixel(15, 5));

        var rightCommands = svg.Model!
            .FindCommandsBySourceElementId<DrawPathCanvasCommand>("right")
            .ToList();
        var command = Assert.Single(rightCommands);
        Assert.NotNull(command.Paint);
        command.Paint!.Color = new ShimSkiaSharp.SKColor(0, 128, 0, 255);

        var rebuilt = svg.RebuildFromModel();

        Assert.NotNull(rebuilt);
        using var rebuiltBitmap = RenderBitmap(rebuilt!);
        Assert.Equal(new SkiaColor(255, 0, 0, 255), rebuiltBitmap.GetPixel(5, 5));
        Assert.Equal(new SkiaColor(0, 128, 0, 255), rebuiltBitmap.GetPixel(15, 5));
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
    public void DirectDraw_ReflectsMutatedComplexPaintAfterCachedReplay()
    {
        var paint = new ShimSkiaSharp.SKPaint
        {
            Style = ShimSkiaSharp.SKPaintStyle.Fill,
            Color = new ShimSkiaSharp.SKColor(255, 0, 0, 255),
            ImageFilter = ShimSkiaSharp.SKImageFilter.CreateBlur(0.1f, 0.1f)
        };
        var path = new ShimSkiaSharp.SKPath();
        path.AddRect(ShimSkiaSharp.SKRect.Create(0, 0, 10, 10));
        var picture = new ShimSkiaSharp.SKPicture(
            ShimSkiaSharp.SKRect.Create(0, 0, 10, 10),
            new CanvasCommand[]
            {
                new DrawPathCanvasCommand(path, paint)
            });
        var skiaModel = new SkiaModel(new SKSvgSettings());

        using var originalBitmap = RenderModelBitmap(skiaModel, picture, 10, 10);
        Assert.Equal(new SkiaColor(255, 0, 0, 255), originalBitmap.GetPixel(5, 5));

        using var cachedBitmap = RenderModelBitmap(skiaModel, picture, 10, 10);
        Assert.Equal(new SkiaColor(255, 0, 0, 255), cachedBitmap.GetPixel(5, 5));

        paint.Color = new ShimSkiaSharp.SKColor(0, 0, 255, 255);

        using var mutatedBitmap = RenderModelBitmap(skiaModel, picture, 10, 10);
        Assert.Equal(new SkiaColor(0, 0, 255, 255), mutatedBitmap.GetPixel(5, 5));
    }

    [Fact]
    public void DirectDraw_ReflectsMutatedTextShaderAfterCachedReplay()
    {
        var colors = new[]
        {
            new ShimSkiaSharp.SKColorF(1f, 0f, 0f, 1f),
            new ShimSkiaSharp.SKColorF(1f, 0f, 0f, 1f)
        };
        var paint = new ShimSkiaSharp.SKPaint
        {
            Style = ShimSkiaSharp.SKPaintStyle.Fill,
            TextSize = 24f,
            Shader = ShimSkiaSharp.SKShader.CreateLinearGradient(
                new ShimSkiaSharp.SKPoint(0f, 0f),
                new ShimSkiaSharp.SKPoint(40f, 0f),
                colors,
                ShimSkiaSharp.SKColorSpace.Srgb,
                null,
                ShimSkiaSharp.SKShaderTileMode.Clamp)
        };
        var picture = new ShimSkiaSharp.SKPicture(
            ShimSkiaSharp.SKRect.Create(0, 0, 48, 32),
            new CanvasCommand[]
            {
                new DrawTextCanvasCommand("X", 4f, 24f, paint)
            });
        var skiaModel = new SkiaModel(new SKSvgSettings());

        using var originalBitmap = RenderModelBitmap(skiaModel, picture, 48, 32);
        Assert.True(CountPixels(originalBitmap, IsRed) > 0);

        using var cachedBitmap = RenderModelBitmap(skiaModel, picture, 48, 32);
        Assert.True(CountPixels(cachedBitmap, IsRed) > 0);

        colors[0] = new ShimSkiaSharp.SKColorF(0f, 0f, 1f, 1f);
        colors[1] = new ShimSkiaSharp.SKColorF(0f, 0f, 1f, 1f);

        using var mutatedBitmap = RenderModelBitmap(skiaModel, picture, 48, 32);
        Assert.True(CountPixels(mutatedBitmap, IsBlue) > 0);
        Assert.Equal(0, CountPixels(mutatedBitmap, IsRed));
    }

    [Fact]
    public void DirectDraw_ReflectsMutatedNestedPictureAfterCachedReplay()
    {
        var paint = new ShimSkiaSharp.SKPaint
        {
            Style = ShimSkiaSharp.SKPaintStyle.Fill,
            Color = new ShimSkiaSharp.SKColor(255, 0, 0, 255)
        };
        var path = new ShimSkiaSharp.SKPath();
        path.AddRect(ShimSkiaSharp.SKRect.Create(0, 0, 10, 10));
        var nestedPicture = new ShimSkiaSharp.SKPicture(
            ShimSkiaSharp.SKRect.Create(0, 0, 10, 10),
            new CanvasCommand[]
            {
                new DrawPathCanvasCommand(path, paint)
            });
        var picture = new ShimSkiaSharp.SKPicture(
            ShimSkiaSharp.SKRect.Create(0, 0, 10, 10),
            new CanvasCommand[]
            {
                new DrawPictureCanvasCommand(nestedPicture)
            });
        var skiaModel = new SkiaModel(new SKSvgSettings());

        using var originalBitmap = RenderModelBitmap(skiaModel, picture, 10, 10);
        Assert.Equal(new SkiaColor(255, 0, 0, 255), originalBitmap.GetPixel(5, 5));

        using var cachedBitmap = RenderModelBitmap(skiaModel, picture, 10, 10);
        Assert.Equal(new SkiaColor(255, 0, 0, 255), cachedBitmap.GetPixel(5, 5));

        paint.Color = new ShimSkiaSharp.SKColor(0, 0, 255, 255);

        using var mutatedBitmap = RenderModelBitmap(skiaModel, picture, 10, 10);
        Assert.Equal(new SkiaColor(0, 0, 255, 255), mutatedBitmap.GetPixel(5, 5));
    }

    [Fact]
    public void DirectDraw_ReflectsReplacedCommandAfterCachedReplay()
    {
        static DrawPathCanvasCommand CreateCommand(SkiaColor color)
        {
            var path = new ShimSkiaSharp.SKPath();
            path.AddRect(ShimSkiaSharp.SKRect.Create(0, 0, 10, 10));
            return new DrawPathCanvasCommand(
                path,
                new ShimSkiaSharp.SKPaint
                {
                    Style = ShimSkiaSharp.SKPaintStyle.Fill,
                    Color = new ShimSkiaSharp.SKColor(color.Red, color.Green, color.Blue, color.Alpha)
                });
        }

        var commands = new List<CanvasCommand>
        {
            CreateCommand(new SkiaColor(255, 0, 0, 255))
        };
        var picture = new ShimSkiaSharp.SKPicture(
            ShimSkiaSharp.SKRect.Create(0, 0, 10, 10),
            commands);
        var skiaModel = new SkiaModel(new SKSvgSettings());

        using var originalBitmap = RenderModelBitmap(skiaModel, picture, 10, 10);
        Assert.Equal(new SkiaColor(255, 0, 0, 255), originalBitmap.GetPixel(5, 5));

        using var cachedBitmap = RenderModelBitmap(skiaModel, picture, 10, 10);
        Assert.Equal(new SkiaColor(255, 0, 0, 255), cachedBitmap.GetPixel(5, 5));

        commands[0] = CreateCommand(new SkiaColor(0, 0, 255, 255));

        using var mutatedBitmap = RenderModelBitmap(skiaModel, picture, 10, 10);
        Assert.Equal(new SkiaColor(0, 0, 255, 255), mutatedBitmap.GetPixel(5, 5));
    }

    [Fact]
    public void ToSKPicture_UsesRecordedImageSampling()
    {
        var image = new ShimSkiaSharp.SKImage
        {
            Data = CreateEncodedBitmap(2, 1, static (x, _) => x == 0
                ? new SkiaColor(255, 0, 0, 255)
                : new SkiaColor(0, 0, 255, 255)),
            Width = 2,
            Height = 1
        };
        var picture = new ShimSkiaSharp.SKPicture(
            ShimSkiaSharp.SKRect.Create(0, 0, 20, 1),
            new CanvasCommand[]
            {
                new DrawImageCanvasCommand(
                    image,
                    ShimSkiaSharp.SKRect.Create(0, 0, 2, 1),
                    ShimSkiaSharp.SKRect.Create(0, 0, 20, 1),
                    new ShimSkiaSharp.SKPaint { FilterQuality = ShimSkiaSharp.SKFilterQuality.None },
                    new ShimSkiaSharp.SKSamplingOptions(ShimSkiaSharp.SKFilterMode.Linear, ShimSkiaSharp.SKMipmapMode.None))
            });
        var skiaModel = new SkiaModel(new SKSvgSettings());

        using var renderedPicture = skiaModel.ToSKPicture(picture);
        using var bitmap = RenderBitmap(Assert.IsType<SkiaPicture>(renderedPicture));

        var blendedPixels = CountPixels(bitmap, static color => color.Red > 0 && color.Blue > 0 && color.Green < 16);
        Assert.True(blendedPixels > 0);
    }

    [Fact]
    public void ToSKPicture_UsesRecordedTextFontAndAlignment()
    {
        var paint = new ShimSkiaSharp.SKPaint
        {
            Color = new ShimSkiaSharp.SKColor(0, 0, 0, 255),
            IsAntialias = false,
            TextAlign = ShimSkiaSharp.SKTextAlign.Left,
            TextSize = 8f
        };
        var font = new ShimSkiaSharp.SKFont(null, 32f)
        {
            Edging = ShimSkiaSharp.SKFontEdging.Alias
        };
        var picture = new ShimSkiaSharp.SKPicture(
            ShimSkiaSharp.SKRect.Create(0, 0, 80, 45),
            new CanvasCommand[]
            {
                new DrawTextCanvasCommand("MM", 40f, 34f, paint, ShimSkiaSharp.SKTextAlign.Right, font)
            });
        var skiaModel = new SkiaModel(new SKSvgSettings());

        using var renderedPicture = skiaModel.ToSKPicture(picture);
        using var bitmap = RenderBitmap(Assert.IsType<SkiaPicture>(renderedPicture));

        var leftDarkPixels = CountPixels(bitmap, static (x, _, color) => x < 40 && IsDark(color));
        var rightDarkPixels = CountPixels(bitmap, static (x, _, color) => x >= 40 && IsDark(color));
        Assert.True(leftDarkPixels > 50);
        Assert.True(leftDarkPixels > rightDarkPixels * 3);
    }

    [Fact]
    public void ToSKFont_MapsAliasedPaintToAliasEdging()
    {
        var skiaModel = new SkiaModel(new SKSvgSettings());

        using var font = skiaModel.ToSKFont(new ShimSkiaSharp.SKPaint
        {
            IsAntialias = false,
            LcdRenderText = true
        });

        Assert.Equal(SkiaSharp.SKFontEdging.Alias, font.Edging);
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

    private static SkiaBitmap RenderModelBitmap(SkiaModel skiaModel, ShimSkiaSharp.SKPicture picture, int width, int height)
    {
        var bitmap = new SkiaBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        skiaModel.Draw(picture, canvas);
        return bitmap;
    }

    private static int CountPixels(SkiaBitmap bitmap, Func<SkiaColor, bool> predicate)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (predicate(bitmap.GetPixel(x, y)))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static int CountPixels(SkiaBitmap bitmap, Func<int, int, SkiaColor, bool> predicate)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (predicate(x, y, bitmap.GetPixel(x, y)))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static bool IsDark(SkiaColor color)
    {
        return color.Red < 128 && color.Green < 128 && color.Blue < 128;
    }

    private static bool IsRed(SkiaColor color)
    {
        return color.Red > 160 && color.Green < 96 && color.Blue < 96;
    }

    private static bool IsBlue(SkiaColor color)
    {
        return color.Blue > 160 && color.Red < 96 && color.Green < 96;
    }

    private static void AssertTextPathCommandSource(CanvasCommand command)
    {
        Assert.Equal("path-text", command.SourceElementId);
        Assert.Equal("SvgTextPath", command.SourceElementTypeName);
        Assert.False(string.IsNullOrWhiteSpace(command.SourceElementAddress));
    }

    private static byte[] CreateEncodedBitmap(int width, int height, Func<int, int, SkiaColor> getColor)
    {
        using var bitmap = new SkiaBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, getColor(x, y));
            }
        }

        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
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
}
