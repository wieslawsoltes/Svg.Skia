namespace Svg.Picture
{
    public class DrawImageCanvasCommand : CanvasCommand
    {
        public Image? Image { get; set; }
        public Rect Source { get; set; }
        public Rect Dest { get; set; }
        public Paint? Paint { get; set; }

        public DrawImageCanvasCommand(Image image, Rect source, Rect dest, Paint? paint = null)
        {
            Image = image;
            Source = source;
            Dest = dest;
            Paint = paint;
        }
    }
}
