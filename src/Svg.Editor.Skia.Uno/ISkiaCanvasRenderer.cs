using SkiaSharp;
using Windows.Foundation;

namespace Svg.Editor.Skia.Uno;

/// <summary>
/// Supplies an editor surface's existing Skia painter to a host-owned compositor
/// bridge. Coordinates and clipping match <c>SKCanvasElement.RenderOverride</c>.
/// </summary>
public interface ISkiaCanvasRenderer
{
    /// <summary>Draws into a canvas configured for the surface's logical coordinate space.</summary>
    void Render(SKCanvas canvas, Size area);
}
