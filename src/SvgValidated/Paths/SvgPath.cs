using SvgValidated.Pathing;

namespace SvgValidated
{
    public class SvgPath : SvgMarkerElement, ISvgPathElement
    {
        public SvgPathSegmentList PathData { get; set; }
        public float PathLength { get; set; }
    }
}
