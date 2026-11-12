using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;
using Svg.Skia;
using UnoDraw.Rendering;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SkiaCanvasLegacyCopyTests
{
    [Fact]
    public void ADisposedBgraOnlyBufferCannotCreateALateUploadAllocation()
    {
        var output = new SkiaCanvasUploadBuffer(17, 13);
        output.Canvas.Clear(SKColors.Red);
        output.Dispose();
        Assert.Throws<ObjectDisposedException>(() => output.UpdateUpload());
        Assert.Throws<ObjectDisposedException>(() => _ = output.UploadPixels);
        Assert.Throws<ObjectDisposedException>(() => _ = output.UploadRowBytes);
        Assert.Throws<ObjectDisposedException>(() => _ = output.UploadInfo);
        Assert.Throws<ObjectDisposedException>(() => _ = output.UploadSpan);
    }

    [Fact]
    public void SnapshotRetainedByPublicDrawCallbackCannotMakeCopiesOrUploadsReadOldPixels()
    {
        using var output = new SkiaCanvasUploadBuffer(17, 13);
        using var svg = new SKSvg();
        svg.FromSvg("<svg xmlns='http://www.w3.org/2000/svg' width='17' height='13'><rect width='17' height='13' fill='red'/></svg>");
        SKImage? captured = null;
        svg.OnDraw += (_, args) => captured = args.Canvas.Surface!.Snapshot();
        svg.Draw(output.Canvas);
        using var snapshot = captured!;
        Assert.NotNull(snapshot);
        var original = ImageBytes(snapshot);

        output.Canvas.Clear(SKColors.Blue);
        output.UpdateUpload();
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, output.UploadSpan[..4].ToArray());
        using var destination = new SKBitmap(new SKImageInfo(17, 13, SKColorType.Bgra8888, SKAlphaType.Premul));
        output.CopyRasterTo(destination);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, destination.GetPixelSpan()[..4].ToArray());
        Assert.Equal(original, ImageBytes(snapshot));
    }

    [Fact]
    public void RestoringCachedPixelsPreservesSnapshotsAndRefreshesTheCurrentSurfaceImage()
    {
        using var output = new SkiaCanvasUploadBuffer(17, 13);
        using var cache = new SkiaCanvasDocumentCache();
        output.Canvas.Clear(SKColors.Blue);
        cache.Store(output);
        output.Canvas.Clear(SKColors.Red);
        using var original = output.Canvas.Surface!.Snapshot();
        var originalPixels = ImageBytes(original);

        Assert.True(cache.TryRestore(output));
        using var restored = output.Canvas.Surface!.Snapshot();
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, ImageBytes(restored)[..4]);
        Assert.Equal(originalPixels, ImageBytes(original));
        output.UpdateUpload();
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, output.UploadSpan[..4].ToArray());
    }

    [Theory]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    public void CachedFramesCopyExactlyIntoTheOriginalHostAtFractionalDeviceScale(float scale)
    {
        const float logicalWidth = 213.5f;
        const float logicalHeight = 159.25f;
        var info = new SKImageInfo((int)Math.Ceiling(logicalWidth * scale), (int)Math.Ceiling(logicalHeight * scale),
            SKColorType.Bgra8888, SKAlphaType.Premul);
        using var expected = SKSurface.Create(info);
        using var target = SKSurface.Create(info);
        using var svg = new SKSvg();
        svg.Load(Path.Combine(AppContext.BaseDirectory, "CompositeFixtures", "clip.svg"));
        var snapshots = new List<(SKImage Image, byte[] Pixels)>();
        svg.OnDraw += (_, args) =>
        {
            var image = args.Canvas.Surface!.Snapshot();
            snapshots.Add((image, ImageBytes(image)));
        };
        var state = new SkiaCanvasRasterState();
        var documents = 0;
        try
        {
            for (var frame = 0; frame < 4; frame++)
            {
                PaintSoftware(expected, scale, logicalWidth, logicalHeight, canvas =>
                {
                    Document(canvas, svg, logicalWidth, logicalHeight);
                    Feedback(canvas, frame);
                });
                using var hostSnapshot = target.Snapshot();
                var previous = ImageBytes(hostSnapshot);
                PaintSoftware(target, scale, logicalWidth, logicalHeight, canvas =>
                {
                    using var paint = state.BeginPaint(info.Width, info.Height);
                    if (!paint.TryRestoreDocument())
                    {
                        documents++;
                        paint.Buffer.Canvas.Clear(SKColors.Transparent);
                        Assert.True(paint.TryRender(0, (_, owned) => Layer(owned, scale, logicalWidth, logicalHeight,
                            c => Document(c, svg, logicalWidth, logicalHeight))));
                        Assert.True(paint.TryStoreDocument());
                    }
                    Assert.True(paint.TryRender(frame, (value, owned) => Layer(owned, scale, logicalWidth, logicalHeight,
                        c => Feedback(c, value))));
                    var matrix = canvas.TotalMatrix;
                    var clip = canvas.DeviceClipBounds;
                    using var borrowed = canvas.Surface!.PeekPixels();
                    Assert.True(paint.TryCopyRasterTo(borrowed));
                    Assert.Equal(matrix, canvas.TotalMatrix);
                    Assert.Equal(clip, canvas.DeviceClipBounds);
                });
                using var expectedPixels = expected.PeekPixels();
                using var actualPixels = target.PeekPixels();
                Assert.True(expectedPixels.GetPixelSpan().SequenceEqual(actualPixels.GetPixelSpan()));
                Assert.Equal(previous, ImageBytes(hostSnapshot));
                foreach (var snapshot in snapshots)
                    Assert.Equal(snapshot.Pixels, ImageBytes(snapshot.Image));
            }
            Assert.Equal(1, documents);
            Assert.NotEqual(IntPtr.Zero, target.Canvas.Handle);
        }
        finally
        {
            state.ReleaseSurfaces();
            foreach (var snapshot in snapshots)
                snapshot.Image.Dispose();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CanceledOrThrowingDrawCannotCopyIntoOrDisposeTheBorrowedHost(bool throwFromDraw)
    {
        using var host = SKSurface.Create(new SKImageInfo(17, 13, SKColorType.Bgra8888, SKAlphaType.Premul));
        host.Canvas.Clear(SKColors.Magenta);
        using var destination = host.PeekPixels();
        var original = destination.GetPixelSpan().ToArray();
        var state = new SkiaCanvasRasterState();
        SKCanvas? owned = null;
        try
        {
            using (var paint = state.BeginPaint(17, 13))
            {
                owned = paint.Buffer.Canvas;
                void Draw(int _, SKCanvas canvas)
                {
                    using var restore = new SKAutoCanvasRestore(canvas);
                    canvas.Translate(1.25f, 2.375f);
                    state.ReleaseSurfaces();
                    canvas.Clear(SKColors.Blue);
                    if (throwFromDraw)
                        throw new InvalidOperationException("The painter unloaded and failed.");
                }
                if (throwFromDraw)
                    Assert.Throws<InvalidOperationException>(() => paint.TryRender(0, Draw));
                else
                    Assert.False(paint.TryRender(0, Draw));
                Assert.Equal(1, owned.SaveCount);
                Assert.False(paint.TryCopyRasterTo(destination));
                Assert.Equal(original, destination.GetPixelSpan().ToArray());
            }
            Assert.Equal(IntPtr.Zero, owned.Handle);
            Assert.NotEqual(IntPtr.Zero, host.Handle);
            Assert.NotEqual(IntPtr.Zero, host.Canvas.Handle);
        }
        finally
        {
            state.ReleaseSurfaces();
        }
    }

    [Fact]
    public void BorrowedCopiesRejectAColorConversionBeforeWritingAnyDestinationPixels()
    {
        using var output = new SkiaCanvasUploadBuffer(17, 13);
        output.Canvas.Clear(SKColors.Red);
        using var wrongFormat = SKSurface.Create(new SKImageInfo(17, 13, SKColorType.Rgba8888, SKAlphaType.Premul));
        wrongFormat.Canvas.Clear(SKColors.Blue);
        using var destination = wrongFormat.PeekPixels();
        var before = destination.GetPixelSpan().ToArray();
        Assert.Throws<ArgumentException>(() => output.CopyRasterTo(destination));
        Assert.Equal(before, destination.GetPixelSpan().ToArray());
    }

    private static byte[] ImageBytes(SKImage image)
    {
        using var pixels = image.PeekPixels();
        return pixels.GetPixelSpan().ToArray();
    }

    private static void PaintSoftware(SKSurface surface, float scale, float width, float height, Action<SKCanvas> draw)
    {
        surface.Canvas.Clear(SKColors.Transparent);
        Layer(surface.Canvas, scale, width, height, draw);
        surface.Canvas.Flush();
    }

    private static void Layer(SKCanvas canvas, float scale, float width, float height, Action<SKCanvas> draw)
    {
        using var restore = new SKAutoCanvasRestore(canvas);
        canvas.Scale(scale);
        canvas.ClipRect(SKRect.Create(width, height));
        draw(canvas);
    }

    private static void Document(SKCanvas canvas, SKSvg svg, float width, float height)
    {
        using var restore = new SKAutoCanvasRestore(canvas);
        using var background = new SKPaint { Color = new SKColor(41, 43, 47, 173), IsAntialias = true };
        canvas.DrawRect(SKRect.Create(width, height), background);
        canvas.ClipRect(new SKRect(3.375f, 6.125f, width - 2.25f, height - 3.625f), SKClipOperation.Intersect, true);
        canvas.Translate(-13.625f, 7.375f);
        canvas.Scale(1.375f);
        using var rounded = new SKRoundRect(new SKRect(4.125f, 2.375f, 157.625f, 111.875f), 13.375f, 9.125f);
        canvas.ClipRoundRect(rounded, SKClipOperation.Intersect, true);
        svg.Draw(canvas);
    }

    private static void Feedback(SKCanvas canvas, int frame)
    {
        if (frame == 3)
            return;
        using var paint = new SKPaint { Color = new SKColor(131, 176, 255, 93), IsAntialias = true,
            Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        canvas.DrawCircle(new SKPoint(79.125f + frame * 31.375f, 54.875f), 10.375f, paint);
        canvas.DrawRect(new SKRect(11.375f + frame, 21.875f, 177.625f, 121.125f), paint);
    }
}
