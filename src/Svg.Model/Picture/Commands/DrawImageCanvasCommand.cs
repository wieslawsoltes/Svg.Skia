
using Svg.Model.Primitives;

namespace Svg.Model.Picture.Commands
{
    public sealed class DrawImageCanvasCommand : CanvasCommand
    {
        public Image? Image { get; }
        public Rect Source { get; }
        public Rect Dest { get; }
        public Painting.Paint? Paint { get; }

        public DrawImageCanvasCommand(Image image, Rect source, Rect dest, Painting.Paint? paint = null)
        {
            Image = image;
            Source = source;
            Dest = dest;
            Paint = paint;
        }
    }
}
