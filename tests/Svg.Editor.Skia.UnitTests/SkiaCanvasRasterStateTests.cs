using System;
using SkiaSharp;
using Svg.Skia;
using UnoDraw.Rendering;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SkiaCanvasRasterStateTests
{
    private const string Source = """
        <svg xmlns="http://www.w3.org/2000/svg" width="64" height="64">
          <rect x="3.25" y="7.5" width="54" height="48" fill="#639d91"/>
        </svg>
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DrawCallbackReleaseKeepsTheCanvasAliveThroughRestoreAndRejectsRemainingWork(bool throwFromCallback)
    {
        var state = new SkiaCanvasRasterState();
        using var svg = new SKSvg();
        svg.FromSvg(Source);
        SKCanvas? activeCanvas = null;
        var feedbackCount = 0;
        try
        {
            using (var paint = state.BeginPaint(80, 80))
            {
                activeCanvas = paint.Buffer.Canvas;
                svg.OnDraw += (_, _) =>
                {
                    state.ReleaseSurfaces();
                    state.ReleaseSurfaces();
                    state.ReleaseDocumentCache();
                    Assert.NotEqual(IntPtr.Zero, activeCanvas.Handle);
                    Assert.False(paint.IsCurrent);
                    Assert.False(paint.TryRender(0, (_, _) => feedbackCount++));
                    Assert.False(paint.TryStoreDocument());
                    Assert.False(paint.TryRestoreDocument());
                    Assert.False(paint.TryUpdateUpload());
                    if (throwFromCallback)
                        throw new InvalidOperationException("The draw callback failed after unloading.");
                };

                if (throwFromCallback)
                    Assert.Throws<InvalidOperationException>(() => paint.TryRender(svg, DrawDocument));
                else
                    Assert.False(paint.TryRender(svg, DrawDocument));

                // Both nested SKAutoCanvasRestore scopes have run. The cancelled
                // buffer is still alive until the outer paint ownership ends.
                Assert.NotEqual(IntPtr.Zero, activeCanvas.Handle);
                Assert.Equal(1, activeCanvas.SaveCount);
                Assert.Equal(0, feedbackCount);
                Assert.False(paint.TryStoreDocument());
                Assert.False(paint.TryUpdateUpload());
            }

            Assert.Equal(IntPtr.Zero, activeCanvas.Handle);
            using var reloaded = state.BeginPaint(80, 80);
            Assert.True(reloaded.IsCurrent);
            Assert.NotSame(activeCanvas, reloaded.Buffer.Canvas);
            Assert.False(reloaded.TryRestoreDocument());
            Assert.True(reloaded.TryRender(SKColors.Blue, static (color, canvas) => canvas.Clear(color)));
            Assert.True(reloaded.TryUpdateUpload());
            Assert.Equal(new byte[] { 0, 0, 255, 255 }, reloaded.Buffer.UploadSpan[..4].ToArray());
        }
        finally
        {
            state.ReleaseSurfaces();
        }
    }

    [Fact]
    public void LayerReplacementDuringDrawDiscardsTheBackdropAfterRestoreWithoutDestroyingReusablePixels()
    {
        var state = new SkiaCanvasRasterState();
        using var svg = new SKSvg();
        svg.FromSvg(Source);
        try
        {
            using (var initial = state.BeginPaint(80, 80))
            {
                Assert.True(initial.TryRender(SKColors.Red, static (color, canvas) => canvas.Clear(color)));
                Assert.True(initial.TryStoreDocument());
            }

            SKCanvas originalCanvas;
            using (var paint = state.BeginPaint(80, 80))
            {
                originalCanvas = paint.Buffer.Canvas;
                Assert.True(paint.TryRestoreDocument());
                svg.OnDraw += (_, _) =>
                {
                    // SetCompositeLayers/Configure use this same production
                    // release path when a callback replaces a lease or size.
                    state.ReleaseDocumentCache();
                    state.ReleaseDocumentCache();
                };
                Assert.False(paint.TryRender(svg, DrawDocument));
                Assert.NotEqual(IntPtr.Zero, originalCanvas.Handle);
                Assert.False(paint.TryRender(0, static (_, _) => throw new Exception("Stale feedback was accepted.")));
                Assert.False(paint.TryStoreDocument());
                Assert.False(paint.TryUpdateUpload());
            }

            using var next = state.BeginPaint(80, 80);
            Assert.Same(originalCanvas, next.Buffer.Canvas);
            Assert.False(next.TryRestoreDocument());
            Assert.True(next.TryRender(SKColors.Green, static (color, canvas) => canvas.Clear(color)));
            Assert.True(next.TryStoreDocument());
            Assert.True(next.TryUpdateUpload());
        }
        finally
        {
            state.ReleaseSurfaces();
        }
    }

    [Fact]
    public void CompletedFramesRetainExactPixelsAndOldScopesCannotReleaseANewPaint()
    {
        var state = new SkiaCanvasRasterState();
        try
        {
            var previous = state.BeginPaint(17, 13);
            Assert.True(previous.TryRender(SKColors.CornflowerBlue, static (color, canvas) => canvas.Clear(color)));
            Assert.True(previous.TryStoreDocument());
            Assert.True(previous.TryUpdateUpload());
            var expected = previous.Buffer.UploadSpan.ToArray();
            previous.Dispose();

            using var current = state.BeginPaint(17, 13);
            previous.Dispose();
            Assert.True(current.IsCurrent);
            Assert.Throws<InvalidOperationException>(() => state.BeginPaint(17, 13));
            Assert.True(current.TryRender(SKColors.Transparent, static (color, canvas) => canvas.Clear(color)));
            Assert.True(current.TryRestoreDocument());
            Assert.True(current.TryUpdateUpload());
            Assert.Equal(expected, current.Buffer.UploadSpan.ToArray());
            Assert.False(previous.TryStoreDocument());
            Assert.False(previous.TryUpdateUpload());
        }
        finally
        {
            state.ReleaseSurfaces();
        }
    }

    private static void DrawDocument(SKSvg svg, SKCanvas canvas)
    {
        canvas.Clear(SKColors.Transparent);
        using var restore = new SKAutoCanvasRestore(canvas);
        canvas.Scale(1.25f);
        canvas.ClipRect(new SKRect(0, 0, 64, 64));
        using var documentRestore = new SKAutoCanvasRestore(canvas);
        svg.Draw(canvas);
    }
}
