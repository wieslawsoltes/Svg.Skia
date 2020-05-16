namespace Svg.Model
{
    public class SetMatrixCanvasCommand : CanvasCommand
    {
        public Matrix Matrix;

        public SetMatrixCanvasCommand(Matrix matrix)
        {
            Matrix = matrix;
        }
    }
}
