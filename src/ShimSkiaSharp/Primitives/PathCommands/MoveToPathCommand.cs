
namespace ShimSkiaSharp.Primitives.PathCommands
{
    public sealed class MoveToPathCommand : PathCommand
    {
        public float X { get; }
        public float Y { get; }

        public MoveToPathCommand(float x, float y)
        {
            X = x;
            Y = y;
        }
    }
}
