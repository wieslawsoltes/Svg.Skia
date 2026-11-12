using System;
using SkiaSharp;

namespace UnoDraw.Rendering;

/// <summary>
/// Replays an unchanged text picture through the original BGRA raster painter at
/// the live device matrix. Ganesh then composites those pixels one to one. This
/// keeps font hinting, gamma and coverage when Ganesh would choose distance fields.
/// </summary>
internal sealed class SkiaTextRasterDrawable : SKDrawable
{
    private readonly SKPicture _picture;
    private readonly SKRect _recordingBounds;
    private readonly SkiaTextRasterFrame _frame;
    private SKSurface? _surface;
    private SKImage? _image;
    private SKMatrix _rasterMatrix;
    private int _width;
    private int _height;
    private long _bytes;

    internal SkiaTextRasterDrawable(SKPicture picture, SKRect recordingBounds, SkiaTextRasterFrame frame)
    {
        _picture = picture;
        _recordingBounds = recordingBounds;
        _frame = frame;
    }

    internal int RasterizationCount { get; private set; }

    // The broad original model bounds prevent early drawable rejection from
    // clipping a hinted glyph. The R-tree's tight picture bounds size the tile.
    protected override SKRect OnGetBounds() => _recordingBounds;

    protected override void OnDraw(SKCanvas canvas)
    {
        if (_frame.Failure is not null)
            return;
        try
        {
            DrawCore(canvas);
        }
        catch (Exception error)
        {
            // Never unwind a managed exception through Skia's native callback.
            // The owner checks this after the native drawable returns and requests
            // the complete original CPU frame before any browser texture copy.
            _frame.Failure = error;
        }
    }

    private void DrawCore(SKCanvas canvas)
    {
        var matrix = canvas.TotalMatrix;
        if (!IsFiniteAffine(matrix))
            throw new SkiaDocumentRasterizationException("text has a non-finite or perspective device transform");

        var bounds = matrix.MapRect(_picture.CullRect);
        if (!float.IsFinite(bounds.Left) || !float.IsFinite(bounds.Top) ||
            !float.IsFinite(bounds.Right) || !float.IsFinite(bounds.Bottom))
            throw new SkiaDocumentRasterizationException("text has unbounded device-space paint bounds");
        if (bounds.IsEmpty)
            return;

        var clip = canvas.DeviceClipBounds;
        // R-tree recording already includes the original paint/filter outsets.
        // Add two physical pixels for hinted glyph and antialias edge coverage,
        // then intersect before narrowing to integers or allocating a buffer.
        var left = Math.Max(clip.Left, Math.Floor(bounds.Left) - 2);
        var top = Math.Max(clip.Top, Math.Floor(bounds.Top) - 2);
        var right = Math.Min(clip.Right, Math.Ceiling(bounds.Right) + 2);
        var bottom = Math.Min(clip.Bottom, Math.Ceiling(bounds.Bottom) + 2);
        if (right <= left || bottom <= top)
            return;
        var x = checked((int)left);
        var y = checked((int)top);
        var width = checked((int)(right - left));
        var height = checked((int)(bottom - top));

        // Integer device translation cancels out here. Reuse the immutable image
        // when the glyph phase, scale, shader coordinates and clipped size match.
        var rasterMatrix = matrix;
        rasterMatrix.TransX -= x;
        rasterMatrix.TransY -= y;
        if (_image is null || _width != width || _height != height || _rasterMatrix != rasterMatrix)
        {
            if (_surface is null || _width != width || _height != height)
            {
                ReleasePixels();
                var bytes = checked((long)width * height * 4);
                _frame.Reserve(bytes);
                _bytes = bytes;
                _surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul))
                    ?? throw new SkiaDocumentRasterizationException("Skia could not allocate the full-resolution text tile");
                _width = width;
                _height = height;
            }

            _image?.Dispose();
            _image = null;
            var raster = _surface.Canvas;
            raster.Clear(SKColors.Transparent);
            var saveCount = raster.Save();
            try
            {
                raster.SetMatrix(in rasterMatrix);
                raster.DrawPicture(_picture);
            }
            finally
            {
                raster.RestoreToCount(saveCount);
            }
            _image = _surface.Snapshot();
            _rasterMatrix = rasterMatrix;
            RasterizationCount++;
        }

        var count = canvas.Save();
        try
        {
            canvas.ResetMatrix();
            canvas.DrawImage(_image, x, y, new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
        }
        finally
        {
            canvas.RestoreToCount(count);
        }
    }

    internal static bool IsFiniteAffine(SKMatrix matrix) =>
        float.IsFinite(matrix.ScaleX) && float.IsFinite(matrix.SkewX) && float.IsFinite(matrix.TransX) &&
        float.IsFinite(matrix.SkewY) && float.IsFinite(matrix.ScaleY) && float.IsFinite(matrix.TransY) &&
        matrix.Persp0 == 0 && matrix.Persp1 == 0 && matrix.Persp2 == 1;

    private void ReleasePixels()
    {
        _image?.Dispose();
        _image = null;
        _surface?.Dispose();
        _surface = null;
        _frame.Release(_bytes);
        _bytes = 0;
        _width = _height = 0;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ReleasePixels();
            _picture.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal sealed class SkiaTextRasterFrame
{
    private const long MaximumRetainedBytes = 64L * 1024 * 1024;
    internal Exception? Failure { get; set; }
    internal long RetainedBytes { get; private set; }

    internal void Reserve(long bytes)
    {
        if (bytes > MaximumRetainedBytes - RetainedBytes)
            throw new SkiaDocumentRasterizationException("the document exceeds the retained text pixel budget");
        RetainedBytes += bytes;
    }

    internal void Release(long bytes) => RetainedBytes -= bytes;
}

internal sealed class SkiaDocumentRasterizationException : NotSupportedException
{
    internal SkiaDocumentRasterizationException(string reason) : base(reason) { }
    internal SkiaDocumentRasterizationException(string reason, Exception inner) : base(reason, inner) { }
}
