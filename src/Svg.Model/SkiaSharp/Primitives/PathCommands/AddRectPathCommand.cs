
namespace Svg.Model.Primitives.PathCommands
{
    public sealed class AddRectPathCommand : PathCommand
    {
        public SKRect Rect { get; }

        public AddRectPathCommand(SKRect rect)
        {
            Rect = rect;
        }
    }
}
