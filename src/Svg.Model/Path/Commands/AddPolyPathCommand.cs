using System.Collections.Generic;
using Svg.Model.Primitives;

namespace Svg.Model.Path.Commands
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
