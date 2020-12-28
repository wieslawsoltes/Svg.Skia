
using Svg.Model.Primitives;

namespace Svg.Model.Path.Commands
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
