namespace Svg.Picture
{
    public class LineToPathCommand : PathCommand
    {
        public float X { get; set; }
        public float Y { get; set; }

        public LineToPathCommand(float x, float y)
        {
            X = x;
            Y = y;
        }
    }
}
