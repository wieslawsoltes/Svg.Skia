namespace Svg.Model
{
    public class SaveLayerPictureCommand : PictureCommand
    {
        public Paint? Paint { get; set; }

        public SaveLayerPictureCommand()
        {
        }

        public SaveLayerPictureCommand(Paint paint)
        {
            Paint = paint;
        }
    }
}
