using A = Avalonia;

namespace Svg.Picture.Avalonia
{
    public sealed class ClipDrawCommand : DrawCommand
    {
        public readonly A.Rect Clip;

        public ClipDrawCommand(A.Rect clip)
        {
            Clip = clip;
        }
    }
}
