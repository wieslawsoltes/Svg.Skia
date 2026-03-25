using Svg.Controls.ColorPicker.Uno.Models;
using Windows.UI;

namespace Svg.Controls.ColorPicker.Uno;

public sealed class PaintStyleCreateRequestedEventArgs : EventArgs
{
    public PaintStyleCreateRequestedEventArgs(
        Color color,
        PaintStyleTarget target,
        double strokeWidth,
        ColorPickerPaintMode paintMode = ColorPickerPaintMode.Solid)
    {
        Color = color;
        Target = target;
        StrokeWidth = strokeWidth;
        PaintMode = paintMode;
    }

    public Color Color { get; }

    public PaintStyleTarget Target { get; }

    public double StrokeWidth { get; }

    public ColorPickerPaintMode PaintMode { get; }
}
