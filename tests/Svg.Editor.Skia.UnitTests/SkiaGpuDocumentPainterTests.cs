using System;
using System.Collections.Generic;
using SkiaSharp;
using Svg.Skia;
using UnoDraw.Rendering;
using Xunit;
using Shim = ShimSkiaSharp;

namespace Svg.Editor.Skia.UnitTests;

public class SkiaGpuDocumentPainterTests
{
    private const string Scene = """
        <svg xmlns="http://www.w3.org/2000/svg" width="240" height="160">
          <defs><linearGradient id="g"><stop stop-color="#b92673"/>
            <stop offset="1" stop-color="#216bc4"/></linearGradient></defs>
          <rect width="240" height="160" fill="url(#g)"/>
          <text x="8.25" y="98.5" font-family="serif" font-size="54.75" fill="#f6e9d7">Field</text>
        </svg>
        """;

    [Theory]
    [InlineData(1f)]
    [InlineData(1.25f)]
    [InlineData(2f)]
    public void SharedPainterPreservesTheExistingLiveDrawableAcrossCameraAndContentChanges(float dpr)
    {
        using var svg = new SKSvg();
        svg.FromSvg(Scene);
        using var painter = new SkiaGpuDocumentPainter();
        using var expected = Raster(dpr);
        using var actual = Raster(dpr);
        using var actualPixels = actual.PeekPixels();
        for (var frame = 0; frame < 3; frame++)
        {
            if (frame == 2)
                svg.FromSvg(Scene.Replace("#b92673", "#2fa374", StringComparison.Ordinal));
            var viewport = SKMatrix.CreateScaleTranslation(1.125f, 1.125f, 7.375f + frame * 2.25f, 3.625f);
            Assert.True(painter.Prepare(svg, actualPixels.Width, actualPixels.Height, dpr, 400, 260, viewport, out var reason));
            Assert.Null(reason);
            using var original = new SkiaDocumentDrawable(svg.SkiaModel, svg.Model!);
            Paint(expected.Canvas, dpr, viewport, original.Draw);
            Paint(actual.Canvas, dpr, viewport, canvas => painter.DrawDocument(svg, canvas));
            using var before = expected.PeekPixels();
            using var after = actual.PeekPixels();
            Assert.True(before.GetPixelSpan().SequenceEqual(after.GetPixelSpan()));
        }
    }

    [Fact]
    public void DynamicModeCanReturnToTheSameStaticDocumentWithoutStalePixels()
    {
        using var svg = new SKSvg();
        svg.FromSvg(Scene);
        using var painter = new SkiaGpuDocumentPainter();
        using var surface = Raster(1);
        Assert.True(Prepare(painter, svg));
        painter.DrawDocument(svg, surface.Canvas);
        svg.Wireframe = true;
        Assert.False(Prepare(painter, svg));
        svg.Wireframe = false;
        Assert.True(Prepare(painter, svg));
        surface.Canvas.Clear();
        painter.DrawDocument(svg, surface.Canvas);
    }

    [Theory]
    [InlineData("width")]
    [InlineData("height")]
    [InlineData("scale")]
    [InlineData("logicalWidth")]
    [InlineData("logicalHeight")]
    [InlineData("viewport")]
    [InlineData("viewer")]
    public void LiveRasterFailuresAreCachedUntilARelevantViewInputChanges(string changed)
    {
        using var svg = new SKSvg();
        svg.FromSvg(Scene);
        using var painter = new SkiaGpuDocumentPainter();
        using var surface = Raster(1);
        var viewport = SKMatrix.Identity;
        Assert.True(Prepare(painter, svg));
        var unsupported = SKMatrix.Identity;
        unsupported.Persp0 = .0001f;
        surface.Canvas.SetMatrix(in unsupported);
        Assert.Throws<SkiaDocumentRasterizationException>(() => painter.DrawDocument(svg, surface.Canvas));
        Assert.False(Prepare(painter, svg));
        var width = 400;
        var height = 260;
        var scale = 1d;
        var logicalWidth = 400d;
        var logicalHeight = 260d;
        switch (changed)
        {
            case "width":
                width++;
                break;
            case "height":
                height++;
                break;
            case "scale":
                scale = 1.25;
                break;
            case "logicalWidth":
                logicalWidth -= .1;
                break;
            case "logicalHeight":
                logicalHeight -= .1;
                break;
            case "viewport":
                viewport.TransX = .25f;
                break;
            case "viewer":
                Assert.True(svg.SetViewerTransform(1.125, new Shim.SKPoint(.25f, .5f)));
                break;
        }
        Assert.True(painter.Prepare(svg, width, height, scale, logicalWidth, logicalHeight, viewport, out var reason));
        Assert.Null(reason);
        surface.Canvas.ResetMatrix();
        painter.DrawDocument(svg, surface.Canvas);
    }

    [Fact]
    public void UnsupportedModelPolicyStaysCachedAcrossCameraChangesButRecoversAfterContentReplacement()
    {
        using var svg = new SKSvg();
        svg.FromSvg(Scene);
        var changed = 0;
        ChangeText(svg.Model!);
        Assert.True(changed > 0);
        svg.RebuildFromModel();
        using var painter = new SkiaGpuDocumentPainter();
        using var surface = Raster(1);
        Assert.True(Prepare(painter, svg));
        Assert.Throws<SkiaDocumentRasterizationException>(() => painter.DrawDocument(svg, surface.Canvas));
        Assert.False(painter.Prepare(svg, 800, 520, 2, 400, 260,
            SKMatrix.CreateTranslation(20, 10), out var reason));
        Assert.NotNull(reason);
        svg.FromSvg(Scene);
        Assert.True(Prepare(painter, svg));
        painter.DrawDocument(svg, surface.Canvas);

        void ChangeText(Shim.SKPicture picture)
        {
            foreach (var command in picture.Commands ?? new List<Shim.CanvasCommand>())
            {
                if (command is Shim.DrawTextCanvasCommand { Paint: { } paint })
                {
                    paint.BlendMode = Shim.SKBlendMode.DstIn;
                    changed++;
                }
                if (command is Shim.DrawTextBlobCanvasCommand { Paint: { } blobPaint })
                {
                    blobPaint.BlendMode = Shim.SKBlendMode.DstIn;
                    changed++;
                }
                if (command is Shim.DrawPictureCanvasCommand { Picture: { } child })
                    ChangeText(child);
            }
        }
    }

    [Fact]
    public void UnloadingFromThePublicDrawCallbackReleasesAfterDrawingAndRejectsFurtherUse()
    {
        using var svg = new SKSvg();
        svg.FromSvg(Scene);
        using var painter = new SkiaGpuDocumentPainter();
        using var surface = Raster(1);
        Assert.True(Prepare(painter, svg));
        svg.OnDraw += (_, _) => painter.Dispose();
        painter.DrawDocument(svg, surface.Canvas);
        painter.Dispose();
        Assert.Throws<ObjectDisposedException>(() => Prepare(painter, svg));
        Assert.Throws<ObjectDisposedException>(() => painter.DrawDocument(svg, surface.Canvas));
    }

    private static bool Prepare(SkiaGpuDocumentPainter painter, SKSvg svg) =>
        painter.Prepare(svg, 400, 260, 1, 400, 260, SKMatrix.Identity, out _);

    private static SKSurface Raster(float dpr) => SKSurface.Create(new SKImageInfo(
        (int)Math.Ceiling(400 * dpr), (int)Math.Ceiling(260 * dpr), SKColorType.Bgra8888, SKAlphaType.Premul));

    private static void Paint(SKCanvas canvas, float dpr, SKMatrix viewport, Action<SKCanvas> draw)
    {
        canvas.RestoreToCount(1);
        canvas.ResetMatrix();
        canvas.Clear(new SKColor(247, 246, 242));
        canvas.Scale(dpr);
        canvas.Concat(in viewport);
        draw(canvas);
    }
}
