namespace Svg.Picture
{
    public class MoveToPathCommand : PathCommand
    {
        public float X { get; set; }
        public float Y { get; set; }

        public MoveToPathCommand(float x, float y)
        {
            X = x;
            Y = y;
        }
    }
}
