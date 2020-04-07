using System;
using System.Collections.Generic;

namespace SvgValidated.Pathing
{
    public class SvgPathSegmentList : List<SvgPathSegment>
    {
        public ISvgPathElement Owner { get; set; }
    }

    public interface ISvgPathElement
    {
    }
}
