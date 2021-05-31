namespace ShimSkiaSharp.Primitives.PathCommands
{
    public sealed class ArcToPathCommand : PathCommand
    {
        public float Rx { get; }
        public float Ry { get; }
        public float XAxisRotate { get; }
        public SKPathArcSize LargeArc { get; }
        public SKPathDirection Sweep { get; }
        public float X { get; }
        public float Y { get; }

        public ArcToPathCommand(float rx, float ry, float xAxisRotate, SKPathArcSize largeArc, SKPathDirection sweep, float x, float y)
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
