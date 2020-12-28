
namespace Svg.Model
{
    public sealed class AddCirclePathCommand : PathCommand
    {
        public float X { get; }
        public float Y { get; }
        public float Radius { get; }

        public AddCirclePathCommand(float x, float y, float radius)
        {
            X = x;
            Y = y;
            Radius = radius;
        }
    }
}
