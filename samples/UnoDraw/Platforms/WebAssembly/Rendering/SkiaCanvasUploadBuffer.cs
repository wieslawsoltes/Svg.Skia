using System;
using SkiaSharp;

namespace UnoDraw.Rendering;

/// <summary>
/// Retains the original BGRA raster surface and lazily creates a native RGBA upload buffer.
/// This class has no UI dependency so its actual pixel path can be quality-tested.
/// </summary>
internal sealed class SkiaCanvasUploadBuffer : IDisposable
{
    private SKSurface? _surface;
    private SKPixmap? _rasterPixels;
    private SKBitmap? _upload;

    internal SkiaCanvasUploadBuffer(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
        try
        {
            // The raster color type is observable: RGBA and BGRA painting can
            // round coverage/filter intermediates differently. Keep BGRA painting
            // and perform only a lossless red/blue byte permutation afterward.
            _surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul))
                ?? throw new InvalidOperationException("Skia could not allocate the canvas pixel surface.");
            _rasterPixels = _surface.PeekPixels()
                ?? throw new InvalidOperationException("Skia could not expose the canvas pixel buffer.");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal int Width { get; }
    internal int Height { get; }
    internal SKCanvas Canvas => (_surface ?? throw new ObjectDisposedException(nameof(SkiaCanvasUploadBuffer))).Canvas;
    internal IntPtr UploadPixels => UploadBitmap.GetPixels();
    internal int UploadRowBytes => UploadBitmap.RowBytes;
    internal SKImageInfo UploadInfo => UploadBitmap.Info;
    internal ReadOnlySpan<byte> UploadSpan => UploadBitmap.GetPixelSpan();
    private SKBitmap UploadBitmap
    {
        get
        {
            if (_surface is null)
                throw new ObjectDisposedException(nameof(SkiaCanvasUploadBuffer));
            if (_upload is not null)
                return _upload;

            // The original software uploader consumes BGRA directly. Retain the
            // RGBA buffer only for transports that actually request an upload.
            var upload = new SKBitmap(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul));
            if (upload.GetPixels() == IntPtr.Zero)
            {
                upload.Dispose();
                throw new InvalidOperationException("Skia could not allocate the canvas upload buffer.");
            }
            return _upload = upload;
        }
    }

    internal void CopyRasterTo(SKBitmap destination)
    {
        var raster = PeekRaster();
        ValidateRasterCopy(destination, raster.Info);
        CopyRows(raster.GetPixelSpan(), raster.RowBytes, destination.GetPixelSpan(), destination.RowBytes);
    }

    // The destination is a borrowed view of a host-owned CPU surface. Its owner
    // must notify Skia before writing (Uno's PaintSoftware clears it first).
    // This method neither retains nor disposes the destination or its surface.
    internal void CopyRasterTo(SKPixmap destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var raster = PeekRaster();
        if (destination.Info != raster.Info || destination.GetPixels() == IntPtr.Zero)
            throw new ArgumentException("A raster copy requires matching allocated BGRA premultiplied pixels.", nameof(destination));
        CopyRows(raster.GetPixelSpan(), raster.RowBytes, destination.GetPixelSpan(), destination.RowBytes);
    }

    internal void RestoreRaster(SKBitmap source)
    {
        ValidateRasterCopy(source, new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        // Public SVG callbacks can retain canvas.Surface.Snapshot(). Notify Skia
        // before a complete native overwrite so it detaches shared snapshot
        // storage and invalidates its cached image without changing matrix/clip.
        Canvas.Discard();
        var raster = PeekRaster();
        CopyRows(source.GetPixelSpan(), source.RowBytes, raster.GetPixelSpan(), raster.RowBytes);
    }

    private SKPixmap PeekRaster()
    {
        var surface = _surface ?? throw new ObjectDisposedException(nameof(SkiaCanvasUploadBuffer));
        surface.Canvas.Flush();
        // Any draw can perform copy-on-write when a callback holds a snapshot.
        // Refresh the existing wrapper rather than retaining an old address or
        // allocating a new managed/native pixmap for every frame.
        if (!surface.PeekPixels(_rasterPixels!))
            throw new InvalidOperationException("Skia could not expose the canvas pixel buffer.");
        return _rasterPixels!;
    }

    private static void ValidateRasterCopy(SKBitmap bitmap, SKImageInfo info)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        if (bitmap.Info != info || bitmap.GetPixels() == IntPtr.Zero)
            throw new ArgumentException("A raster copy requires matching allocated BGRA premultiplied pixels.", nameof(bitmap));
    }

    private void CopyRows(ReadOnlySpan<byte> source, int sourceRowBytes, Span<byte> destination, int destinationRowBytes)
    {
        var rowBytes = checked(Width * 4);
        if (sourceRowBytes == rowBytes && destinationRowBytes == rowBytes)
        {
            source[..checked(rowBytes * Height)].CopyTo(destination);
            return;
        }
        for (var row = 0; row < Height; row++)
            source.Slice(checked(row * sourceRowBytes), rowBytes)
                .CopyTo(destination.Slice(checked(row * destinationRowBytes), rowBytes));
    }

    internal void UpdateUpload()
    {
        var source = PeekRaster();
        var upload = UploadBitmap;
        var sourcePixels = source.GetPixels();
        var uploadPixels = upload.GetPixels();
        var packedRowBytes = checked(Width * 4);
        if (source.RowBytes == packedRowBytes && upload.RowBytes == packedRowBytes)
        {
            SKSwizzle.SwapRedBlue(uploadPixels, sourcePixels, checked(Width * Height));
        }
        else
        {
            for (var row = 0; row < Height; row++)
                SKSwizzle.SwapRedBlue(IntPtr.Add(uploadPixels, checked(row * upload.RowBytes)),
                    IntPtr.Add(sourcePixels, checked(row * source.RowBytes)), Width);
        }
    }

    public void Dispose()
    {
        _upload?.Dispose();
        _upload = null;
        _rasterPixels?.Dispose();
        _rasterPixels = null;
        _surface?.Dispose();
        _surface = null;
    }
}
