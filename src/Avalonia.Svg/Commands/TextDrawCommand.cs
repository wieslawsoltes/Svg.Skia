using A = Avalonia;
using AM = Avalonia.Media;

namespace Avalonia.Svg.Commands;

public sealed class TextDrawCommand(A.Point origin, AM.FormattedText? formattedText) : DrawCommand
{
    public A.Point Origin { get; } = origin;
    public AM.FormattedText? FormattedText { get; } = formattedText;
}
