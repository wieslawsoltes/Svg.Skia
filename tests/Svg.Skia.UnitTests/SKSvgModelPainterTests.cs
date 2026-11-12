using System;
using SkiaSharp;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SKSvgModelPainterTests
{
    private const string Artwork = """
        <svg xmlns="http://www.w3.org/2000/svg" width="100" height="60">
          <defs><linearGradient id="g"><stop stop-color="#dc6647"/>
            <stop offset="1" stop-color="#467acc"/></linearGradient></defs>
          <rect x="3.25" y="4.5" width="67" height="37" fill="url(#g)" opacity=".7"/>
          <ellipse cx="60" cy="29" rx="16.25" ry="13.5" fill="none" stroke="#183b29" stroke-width="2.5"/>
        </svg>
        """;

    private const string AnimatedArtwork = """
        <svg xmlns="http://www.w3.org/2000/svg" width="100" height="60">
          <rect width="100" height="60" fill="#faf7ed"/>
          <g><rect x="4" y="12" width="20" height="24" fill="#467acc">
            <animate attributeName="x" from="4" to="40" dur="1s" fill="freeze"/>
          </rect></g>
        </svg>
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ModelPainter_PreservesPixelsViewerTransformAndDrawNotification(bool handlesModel)
    {
        using var svg = new SKSvg();
        svg.FromSvg(Artwork);
        Assert.True(svg.SetViewerTransform(1.375, new ShimSkiaSharp.SKPoint(7.25f, 3.5f)));
        using var expected = CreateBitmap();
        using var expectedCanvas = CreateCanvas(expected);
        svg.Draw(expectedCanvas);

        using var actual = CreateBitmap();
        using var canvas = CreateCanvas(actual);
        var beforeMatrix = canvas.TotalMatrix;
        var beforeClip = canvas.DeviceClipBounds;
        var beforeSaveCount = canvas.SaveCount;
        var calls = 0;
        var notifications = 0;
        svg.OnDraw += (_, args) =>
        {
            notifications++;
            Assert.Equal(beforeMatrix, canvas.TotalMatrix);
            Assert.Equal(beforeSaveCount, canvas.SaveCount);
        };

        svg.Draw(canvas, (target, painter, model) =>
        {
            calls++;
            Assert.Same(canvas, target);
            Assert.Same(svg.SkiaModel, painter);
            Assert.Same(svg.Model, model);
            Assert.NotEqual(beforeMatrix, target.TotalMatrix);
            if (handlesModel)
                painter.Draw(model, target);
            return handlesModel;
        });

        Assert.Equal(1, calls);
        Assert.Equal(1, notifications);
        Assert.Equal(beforeMatrix, canvas.TotalMatrix);
        Assert.Equal(beforeClip, canvas.DeviceClipBounds);
        Assert.Equal(beforeSaveCount, canvas.SaveCount);
        Assert.Equal(expected.GetPixelSpan().ToArray(), actual.GetPixelSpan().ToArray());
    }

    [Fact]
    public void ModelPainter_DeclinedProbeCannotChangeTheFallbackCanvasState()
    {
        using var svg = new SKSvg();
        svg.FromSvg(Artwork);
        using var expected = CreateBitmap();
        using var expectedCanvas = CreateCanvas(expected);
        svg.Draw(expectedCanvas);
        using var actual = CreateBitmap();
        using var canvas = CreateCanvas(actual);

        svg.Draw(canvas, (target, _, _) =>
        {
            target.Save();
            target.Scale(3);
            target.ClipRect(new SKRect(2, 3, 8, 9));
            return false;
        });

        Assert.Equal(expected.GetPixelSpan().ToArray(), actual.GetPixelSpan().ToArray());
        Assert.Equal(expectedCanvas.TotalMatrix, canvas.TotalMatrix);
        Assert.Equal(expectedCanvas.DeviceClipBounds, canvas.DeviceClipBounds);
        Assert.Equal(expectedCanvas.SaveCount, canvas.SaveCount);
    }

    [Fact]
    public void ModelPainter_ExceptionRestoresCanvasAndReleasesDocumentDraw()
    {
        using var svg = new SKSvg();
        svg.FromSvg(Artwork);
        using var bitmap = CreateBitmap();
        using var canvas = CreateCanvas(bitmap);
        var beforeMatrix = canvas.TotalMatrix;
        var beforeClip = canvas.DeviceClipBounds;
        var beforeSaveCount = canvas.SaveCount;
        var notifications = 0;
        svg.OnDraw += (_, _) => notifications++;

        Assert.Throws<InvalidOperationException>(() => svg.Draw(canvas, (target, _, _) =>
        {
            target.Save();
            target.Scale(3);
            target.ClipRect(new SKRect(2, 3, 8, 9));
            throw new InvalidOperationException("The optional painter could not render.");
        }));

        Assert.Equal(beforeMatrix, canvas.TotalMatrix);
        Assert.Equal(beforeClip, canvas.DeviceClipBounds);
        Assert.Equal(beforeSaveCount, canvas.SaveCount);
        Assert.Equal(0, notifications);
        // Replacing the protected model waits for active draws. A failed custom
        // paint must release that lifetime before the next document operation.
        Assert.NotNull(svg.FromSvg(Artwork));
        svg.Draw(canvas);
        Assert.Equal(1, notifications);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ModelPainter_DoesNotReplaceWireframeOrAnimatedLayers(bool animated)
    {
        using var svg = new SKSvg();
        svg.FromSvg(animated ? AnimatedArtwork : Artwork);
        svg.Wireframe = !animated;
        if (animated)
        {
            Assert.True(svg.UsesAnimationLayerCaching);
            svg.SetAnimationTime(TimeSpan.FromMilliseconds(375));
        }

        using var expected = CreateBitmap();
        using var expectedCanvas = CreateCanvas(expected);
        svg.Draw(expectedCanvas);
        using var actual = CreateBitmap();
        using var canvas = CreateCanvas(actual);
        var notifications = 0;
        svg.OnDraw += (_, _) => notifications++;

        svg.Draw(canvas, (_, _, _) => throw new InvalidOperationException("This mode uses its original painter."));

        Assert.Equal(1, notifications);
        Assert.Equal(expected.GetPixelSpan().ToArray(), actual.GetPixelSpan().ToArray());
    }

    [Fact]
    public void ModelPainter_EmptyRendererPreservesCanvasWithoutCallingPainter()
    {
        using var svg = new SKSvg();
        using var bitmap = CreateBitmap();
        using var canvas = CreateCanvas(bitmap);
        var beforeMatrix = canvas.TotalMatrix;
        var beforeSaveCount = canvas.SaveCount;
        var notifications = 0;
        svg.OnDraw += (_, _) => notifications++;

        svg.Draw(canvas, (_, _, _) => throw new InvalidOperationException("No model is available."));

        Assert.Equal(beforeMatrix, canvas.TotalMatrix);
        Assert.Equal(beforeSaveCount, canvas.SaveCount);
        Assert.Equal(0, notifications);
    }

    private static SKBitmap CreateBitmap() => new(new SKImageInfo(240, 180, SKColorType.Bgra8888, SKAlphaType.Premul));

    private static SKCanvas CreateCanvas(SKBitmap bitmap)
    {
        var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.Translate(2.375f, 1.25f);
        canvas.Scale(1.25f);
        canvas.ClipRect(new SKRect(1, 2, 179.5f, 131.25f), antialias: true);
        return canvas;
    }
}
