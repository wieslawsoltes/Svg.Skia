namespace Svg.Model
{
    public class SetMatrixPictureCommand : PictureCommand
    {
        public Matrix Matrix { get; set; }

        public SetMatrixPictureCommand(Matrix matrix)
        {
            Matrix = matrix;
        }
    }
}
