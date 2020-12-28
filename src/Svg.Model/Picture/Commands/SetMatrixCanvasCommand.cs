
namespace Svg.Model
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
