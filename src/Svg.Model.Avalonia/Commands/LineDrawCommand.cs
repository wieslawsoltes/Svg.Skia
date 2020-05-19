using A = Avalonia;
using AM = Avalonia.Media;

namespace Svg.Model.Avalonia
{
    internal class LineDrawCommand : DrawCommand
    {
        public readonly AM.IPen? Pen;
        public readonly A.Point P1;
        public readonly A.Point P2;

        public LineDrawCommand(AM.IPen? pen, A.Point p1, A.Point p2)
        {
            Pen = pen;
            P1 = p1;
            P2 = p2;
        }
    }
}
