using A = Avalonia;

namespace Svg.Picture.Avalonia
{
    public sealed class SetTransformDrawCommand : DrawCommand
    {
        public readonly A.Matrix Matrix;

        public SetTransformDrawCommand(A.Matrix matrix)
        {
            Matrix = matrix;
        }
    }
}
