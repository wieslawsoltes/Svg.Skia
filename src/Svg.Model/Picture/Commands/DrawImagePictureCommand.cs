namespace Svg.Model
{
    public class DrawImagePictureCommand : PictureCommand
    {
        public Image? Image;
        public Rect Source;
        public Rect Dest;
        public Paint? Paint;

        public DrawImagePictureCommand(Image image, Rect source, Rect dest, Paint? paint = null)
        {
            Image = image;
            Source = source;
            Dest = dest;
            Paint = paint;
        }
    }
}
