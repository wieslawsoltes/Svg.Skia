namespace Svg.Picture
{
    public class LineToPathCommand : PathCommand
    {
        public float X;
        public float Y;

        public LineToPathCommand(float x, float y)
        {
            X = x;
            Y = y;
        }
    }
}
