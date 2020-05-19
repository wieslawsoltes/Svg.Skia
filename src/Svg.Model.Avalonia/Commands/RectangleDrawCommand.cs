using A = Avalonia;
using AM = Avalonia.Media;

namespace Svg.Model.Avalonia
{
    internal class RectangleDrawCommand : DrawCommand
    {
        public readonly AM.IBrush? Brush;
        public readonly AM.IPen? Pen;
        public readonly A.Rect Rect;
        public readonly double RadiusX;
        public readonly double RadiusY;

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
