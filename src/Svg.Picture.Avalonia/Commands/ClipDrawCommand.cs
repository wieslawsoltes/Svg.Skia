using A = Avalonia;

namespace Svg.Picture.Avalonia
{
    public sealed class ClipDrawCommand : DrawCommand
    {
        public A.Rect Clip { get; }

        public ClipDrawCommand(A.Rect clip)
        {
            Clip = clip;
        }
    }
}
