namespace Svg.Picture
{
    public class ArcToPathCommand : PathCommand
    {
        public float Rx;
        public float Ry;
        public float XAxisRotate;
        public PathArcSize LargeArc;
        public PathDirection Sweep;
        public float X;
        public float Y;

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
