namespace Svg.Picture
{
    public class AddCirclePathCommand : PathCommand
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Radius { get; set; }

        public AddCirclePathCommand(float x, float y, float radius)
        {
            X = x;
            Y = y;
            Radius = radius;
        }
    }
}
