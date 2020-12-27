using A = Avalonia;

namespace Avalonia.Svg.Picture.Commands
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
