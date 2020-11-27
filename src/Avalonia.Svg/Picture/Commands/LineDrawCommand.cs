using A = Avalonia;
using AM = Avalonia.Media;

namespace Avalonia.Svg.Skia
{
    public sealed class LineDrawCommand : DrawCommand
    {
        public AM.IPen? Pen { get; }
        public A.Point P1 { get; }
        public A.Point P2 { get; }

        public LineDrawCommand(AM.IPen? pen, A.Point p1, A.Point p2)
        {
            Pen = pen;
            P1 = p1;
            P2 = p2;
        }
    }
}