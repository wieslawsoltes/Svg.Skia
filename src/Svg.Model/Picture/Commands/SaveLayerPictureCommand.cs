namespace Svg.Model
{
    public class SaveLayerPictureCommand : PictureCommand
    {
        public int Count;
        public Paint? Paint;

        public SaveLayerPictureCommand(int count)
        {
            Count = count;
        }

        public SaveLayerPictureCommand(int count, Paint paint)
        {
            Count = count;
            Paint = paint;
        }
    }
}
