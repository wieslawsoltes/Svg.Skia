using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;
using Svg.Skia;
using UnoDraw.Rendering;
using Xunit;
using Shim = ShimSkiaSharp;

namespace Svg.Editor.Skia.UnitTests;

public class SkiaDocumentDrawableTests
{
    private const string TextScene = """
        <svg xmlns="http://www.w3.org/2000/svg" width="400" height="220">
          <defs><clipPath id="clip"><rect x="5.25" y="2.5" width="385" height="205" rx="9.5"/></clipPath>
            <linearGradient id="g"><stop stop-color="#b92673"/><stop offset="1" stop-color="#216bc4"/></linearGradient></defs>
          <rect width="400" height="220" fill="#dfe9e2"/>
          <g clip-path="url(#clip)" opacity=".73" transform="translate(6.25 7.5)">
            <text x="8.25" y="88.5" font-family="serif" font-size="72.75" fill="url(#g)">Field</text>
            <g transform="rotate(3 90 140)"><text x="12.5" y="162.25" font-family="monospace"
              font-size="36.5" fill="#216bc4" stroke="#32a458" stroke-width=".45">SVG canvas</text></g>
          </g>
        </svg>
        """;

    public static IEnumerable<object[]> FixtureCases()
    {
        foreach (var scene in new[] { "gradients", "filters", "mask", "clip", "pattern", "text", "image", "opacity-and-strokes" })
            foreach (var dpr in new[] { 1f, 1.25f, 2f })
                yield return [scene, dpr];
    }

    [Theory]
    [MemberData(nameof(FixtureCases))]
    public void ProductionDrawablePreservesExistingPainterAndNativePixelPhase(string scene, float dpr)
    {
        using var svg = new SKSvg();
        svg.FromSvg(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "CompositeFixtures", scene + ".svg")));
        using var drawable = new SkiaDocumentDrawable(svg.SkiaModel, svg.Model!);
        Assert.Equal(0, drawable.TextRasterizationCount);
        using var expected = Raster(dpr);
        using var actual = Raster(dpr);
        Draw(expected.Canvas, dpr, canvas => svg.Draw(canvas));
        Draw(actual.Canvas, dpr, drawable.Draw);
        AssertCoveragePreserved(expected, actual);
        if (scene == "text")
            Assert.True(drawable.TextRasterizationCount > 0);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.25f)]
    [InlineData(2f)]
    public void LiveTextPreservesClipLayerTransformAndNoncommutativeLayerBlending(float dpr)
    {
        using var svg = new SKSvg();
        svg.FromSvg(TextScene);
        var layers = 0;
        ChangeLayerBlends(svg.Model!);
        Assert.True(layers > 0);
        svg.RebuildFromModel();
        using var drawable = new SkiaDocumentDrawable(svg.SkiaModel, svg.Model!);
        Assert.Equal(0, drawable.TextRasterizationCount);
        using var expected = Raster(dpr);
        using var actual = Raster(dpr);
        Draw(expected.Canvas, dpr, canvas => svg.Draw(canvas));
        Draw(actual.Canvas, dpr, drawable.Draw);
        AssertCoveragePreserved(expected, actual);
        Assert.True(drawable.TextRasterizationCount > 0);

        void ChangeLayerBlends(Shim.SKPicture picture)
        {
            if (picture.Commands is null)
                return;
            foreach (var command in picture.Commands)
            {
                if (command is Shim.SaveLayerCanvasCommand { Paint: { } paint })
                {
                    paint.BlendMode = Shim.SKBlendMode.Multiply;
                    layers++;
                }
                if (command is Shim.DrawPictureCanvasCommand { Picture: { } child })
                    ChangeLayerBlends(child);
            }
        }
    }

    [Fact]
    public void TightTilesReuseIntegerTranslationButRerasterizeChangedGlyphPhase()
    {
        using var svg = new SKSvg();
        svg.FromSvg(TextScene);
        using var drawable = new SkiaDocumentDrawable(svg.SkiaModel, svg.Model!);
        using var surface = SKSurface.Create(new SKImageInfo(1200, 800, SKColorType.Bgra8888, SKAlphaType.Premul));
        var matrix = SKMatrix.CreateScaleTranslation(1.25f, 1.25f, 25.375f, 17.125f);
        surface.Canvas.SetMatrix(in matrix);
        drawable.Draw(surface.Canvas);
        var count = drawable.TextRasterizationCount;
        Assert.True(count > 0);
        Assert.InRange(drawable.RetainedTextBytes, 1, 1200L * 800 * 4 - 1);
        matrix.TransX += 1;
        surface.Canvas.SetMatrix(in matrix);
        drawable.Draw(surface.Canvas);
        Assert.Equal(count, drawable.TextRasterizationCount);
        matrix.TransX += .25f;
        surface.Canvas.SetMatrix(in matrix);
        drawable.Draw(surface.Canvas);
        Assert.True(drawable.TextRasterizationCount > count);
        drawable.Dispose();
        Assert.Equal(0, drawable.RetainedTextBytes);
        Assert.Throws<ObjectDisposedException>(() => drawable.Draw(surface.Canvas));
    }

    [Fact]
    public void UnsupportedDeviceMatrixReportsAfterNativeCallbackAndRestoresCanvas()
    {
        using var svg = new SKSvg();
        svg.FromSvg(TextScene);
        using var drawable = new SkiaDocumentDrawable(svg.SkiaModel, svg.Model!);
        using var surface = Raster(1);
        var matrix = SKMatrix.Identity;
        matrix.Persp0 = .0001f;
        surface.Canvas.SetMatrix(in matrix);
        var count = surface.Canvas.SaveCount;
        Assert.Throws<SkiaDocumentRasterizationException>(() => drawable.Draw(surface.Canvas));
        Assert.Equal(matrix, surface.Canvas.TotalMatrix);
        Assert.Equal(count, surface.Canvas.SaveCount);
    }

    [Fact]
    public void DestinationDependentTextAndNonScalingStrokesRequireOriginalDocumentPainter()
    {
        var converter = new SkiaModel(new SKSvgSettings());
        var text = new Shim.SKPaint { TextSize = 24, BlendMode = Shim.SKBlendMode.DstIn };
        var picture = Model(new Shim.DrawTextCanvasCommand("Field", 10, 40, text));
        Assert.Throws<SkiaDocumentRasterizationException>(() => new SkiaDocumentDrawable(converter, picture));
        text.BlendMode = Shim.SKBlendMode.SrcOver;
        text.IsStrokeNonScaling = true;
        Assert.Throws<SkiaDocumentRasterizationException>(() => new SkiaDocumentDrawable(converter, picture));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TextInsideShaderOrFilterPicturesCannotBypassTheTextPolicy(bool filter)
    {
        var converter = new SkiaModel(new SKSvgSettings());
        var text = Model(new Shim.DrawTextCanvasCommand("Field", 10, 40, new Shim.SKPaint { TextSize = 24 }));
        var paint = new Shim.SKPaint();
        if (filter)
            paint.ImageFilter = new Shim.PictureImageFilter(text, null);
        else
            paint.Shader = new Shim.PictureShader(text, Shim.SKShaderTileMode.Repeat, Shim.SKShaderTileMode.Repeat,
                Shim.SKMatrix.Identity, text.CullRect);
        var root = Model(new Shim.DrawPathCanvasCommand(null, paint));
        Assert.Throws<SkiaDocumentRasterizationException>(() => new SkiaDocumentDrawable(converter, root));
    }

    [Fact]
    public void CrossKindPaintFilterCyclesAreRejectedBeforeNativeConversion()
    {
        var converter = new SkiaModel(new SKSvgSettings());
        var paint = new Shim.SKPaint();
        paint.ImageFilter = new Shim.PaintImageFilter(paint, null);
        var model = Model(new Shim.DrawPathCanvasCommand(null, paint));
        Assert.Throws<SkiaDocumentRasterizationException>(() => new SkiaDocumentDrawable(converter, model));
    }

    private static Shim.SKPicture Model(params Shim.CanvasCommand[] commands) =>
        new(new Shim.SKRect(0, 0, 400, 220), commands);

    private static SKSurface Raster(float dpr) => SKSurface.Create(new SKImageInfo(
        (int)Math.Ceiling(600 * dpr), (int)Math.Ceiling(400 * dpr), SKColorType.Bgra8888, SKAlphaType.Premul));

    private static void Draw(SKCanvas canvas, float dpr, Action<SKCanvas> paint)
    {
        canvas.Clear(new SKColor(247, 246, 242));
        canvas.Scale(dpr);
        canvas.ClipRect(new SKRect(0, 0, 600, 400));
        canvas.Translate(13.375f, 7.625f);
        canvas.Scale(1.375f);
        paint(canvas);
    }

    private static void AssertCoveragePreserved(SKSurface expected, SKSurface actual)
    {
        using var a = expected.PeekPixels();
        using var b = actual.PeekPixels();
        Assert.Equal(a.Info, b.Info);
        var before = a.GetPixelSpan();
        var after = b.GetPixelSpan();
        // An extra premultiplied text composition can round a few edge channels.
        // This bound is stricter than the browser gate (luminance12/0.1%); no
        // geometry, font hinting, gamma, alpha or visible coverage change is allowed.
        for (var i = 0; i < before.Length; i++)
            Assert.InRange(Math.Abs(before[i] - after[i]), 0, 4);
    }
}
