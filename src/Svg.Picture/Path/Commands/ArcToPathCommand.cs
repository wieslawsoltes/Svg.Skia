namespace Svg.Picture
{
    public class ArcToPathCommand : PathCommand
    {
        public float Rx { get; set; }
        public float Ry { get; set; }
        public float XAxisRotate { get; set; }
        public PathArcSize LargeArc { get; set; }
        public PathDirection Sweep { get; set; }
        public float X { get; set; }
        public float Y { get; set; }

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
