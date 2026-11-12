using SkiaSharp;

namespace UnoDraw.Rendering;

/// <summary>
/// Uses SkiaSharp's supported native-reference cache with an additional source-
/// pixel budget. This bounds retained zoom-phase images as well as entry count;
/// it does not estimate driver allocations or change image quality/sampling.
/// </summary>
internal sealed class GraphiteImageProvider : IDisposable
{
    private const long MaximumSourceBytes = 64L * 1024 * 1024;
    private const int MaximumEntries = 256;
    private readonly HashSet<(uint Id, bool Mipmapped)> _images = [];
    private SKGraphiteImageCache _cache = new();
    private long _sourceBytes;
    private bool _disposed;

    internal string? Failure { get; private set; }

    internal SKImage? FindOrCreate(SKGraphiteRecorder recorder, SKImage image, bool mipmapped)
    {
        try
        {
            var result = FindOrCreateCore(recorder, image, mipmapped);
            if (result is null)
                Failure ??= "an image could not be uploaded to Graphite";
            return result;
        }
        catch (Exception error)
        {
            // This callback is reached through native Skia. Never unwind a managed
            // exception through it, and never silently accept a skipped image.
            Failure ??= error.Message;
            return null;
        }
    }

    private SKImage? FindOrCreateCore(SKGraphiteRecorder recorder, SKImage image, bool mipmapped)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var key = (image.UniqueId, mipmapped);
        if (!_images.Contains(key))
        {
            var info = image.Info;
            var bytes = checked((long)info.Width * info.Height * Math.Max(4, info.BytesPerPixel));
            if (mipmapped)
                bytes = checked(bytes + (bytes + 2) / 3);
            if (bytes > MaximumSourceBytes)
                return image.ToTextureImage(recorder, mipmapped);
            if (_sourceBytes + bytes > MaximumSourceBytes || _images.Count == MaximumEntries)
                ResetCache();
            var result = _cache.FindOrCreate(recorder, image, mipmapped);
            if (result is not null)
            {
                _images.Add(key);
                _sourceBytes += bytes;
            }
            return result;
        }
        return _cache.FindOrCreate(recorder, image, mipmapped);
    }

    internal void InvalidateAfterDiscard()
    {
        // Skia d1717dc TextureUtils.cpp::MakeBitmapProxyView queues Dawn uploads
        // in the recorder and returns the image before those uploads execute.
        // Discarding Snap drops that work, so even an unchanged source UniqueId
        // must create a new texture/upload on the next accepted frame. Previously
        // submitted commands retain their own native image references. Keep this
        // provider instance and Failure: the recorder delegate still owns both.
        if (!_disposed)
            ResetCache();
    }

    private void ResetCache()
    {
        _cache.Dispose();
        _cache = new SKGraphiteImageCache();
        _images.Clear();
        _sourceBytes = 0;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _cache.Dispose();
        _images.Clear();
        _sourceBytes = 0;
    }
}
