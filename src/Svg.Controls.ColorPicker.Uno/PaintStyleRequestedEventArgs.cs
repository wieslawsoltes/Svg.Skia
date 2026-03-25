using Svg.Controls.ColorPicker.Uno.Models;

namespace Svg.Controls.ColorPicker.Uno;

public sealed class PaintStyleRequestedEventArgs : EventArgs
{
    public PaintStyleRequestedEventArgs(ColorSwatchItem style, PaintStyleTarget target)
    {
        Style = style;
        Target = target;
    }

    public ColorSwatchItem Style { get; }

    public PaintStyleTarget Target { get; }

    public ColorPickerPaintMode PaintMode => Style.PaintMode;
}
