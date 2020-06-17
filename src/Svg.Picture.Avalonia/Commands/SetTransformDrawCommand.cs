using A = Avalonia;

namespace Svg.Picture.Avalonia
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
