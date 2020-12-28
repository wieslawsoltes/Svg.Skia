using System.Collections.Generic;

namespace Svg.Model
{
    public sealed class AddPolyPathCommand : PathCommand
    {
        public IList<Point>? Points { get; }
        public bool Close { get; }

        public AddPolyPathCommand(IList<Point> points, bool close)
        {
            Points = points;
            Close = close;
        }
    }
}
