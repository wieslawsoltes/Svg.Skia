
using Svg.Model.Painting;

namespace Svg.Model.Primitives.CanvasCommands
{
    public sealed class DrawImageCanvasCommand : CanvasCommand
    {
        public SKImage? Image { get; }
        public SKRect Source { get; }
        public SKRect Dest { get; }
        public SKPaint? Paint { get; }

        public DrawImageCanvasCommand(SKImage image, SKRect source, SKRect dest, SKPaint? paint = null)
        {
            Image = image;
            Source = source;
            Dest = dest;
            Paint = paint;
        }
    }
}
