using A = Avalonia;
using AM = Avalonia.Media;

namespace Avalonia.Svg.Picture.Commands
{
    public sealed class RectangleDrawCommand : DrawCommand
    {
        public AM.IBrush? Brush { get; }
        public AM.IPen? Pen { get; }
        public A.Rect Rect { get; }
        public double RadiusX { get; }
        public double RadiusY { get; }

        public RectangleDrawCommand(AM.IBrush? brush, AM.IPen? pen, A.Rect rect, double radiusX, double radiusY)
        {
            Brush = brush;
            Pen = pen;
            Rect = rect;
            RadiusX = radiusX;
            RadiusY = radiusY;
        }
    }
}
