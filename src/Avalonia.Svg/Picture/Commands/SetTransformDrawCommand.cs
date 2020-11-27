using A = Avalonia;

namespace Avalonia.Svg.Skia
{
    public sealed class SetTransformDrawCommand : DrawCommand
    {
        public A.Matrix Matrix { get; }

        public SetTransformDrawCommand(A.Matrix matrix)
        {
            Matrix = matrix;
        }
    }
}