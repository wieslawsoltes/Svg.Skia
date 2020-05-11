namespace Svg.Model
{
    public class ArcToPathCommand : PathCommand
    {
        public float RX { get; set; }
        public float RY { get; set; }
        public float XAxisRotate { get; set; }
        public PathArcSize LargeArc { get; set; }
        public PathDirection Sweep { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
    }
}
