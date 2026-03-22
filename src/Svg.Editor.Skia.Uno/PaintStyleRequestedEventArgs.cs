using Svg.Editor.Skia.Uno.Models;

namespace Svg.Editor.Skia.Uno;

public sealed class PaintStyleRequestedEventArgs : EventArgs
{
    public PaintStyleRequestedEventArgs(ColorSwatchItem style, EditorPaintTarget target)
    {
        Style = style;
        Target = target;
    }

    public ColorSwatchItem Style { get; }

    public EditorPaintTarget Target { get; }
}
