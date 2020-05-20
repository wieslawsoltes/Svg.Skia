namespace Svg.Picture
{
    public class DrawImageCanvasCommand : CanvasCommand
    {
        public Image? Image;
        public Rect Source;
        public Rect Dest;
        public Paint? Paint;

        public DrawImageCanvasCommand(Image image, Rect source, Rect dest, Paint? paint = null)
        {
            Image = image;
            Source = source;
            Dest = dest;
            Paint = paint;
        }
    }
}
