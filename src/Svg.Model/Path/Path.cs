using System.Collections.Generic;

namespace Svg.Model
{
    public class Path
    {
        public PathFillType FillType;
        public IList<PathCommand>? Commands;
    }
}
