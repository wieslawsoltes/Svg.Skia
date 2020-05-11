namespace Svg.Model
{
    public class AddRoundRectPathCommand : PathCommand
    {
        public Rect Rect { get; set; }
        public double RX { get; set; }
        public double RY { get; set; }
    }
}
