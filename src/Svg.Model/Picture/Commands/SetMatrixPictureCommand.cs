namespace Svg.Model
{
    public class SetMatrixPictureCommand : PictureCommand
    {
        public Matrix Matrix;

        public SetMatrixPictureCommand(Matrix matrix)
        {
            Matrix = matrix;
        }
    }
}
