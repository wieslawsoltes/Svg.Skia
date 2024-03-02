using AM = Avalonia.Media;
using AP = Avalonia.Platform;

namespace Avalonia.Svg.Commands;

public sealed class GeometryDrawCommand(AM.IBrush? brush, AM.IPen? pen, AM.Geometry? geometry) : DrawCommand
{
    public AM.IBrush? Brush { get; } = brush;
    public AM.IPen? Pen { get; } = pen;
    public AM.Geometry? Geometry { get; } = geometry;
}
