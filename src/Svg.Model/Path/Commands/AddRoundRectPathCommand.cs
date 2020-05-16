namespace Svg.Model
{
    public class AddRoundRectPathCommand : PathCommand
    {
        public Rect Rect;
        public float Rx;
        public float Ry;

        public AddRoundRectPathCommand(Rect rect, float rx, float ry)
        {
            Rect = rect;
            Rx = rx;
            Ry = ry;
        }
    }
}
