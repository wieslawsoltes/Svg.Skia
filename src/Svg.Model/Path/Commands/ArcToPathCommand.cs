namespace Svg.Model
{
    public class ArcToPathCommand : PathCommand
    {
        public double RX { get; set; }
        public double RY { get; set; }
        public double XAxisRotate { get; set; }
        public PathArcSize LargeArc { get; set; }
        public PathDirection Sweep { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }
}
