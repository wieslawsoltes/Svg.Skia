using System;
using SkiaSharp;

namespace UnoDraw.Rendering;

/// <summary>
/// Retains the original BGRA raster surface and a native RGBA upload buffer.
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
            _upload = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
            if (_upload.GetPixels() == IntPtr.Zero)
                throw new InvalidOperationException("Skia could not allocate the canvas upload buffer.");
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
    private SKBitmap UploadBitmap => _upload ?? throw new ObjectDisposedException(nameof(SkiaCanvasUploadBuffer));

    internal void UpdateUpload()
    {
        Canvas.Flush();
        var source = _rasterPixels!;
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
