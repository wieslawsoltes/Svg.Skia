
namespace Svg.Model.Primitives.PathCommands
{
    public sealed class AddOvalPathCommand : PathCommand
    {
        public SKRect Rect { get; }

        public AddOvalPathCommand(SKRect rect)
        {
            Rect = rect;
        }
    }
}
