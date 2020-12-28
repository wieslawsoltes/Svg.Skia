
namespace Svg.Model.Path.Commands
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
