namespace Svg.Model
{
    public class AddRoundRectPathCommand : PathCommand
    {
        public Rect Rect { get; set; }
        public float RX { get; set; }
        public float RY { get; set; }
    }
}
