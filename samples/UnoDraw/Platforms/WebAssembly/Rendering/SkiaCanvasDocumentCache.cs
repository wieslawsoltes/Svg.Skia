using System;
using SkiaSharp;

namespace UnoDraw.Rendering;

/// <summary>
/// Retains the exact BGRA document pixels. Restoring them is a native pixel copy,
/// independent of the canvas matrix, clip, sampling, alpha blending or SVG replay.
/// </summary>
internal sealed class SkiaCanvasDocumentCache : IDisposable
{
    private SKBitmap? _document;

    internal bool TryRestore(SkiaCanvasUploadBuffer output)
    {
        if (_document is not { } document || document.Width != output.Width || document.Height != output.Height)
            return false;

        output.RestoreRaster(document);
        return true;
    }

    internal void Store(SkiaCanvasUploadBuffer output)
    {
        var info = new SKImageInfo(output.Width, output.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        if (_document is null || _document.Info != info)
        {
            Dispose();
            _document = new SKBitmap(info);
            if (_document.GetPixels() == IntPtr.Zero)
                throw new InvalidOperationException("Skia could not allocate the retained document pixels.");
        }

        try
        {
            output.CopyRasterTo(_document);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        _document?.Dispose();
        _document = null;
    }
}
