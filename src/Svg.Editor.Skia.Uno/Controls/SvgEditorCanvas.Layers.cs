using SkiaSharp;
using Windows.Foundation;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorCanvas : ISkiaCanvasLayerSource
{
    private CanvasLayerLease? _layerLease;

    ISkiaCanvasLayers? ISkiaCanvasLayerSource.TryAcquireLayers(Action invalidateInteraction)
    {
        ArgumentNullException.ThrowIfNull(invalidateInteraction);
        if (DispatcherQueue is { HasThreadAccess: false })
            throw new InvalidOperationException("Canvas layer ownership must change on the UI thread.");
        if (_disposed || _layerLease is not null)
            return null;

        var lease = new CanvasLayerLease(this, invalidateInteraction);
        _layerLease = lease;
        // The bridge paints both layers into its own persistent output. Keeping
        // the separate child hidden avoids a second full-viewport image draw.
        _interactionSurface.Opacity = 0;
        Invalidate();
        return lease;
    }

    private void ReleaseLayerLease(CanvasLayerLease lease)
    {
        if (!ReferenceEquals(_layerLease, lease))
            return;

        _layerLease = null;
        _renderSchedule.Reset();
        _hasDocumentRenderState = false;
        _interactionSurface.Opacity = 1;
        if (!_disposed && _isLoaded)
            Invalidate();
    }

    private sealed class CanvasLayerLease(SvgEditorCanvas owner, Action invalidateInteraction) : ISkiaCanvasLayers
    {
        private SvgEditorCanvas? _owner = owner;
        private Action? _invalidateInteraction = invalidateInteraction;
        private DocumentRenderState _documentState;
        private Size _area;
        private bool _hasDocumentState;

        internal void InvalidateInteraction() => _invalidateInteraction?.Invoke();

        public bool IsDocumentCurrent(Size area) => _owner is { _disposed: false } canvas
            && _hasDocumentState && _area == area
            && _documentState == canvas.CaptureDocumentRenderState();

        public void RenderDocument(SKCanvas canvas, Size area)
        {
            var source = GetOwner();
            source.RenderDocument(canvas, area);
            // RenderDocument captures before painting, so a reentrant model
            // change cannot make old pixels appear current afterward.
            _documentState = source._documentRenderState;
            _area = area;
            _hasDocumentState = true;
        }

        public void RenderInteraction(SKCanvas canvas, Size area) => GetOwner().RenderInteraction(canvas, area);

        private SvgEditorCanvas GetOwner() => _owner is { _disposed: false } source
            ? source : throw new ObjectDisposedException(nameof(ISkiaCanvasLayers));

        public void Dispose()
        {
            var source = _owner;
            if (source?.DispatcherQueue is { HasThreadAccess: false })
                throw new InvalidOperationException("Canvas layer ownership must change on the UI thread.");
            _owner = null;
            _invalidateInteraction = null;
            _documentState = default;
            _hasDocumentState = false;
            source?.ReleaseLayerLease(this);
        }
    }
}
