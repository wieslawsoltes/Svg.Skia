using AM = Avalonia.Media;
using AP = Avalonia.Platform;

namespace Avalonia.Svg.Commands
{
    public sealed class GeometryDrawCommand : DrawCommand
    {
        public AM.IBrush? Brush { get; }
        public AM.IPen? Pen { get; }
        public AP.IGeometryImpl? Geometry { get; }

        public GeometryDrawCommand(AM.IBrush? brush, AM.IPen? pen, AP.IGeometryImpl? geometry)
        {
            Brush = brush;
            Pen = pen;
            Geometry = geometry;
        }
    }
}
