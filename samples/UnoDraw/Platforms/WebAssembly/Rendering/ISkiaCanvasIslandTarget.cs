using Microsoft.UI.Xaml;
using Svg.Editor.Skia.Uno;

namespace UnoDraw.Rendering;

/// <summary>Transport operations shared by the public canvas island host.</summary>
internal interface ISkiaCanvasIslandTarget
{
    FrameworkElement Element { get; }
    ISkiaCanvasRenderer? Renderer { get; set; }
    bool Configure(CanvasPixelSize pixels);
    void SetCompositeLayers(ISkiaCanvasLayers? layers);
    void InvalidateDocument();
    void InvalidateInteraction();
    void ReleaseSurfaces();
}
