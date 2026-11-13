using System;
using System.IO;
using SkiaSharp;
using Svg.Skia;
using UnoDraw.Rendering;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SkiaCanvasDocumentCacheTests
{
    private const float LogicalWidth = 213.5f;
    private const float LogicalHeight = 159.25f;

    [Theory]
    [MemberData(nameof(RgbaCanvasEquivalenceTests.RenderCases), MemberType = typeof(RgbaCanvasEquivalenceTests))]
    public void CachedDocumentWithChangingFeedbackExactlyMatchesACompleteRepaint(
        string scene, double scale, float zoom, float panX, float panY)
    {
        // These are the same eight canonical scenes exercised by the browser
        // bridge gate, including their premultiplied color calibration patches.
        using var svg = new SKSvg();
        svg.Load(Path.Combine(AppContext.BaseDirectory, "CompositeFixtures", scene + ".svg"));
        Assert.NotNull(svg.Picture);
        var width = checked((int)Math.Ceiling(LogicalWidth * scale));
        var height = checked((int)Math.Ceiling(LogicalHeight * scale));
        using var reference = new SkiaCanvasUploadBuffer(width, height);
        using var composite = new SkiaCanvasUploadBuffer(width, height);
        using var cache = new SkiaCanvasDocumentCache();
        Assert.False(cache.TryRestore(composite));

        composite.Canvas.Clear(SKColors.Transparent);
        WithPainter(composite.Canvas, scale, canvas => Document(canvas, svg, zoom, panX, panY));
        cache.Store(composite);

        // Include a pointer crossing each physical edge and a fractional interior
        // position. Feedback must disappear fully, not accumulate into the cache.
        SKPoint[] positions = [new(-0.375f, 0.25f), new(79.125f, 54.875f),
            new(LogicalWidth - 0.125f, LogicalHeight - 0.375f), new(-30, -30), new(112.625f, 31.375f)];
        for (var frame = 0; frame < positions.Length; frame++)
        {
            reference.Canvas.Clear(SKColors.Transparent);
            WithPainter(reference.Canvas, scale, canvas =>
            {
                Document(canvas, svg, zoom, panX, panY);
                Interaction(canvas, svg, positions[frame], frame);
            });
            reference.UpdateUpload();

            Assert.True(cache.TryRestore(composite));
            WithPainter(composite.Canvas, scale, canvas => Interaction(canvas, svg, positions[frame], frame));
            composite.UpdateUpload();
            Assert.True(reference.UploadSpan.SequenceEqual(composite.UploadSpan),
                $"Cached pixels differ: {scene}, DPR {scale}, zoom {zoom}, pan ({panX}, {panY}), feedback {frame}.");
        }

        // The cache also restores exactly after feedback is completely removed.
        Assert.True(cache.TryRestore(composite));
        composite.UpdateUpload();
        reference.Canvas.Clear(SKColors.Transparent);
        WithPainter(reference.Canvas, scale, canvas => Document(canvas, svg, zoom, panX, panY));
        reference.UpdateUpload();
        Assert.True(reference.UploadSpan.SequenceEqual(composite.UploadSpan));
    }

    [Fact]
    public void NativeRestoreCopiesPremultipliedBytesWithoutUsingTheCurrentMatrixOrClip()
    {
        using var output = new SkiaCanvasUploadBuffer(257, 17);
        using var source = new SKBitmap(new SKImageInfo(257, 17, SKColorType.Bgra8888, SKAlphaType.Premul));
        var bytes = source.GetPixelSpan();
        for (var offset = 0; offset < bytes.Length; offset += 4)
        {
            var alpha = (byte)((offset / 4) % 256);
            bytes[offset] = (byte)(alpha / 3);
            bytes[offset + 1] = (byte)(alpha / 2);
            bytes[offset + 2] = alpha;
            bytes[offset + 3] = alpha;
        }
        output.RestoreRaster(source);
        output.UpdateUpload();
        var expected = output.UploadSpan.ToArray();
        using var cache = new SkiaCanvasDocumentCache();
        cache.Store(output);

        output.Canvas.Clear(SKColors.Magenta);
        output.Canvas.Save();
        output.Canvas.Translate(19.375f, 7.125f);
        output.Canvas.Scale(1.25f);
        output.Canvas.ClipRect(new SKRect(0, 0, 1, 1));
        Assert.True(cache.TryRestore(output));
        output.Canvas.Restore();
        output.UpdateUpload();
        Assert.Equal(expected, output.UploadSpan.ToArray());
    }

    [Fact]
    public void RasterCopyPreservesPixelsAcrossDifferentRowStrides()
    {
        var info = new SKImageInfo(9, 7, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var source = new SKBitmap();
        using var destination = new SKBitmap();
        Assert.True(source.TryAllocPixels(info, 48));
        Assert.True(destination.TryAllocPixels(info, 64));
        source.GetPixelSpan().Fill(0xA7);
        destination.GetPixelSpan().Fill(0xB3);
        for (var y = 0; y < info.Height; y++)
            for (var x = 0; x < info.Width; x++)
            {
                var pixel = source.GetPixelSpan().Slice(y * source.RowBytes + x * 4, 4);
                pixel[0] = (byte)(x + y);
                pixel[1] = (byte)(17 + x);
                pixel[2] = (byte)(41 + y);
                pixel[3] = 255;
            }

        using var output = new SkiaCanvasUploadBuffer(info.Width, info.Height);
        output.RestoreRaster(source);
        output.CopyRasterTo(destination);
        for (var y = 0; y < info.Height; y++)
            Assert.True(source.GetPixelSpan().Slice(y * source.RowBytes, info.Width * 4)
                .SequenceEqual(destination.GetPixelSpan().Slice(y * destination.RowBytes, info.Width * 4)));

        using var wrongFormat = new SKBitmap(info.WithColorType(SKColorType.Rgba8888));
        Assert.Throws<ArgumentException>(() => output.RestoreRaster(wrongFormat));
    }

    [Fact]
    public void ResizedAndReleasedCachesCannotRestoreStalePixels()
    {
        using var first = new SkiaCanvasUploadBuffer(23, 17);
        using var second = new SkiaCanvasUploadBuffer(31, 19);
        using var cache = new SkiaCanvasDocumentCache();
        first.Canvas.Clear(SKColors.Red);
        second.Canvas.Clear(SKColors.Blue);
        cache.Store(first);
        Assert.False(cache.TryRestore(second));
        cache.Store(second);
        Assert.False(cache.TryRestore(first));
        second.Canvas.Clear(SKColors.Yellow);
        Assert.True(cache.TryRestore(second));
        second.UpdateUpload();
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, second.UploadSpan[..4].ToArray());
        cache.Dispose();
        Assert.False(cache.TryRestore(second));
    }

    private static void WithPainter(SKCanvas canvas, double scale, Action<SKCanvas> render)
    {
        canvas.Save();
        try
        {
            canvas.Scale((float)scale);
            canvas.ClipRect(new SKRect(0, 0, LogicalWidth, LogicalHeight));
            render(canvas);
        }
        finally
        {
            canvas.Restore();
        }
    }

    private static void Document(SKCanvas canvas, SKSvg svg, float zoom, float panX, float panY)
    {
        using var restore = new SKAutoCanvasRestore(canvas);
        canvas.ClipRect(new SKRect(0, 0, LogicalWidth, LogicalHeight));
        using var background = new SKPaint { Color = new SKColor(41, 43, 47), IsAntialias = true };
        canvas.DrawRect(new SKRect(0, 0, LogicalWidth, LogicalHeight), background);
        canvas.Translate(panX, panY);
        canvas.Scale(zoom);
        svg.Draw(canvas);
    }

    private static void Interaction(SKCanvas canvas, SKSvg svg, SKPoint pointer, int frame)
    {
        using var restore = new SKAutoCanvasRestore(canvas);
        canvas.ClipRect(new SKRect(0, 0, LogicalWidth, LogicalHeight));
        using var ghost = new SKPaint { Color = SKColors.White.WithAlpha(47), IsAntialias = true };
        using (var transform = new SKAutoCanvasRestore(canvas))
        {
            canvas.Translate(7.375f + frame * 2.125f, -4.625f);
            canvas.DrawPicture(svg.Picture!, ghost);
        }
        using var stroke = new SKPaint
        {
            Color = new SKColor(131, 176, 255),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };
        using var fill = new SKPaint { Color = new SKColor(131, 176, 255, 93), IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, 11);
        canvas.DrawText("Artboard", 9.375f, 13.625f, SKTextAlign.Left, font, fill);
        canvas.DrawRect(new SKRect(11.375f, 21.875f, 177.625f, 121.125f), stroke);
        canvas.DrawRect(new SKRect(8.125f, 18.625f, 15.125f, 25.625f), fill);
        canvas.DrawCircle(pointer, 10.375f, stroke);
        canvas.DrawLine(pointer.X - 4.125f, pointer.Y, pointer.X + 4.125f, pointer.Y, stroke);
    }
}
