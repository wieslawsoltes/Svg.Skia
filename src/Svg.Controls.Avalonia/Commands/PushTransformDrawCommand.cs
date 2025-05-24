using A = Avalonia;

namespace Avalonia.Svg.Commands;

public sealed class PushTransformDrawCommand : DrawCommand
{
    public A.Matrix Matrix { get; }

    public PushTransformDrawCommand(A.Matrix matrix)
    {
        Matrix = matrix;
    }
}