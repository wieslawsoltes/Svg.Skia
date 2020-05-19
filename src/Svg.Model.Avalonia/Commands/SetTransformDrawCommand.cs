using A = Avalonia;

namespace Svg.Model.Avalonia
{
    internal class SetTransformDrawCommand : DrawCommand
    {
        public readonly A.Matrix Matrix;

        public SetTransformDrawCommand(A.Matrix matrix)
        {
            Matrix = matrix;
        }
    }
}
