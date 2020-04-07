using System;
using SvgValidated.DataTypes;

namespace SvgValidated
{
    public class SvgMarker : SvgPathBasedElement, ISvgViewPort
    {
        public SvgUnit RefX { get; set; }
        public SvgUnit RefY { get; set; }
        public SvgOrient Orient { get; set; }
        public SvgOverflow Overflow { get; set; }
        public SvgViewBox ViewBox { get; set; }
        public SvgAspectRatio AspectRatio { get; set; }
        public SvgUnit MarkerWidth { get; set; }
        public SvgUnit MarkerHeight { get; set; }
        public SvgMarkerUnits MarkerUnits { get; set; }
    }
}
