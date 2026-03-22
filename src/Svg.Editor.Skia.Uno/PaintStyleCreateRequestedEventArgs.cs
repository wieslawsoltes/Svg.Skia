using Svg.Editor.Skia.Uno.Models;
using Windows.UI;

namespace Svg.Editor.Skia.Uno;

public sealed class PaintStyleCreateRequestedEventArgs : EventArgs
{
    public PaintStyleCreateRequestedEventArgs(Color color, EditorPaintTarget target, double strokeWidth)
    {
        Color = color;
        Target = target;
        StrokeWidth = strokeWidth;
    }

    public Color Color { get; }

    public EditorPaintTarget Target { get; }

    public double StrokeWidth { get; }
}
