
namespace SvgValidated.Pathing
{
    public class SvgQuadraticCurveSegment : SvgPathSegment
    {
        public PointF ControlPoint { get; set; }

        public SvgQuadraticCurveSegment(PointF start, PointF controlPoint, PointF end)
            : base(start, end)
        {
            ControlPoint = controlPoint;
        }
    }
}
