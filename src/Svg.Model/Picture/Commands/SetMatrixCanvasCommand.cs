
using Svg.Model.Primitives;

namespace Svg.Model.Picture.Commands
{
    public sealed class SetMatrixCanvasCommand : CanvasCommand
    {
        public Matrix Matrix { get; }

        public SetMatrixCanvasCommand(Matrix matrix)
        {
            Matrix = matrix;
        }
    }
}
