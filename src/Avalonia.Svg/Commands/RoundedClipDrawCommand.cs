using A = Avalonia;

namespace Avalonia.Svg.Commands;

public sealed class RoundedClipDrawCommand : DrawCommand
{
    public A.RoundedRect Clip { get; }

    public RoundedClipDrawCommand(A.RoundedRect clip)
    {
        Clip = clip;
    }
}