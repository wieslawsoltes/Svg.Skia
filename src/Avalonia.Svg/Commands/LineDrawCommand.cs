using A = Avalonia;
using AM = Avalonia.Media;

namespace Avalonia.Svg.Commands;

public sealed class LineDrawCommand(AM.IPen? pen, A.Point p1, A.Point p2) : DrawCommand
{
    public AM.IPen? Pen { get; } = pen;
    public A.Point P1 { get; } = p1;
    public A.Point P2 { get; } = p2;
}