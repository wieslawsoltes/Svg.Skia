using SkiaSharp;
using Windows.Foundation;

namespace Svg.Editor.Skia.Uno;

/// <summary>
/// Allows a compositor bridge to retain a document raster and paint current
/// interaction feedback over its exact pixels in one output surface.
/// </summary>
public interface ISkiaCanvasLayerSource
{
    /// <summary>
    /// Claims the document and interaction painters on the UI thread. Only one
    /// bridge can own them. Interaction-only changes invoke the supplied callback;
    /// ordinary canvas invalidation continues through the host's full invalidation
    /// path. Dispose the lease before unloading or replacing the bridge.
    /// </summary>
    ISkiaCanvasLayers? TryAcquireLayers(Action invalidateInteraction);
}

/// <summary>
/// An exclusive, UI-thread lease over the original ordered canvas painters.
/// The host supplies their unchanged logical coordinate space, device scale and
/// clip, and must discard retained pixels after any full host invalidation.
/// </summary>
public interface ISkiaCanvasLayers : IDisposable
{
    /// <summary>
    /// Whether the last document paint still matches its model, camera, settings
    /// and logical size. Full host invalidation overrides this result.
    /// </summary>
    bool IsDocumentCurrent(Size area);

    /// <summary>Paints the document into a cleared surface and records its current state.</summary>
    void RenderDocument(SKCanvas canvas, Size area);

    /// <summary>Paints current feedback over the restored document pixels, in the original order.</summary>
    void RenderInteraction(SKCanvas canvas, Size area);
}
