using A = Avalonia;
using AM = Avalonia.Media;

namespace Avalonia.Svg.Commands;

public sealed class TextDrawCommand : DrawCommand
{
    public A.Point Origin { get; }
    public AM.FormattedText? FormattedText { get; }
    public AM.IBrush? Brush { get; }

    public TextDrawCommand(A.Point origin, AM.FormattedText? formattedText, AM.IBrush? brush = null)
    {
        Origin = origin;
        FormattedText = formattedText;
        Brush = brush;
    }
}
