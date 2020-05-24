namespace Svg.Picture
{
    public class AddRoundRectPathCommand : PathCommand
    {
        public Rect Rect { get; set; }
        public float Rx { get; set; }
        public float Ry { get; set; }

        public AddRoundRectPathCommand(Rect rect, float rx, float ry)
        {
            Rect = rect;
            Rx = rx;
            Ry = ry;
        }
    }
}
