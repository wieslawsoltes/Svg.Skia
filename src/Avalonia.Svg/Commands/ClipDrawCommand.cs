using A = Avalonia;

namespace Avalonia.Svg.Commands;

public sealed class ClipDrawCommand : DrawCommand
{
    public A.Rect Clip { get; }

    public ClipDrawCommand(A.Rect clip)
    {
        Clip = clip;
    }
}