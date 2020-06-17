
namespace Svg.Picture
{
    public sealed class DrawImageCanvasCommand : CanvasCommand
    {
        public Image? Image { get; }
        public Rect Source { get; }
        public Rect Dest { get; }
        public Paint? Paint { get; }

        public DrawImageCanvasCommand(Image image, Rect source, Rect dest, Paint? paint = null)
        {
            Image = image;
            Source = source;
            Dest = dest;
            Paint = paint;
        }
    }
}
