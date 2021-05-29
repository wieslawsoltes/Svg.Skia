
namespace Svg.Model.Primitives.PathCommands
{
    public sealed class LineToPathCommand : PathCommand
    {
        public float X { get; }
        public float Y { get; }

        public LineToPathCommand(float x, float y)
        {
            X = x;
            Y = y;
        }
    }
}
