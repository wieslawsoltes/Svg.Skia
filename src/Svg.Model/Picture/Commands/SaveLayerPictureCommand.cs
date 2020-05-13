namespace Svg.Model
{
    public class SaveLayerPictureCommand : PictureCommand
    {
        public Paint? Paint;

        public SaveLayerPictureCommand()
        {
        }

        public SaveLayerPictureCommand(Paint paint)
        {
            Paint = paint;
        }
    }
}
