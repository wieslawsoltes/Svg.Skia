
namespace Svg.Model
{
    public sealed class AddOvalPathCommand : PathCommand
    {
        public Rect Rect { get; }

        public AddOvalPathCommand(Rect rect)
        {
            Rect = rect;
        }
    }
}
