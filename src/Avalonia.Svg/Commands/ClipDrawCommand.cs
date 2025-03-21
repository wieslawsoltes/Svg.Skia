using A = Avalonia;

namespace Avalonia.Svg.Commands;

public sealed class ClipDrawCommand(A.Rect clip) : DrawCommand
{
    public A.Rect Clip { get; } = clip;
}