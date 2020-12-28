
namespace Svg.Model.Primitives.PathCommands
{
    public sealed class AddRectPathCommand : PathCommand
    {
        public Rect Rect { get; }

        public AddRectPathCommand(Rect rect)
        {
            Rect = rect;
        }
    }
}
