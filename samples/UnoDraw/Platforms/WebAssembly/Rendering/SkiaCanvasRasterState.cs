using System;
using SkiaSharp;

namespace UnoDraw.Rendering;

/// <summary>
/// Owns the CPU pixels while a synchronous document callback can unload or
/// reconfigure their host. Release requests cancel the frame immediately, but
/// keep its canvas alive until every painter's restore scope has unwound.
/// </summary>
internal sealed class SkiaCanvasRasterState
{
    private SkiaCanvasUploadBuffer? _buffer;
    private SkiaCanvasDocumentCache? _documentCache;
    private uint _generation;
    private uint _paintId;
    private bool _painting;
    private bool _releaseBuffer;
    private bool _releaseDocumentCache;

    internal Paint BeginPaint(int width, int height)
    {
        if (_painting)
            throw new InvalidOperationException("A CPU canvas paint is already active.");

        if (_buffer is null || _buffer.Width != width || _buffer.Height != height)
        {
            ReleaseSurfaces();
            _buffer = new SkiaCanvasUploadBuffer(width, height);
        }

        _painting = true;
        _paintId++;
        return new Paint(this, _buffer, _generation, _paintId);
    }

    internal void ReleaseDocumentCache()
    {
        _generation++;
        _releaseDocumentCache = true;
        CompleteRelease();
    }

    internal void ReleaseSurfaces()
    {
        _generation++;
        _releaseDocumentCache = true;
        _releaseBuffer = true;
        CompleteRelease();
    }

    private bool IsCurrent(uint generation, uint paintId)
        => _painting && _generation == generation && _paintId == paintId;

    private void EndPaint(uint paintId)
    {
        if (!_painting || _paintId != paintId)
            return;

        _painting = false;
        CompleteRelease();
    }

    private void CompleteRelease()
    {
        if (_painting)
            return;

        try
        {
            if (_releaseDocumentCache)
            {
                _releaseDocumentCache = false;
                _documentCache?.Dispose();
                _documentCache = null;
            }
        }
        finally
        {
            if (_releaseBuffer)
            {
                _releaseBuffer = false;
                _buffer?.Dispose();
                _buffer = null;
            }
        }
    }

    internal readonly struct Paint(SkiaCanvasRasterState owner, SkiaCanvasUploadBuffer buffer,
        uint generation, uint paintId) : IDisposable
    {
        internal SkiaCanvasUploadBuffer Buffer => buffer;
        internal bool IsCurrent => owner.IsCurrent(generation, paintId);

        internal bool TryRender<TState>(TState state, Action<TState, SKCanvas> render)
        {
            if (!IsCurrent)
                return false;

            render(state, buffer.Canvas);
            return IsCurrent;
        }

        internal bool TryRestoreDocument()
            => IsCurrent && owner._documentCache?.TryRestore(buffer) == true;

        internal bool TryStoreDocument()
        {
            if (!IsCurrent)
                return false;

            (owner._documentCache ??= new SkiaCanvasDocumentCache()).Store(buffer);
            return true;
        }

        internal bool TryUpdateUpload()
        {
            if (!IsCurrent)
                return false;

            buffer.UpdateUpload();
            return true;
        }

        public void Dispose() => owner.EndPaint(paintId);
    }
}
