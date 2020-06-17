namespace Svg.Picture
{
    public sealed class ArcToPathCommand : PathCommand
    {
        public float Rx { get; }
        public float Ry { get; }
        public float XAxisRotate { get; }
        public PathArcSize LargeArc { get; }
        public PathDirection Sweep { get; }
        public float X { get; }
        public float Y { get; }

        public ArcToPathCommand(float rx, float ry, float xAxisRotate, PathArcSize largeArc, PathDirection sweep, float x, float y)
        {
            Rx = rx;
            Ry = ry;
            XAxisRotate = xAxisRotate;
            LargeArc = largeArc;
            Sweep = sweep;
            X = x;
            Y = y;
        }
    }
}
