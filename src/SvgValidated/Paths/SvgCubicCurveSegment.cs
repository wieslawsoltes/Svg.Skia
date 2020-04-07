
namespace Svg.Pathing
{
    public class SvgCubicCurveSegment : SvgPathSegment
    {
        public PointF FirstControlPoint { get; set; }
        public PointF SecondControlPoint { get; set; }

        public SvgCubicCurveSegment(PointF start, PointF firstControlPoint, PointF secondControlPoint, PointF end)
            : base(start, end)
        {
            FirstControlPoint = firstControlPoint;
            SecondControlPoint = secondControlPoint;
        }
    }
}
