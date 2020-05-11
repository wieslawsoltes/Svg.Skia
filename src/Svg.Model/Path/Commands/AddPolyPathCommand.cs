using System.Collections.Generic;

namespace Svg.Model
{
    public class AddPolyPathCommand : PathCommand
    {
        public IList<Point> Points { get; set; }
        public bool Close { get; set; }

        public AddPolyPathCommand(IList<Point> points, bool close)
        {
            Points = points;
            Close = close;
        }
    }
}
