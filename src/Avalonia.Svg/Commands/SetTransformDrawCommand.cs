using A = Avalonia;

namespace Avalonia.Svg.Commands;

public sealed class SetTransformDrawCommand(A.Matrix matrix) : DrawCommand
{
    public A.Matrix Matrix { get; } = matrix;
}