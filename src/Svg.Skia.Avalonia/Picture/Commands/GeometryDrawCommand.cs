using AM = Avalonia.Media;

namespace Svg.Skia.Avalonia
{
    public sealed class GeometryDrawCommand : DrawCommand
    {
        public AM.IBrush? Brush { get; }
        public AM.IPen? Pen { get; }
        public AM.Geometry? Geometry { get; }

        public GeometryDrawCommand(AM.IBrush? brush, AM.IPen? pen, AM.Geometry? geometry)
        {
            Brush = brush;
            Pen = pen;
            Geometry = geometry;
        }
    }
}
