using System.Collections.Generic;

namespace Svg.Model
{
    public class Path
    {
        public PathFillType FillType { get; set; }
        public IList<PathCommand>? Commands { get; set; }
    }
}
