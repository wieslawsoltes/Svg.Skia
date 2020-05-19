using A = Avalonia;

namespace Svg.Model.Avalonia
{
    internal class ClipDrawCommand : DrawCommand
    {
        public readonly A.Rect Clip;

        public ClipDrawCommand(A.Rect clip)
        {
            Clip = clip;
        }
    }
}
