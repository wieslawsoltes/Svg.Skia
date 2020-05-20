namespace Svg.Picture
{
    public class MoveToPathCommand : PathCommand
    {
        public float X;
        public float Y;

        public MoveToPathCommand(float x, float y)
        {
            X = x;
            Y = y;
        }
    }
}
