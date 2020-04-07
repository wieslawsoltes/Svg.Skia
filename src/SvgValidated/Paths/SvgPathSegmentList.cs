using System;
using System.Collections.Generic;

namespace Svg.Pathing
{
    public class SvgPathSegmentList : List<SvgPathSegment>
    {
        public ISvgPathElement Owner { get; set; }
    }

    public interface ISvgPathElement
    {
    }
}
