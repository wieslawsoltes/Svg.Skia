using A = Avalonia;
using AM = Avalonia.Media;

namespace Avalonia.Svg.Commands;

public sealed class RectangleDrawCommand(AM.IBrush? brush, AM.IPen? pen, A.Rect rect, double radiusX, double radiusY)
    : DrawCommand
{
    public AM.IBrush? Brush { get; } = brush;
    public AM.IPen? Pen { get; } = pen;
    public A.Rect Rect { get; } = rect;
    public double RadiusX { get; } = radiusX;
    public double RadiusY { get; } = radiusY;
}