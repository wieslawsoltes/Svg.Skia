namespace Svg.Editor.Skia;

/// <summary>
/// Canvas interaction modes that operate on the existing SVG document and tools.
/// Kept separate from SvgEditorToolKind so persisted tool values retain their meaning.
/// </summary>
public enum SvgEditorInteractionMode
{
    None,
    Rotate,
    Scale,
    Reflect,
    FreeTransform,
    Gradient,
    Eyedropper,
    Zoom,
    Artboard,
    Paintbrush,
    RoundedRectangle,
    RegularPolygon,
    Star,
    RectangularGrid,
    PolarGrid,
    Spiral,
    AreaType
}
