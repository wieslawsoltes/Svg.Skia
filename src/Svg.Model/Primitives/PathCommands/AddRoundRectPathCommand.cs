
namespace Svg.Model.Primitives.PathCommands
{
    public sealed class AddRoundRectPathCommand : PathCommand
    {
        public SKRect Rect { get; }
        public float Rx { get; }
        public float Ry { get; }

        public AddRoundRectPathCommand(SKRect rect, float rx, float ry)
        {
            Rect = rect;
            Rx = rx;
            Ry = ry;
        }
    }
}
