
namespace Svg.Pathing
{
    public class SvgMoveToSegment : SvgPathSegment
    {
        public SvgMoveToSegment(PointF moveTo)
            : base(moveTo, moveTo)
        {
        }
    }
}
