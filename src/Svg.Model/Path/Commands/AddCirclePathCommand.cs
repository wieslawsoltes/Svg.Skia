namespace Svg.Model
{
    public class AddCirclePathCommand : PathCommand
    {
        public float X;
        public float Y;
        public float Radius;

        public AddCirclePathCommand(float x, float y, float radius)
        {
            X = x;
            Y = y;
            Radius = radius;
        }
    }
}
