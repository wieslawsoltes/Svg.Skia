using Svg.Pathing;

namespace Svg;

internal readonly struct SceneGraphPathDataHash
{
    public SceneGraphPathDataHash(int segmentCount, int hash1, int hash2)
    {
        SegmentCount = segmentCount;
        Hash1 = hash1;
        Hash2 = hash2;
    }

    public int SegmentCount { get; }
    public int Hash1 { get; }
    public int Hash2 { get; }
}

internal static class SceneGraphPathDataHashFactory
{
    private struct Builder
    {
        private int _hash1;
        private int _hash2;

        public void Add<T>(T value)
        {
            var hash = value?.GetHashCode() ?? 0;
            _hash1 = unchecked((_hash1 * 397) ^ hash);
            _hash2 = unchecked((_hash2 * 1009) ^ (hash * 16777619));
        }

        public SceneGraphPathDataHash ToHash(int segmentCount)
        {
            return new SceneGraphPathDataHash(segmentCount, _hash1, _hash2);
        }
    }

    public static SceneGraphPathDataHash Create(SvgPathSegmentList svgPathSegmentList)
    {
        var builder = new Builder();

        for (var i = 0; i < svgPathSegmentList.Count; i++)
        {
            switch (svgPathSegmentList[i])
            {
                case SvgMoveToSegment svgMoveToSegment:
                    builder.Add(1);
                    builder.Add(svgMoveToSegment.IsRelative);
                    AddPoint(ref builder, svgMoveToSegment.End);
                    break;
                case SvgLineSegment svgLineSegment:
                    builder.Add(2);
                    builder.Add(svgLineSegment.IsRelative);
                    AddPoint(ref builder, svgLineSegment.End);
                    break;
                case SvgCubicCurveSegment svgCubicCurveSegment:
                    builder.Add(3);
                    builder.Add(svgCubicCurveSegment.IsRelative);
                    AddPoint(ref builder, svgCubicCurveSegment.FirstControlPoint);
                    AddPoint(ref builder, svgCubicCurveSegment.SecondControlPoint);
                    AddPoint(ref builder, svgCubicCurveSegment.End);
                    break;
                case SvgQuadraticCurveSegment svgQuadraticCurveSegment:
                    builder.Add(4);
                    builder.Add(svgQuadraticCurveSegment.IsRelative);
                    AddPoint(ref builder, svgQuadraticCurveSegment.ControlPoint);
                    AddPoint(ref builder, svgQuadraticCurveSegment.End);
                    break;
                case SvgArcSegment svgArcSegment:
                    builder.Add(5);
                    builder.Add(svgArcSegment.IsRelative);
                    builder.Add(svgArcSegment.RadiusX);
                    builder.Add(svgArcSegment.RadiusY);
                    builder.Add(svgArcSegment.Angle);
                    builder.Add((int)svgArcSegment.Size);
                    builder.Add((int)svgArcSegment.Sweep);
                    AddPoint(ref builder, svgArcSegment.End);
                    break;
                case SvgClosePathSegment:
                    builder.Add(6);
                    break;
            }
        }

        return builder.ToHash(svgPathSegmentList.Count);
    }

    private static void AddPoint(ref Builder builder, System.Drawing.PointF point)
    {
        builder.Add(point.X);
        builder.Add(point.Y);
    }
}
