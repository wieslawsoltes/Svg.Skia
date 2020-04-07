using System;

namespace SvgValidated.Pathing
{
    public class SvgArcSegment : SvgPathSegment
    {
        public float RadiusX { get; set; }
        public float RadiusY { get; set; }
        public float Angle { get; set; }
        public SvgArcSweep Sweep { get; set; }
        public SvgArcSize Size { get; set; }

        public SvgArcSegment(PointF start, float radiusX, float radiusY, float angle, SvgArcSize size, SvgArcSweep sweep, PointF end)
            : base(start, end)
        {
            RadiusX = radiusX;
            RadiusY = radiusY;
            Angle = angle;
            Sweep = sweep;
            Size = size;
        }
    }

    [Flags]
    public enum SvgArcSweep
    {
        Negative = 0,
        Positive = 1
    }

    [Flags]
    public enum SvgArcSize
    {
        Small = 0,
        Large = 1
    }
}
