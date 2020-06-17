namespace Svg.Picture
{
    public sealed class SetMatrixCanvasCommand : CanvasCommand
    {
        public Matrix Matrix { get; set; }

        public SetMatrixCanvasCommand(Matrix matrix)
        {
            Matrix = matrix;
        }
    }
}
