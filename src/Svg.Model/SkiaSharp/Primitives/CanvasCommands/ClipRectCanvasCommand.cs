
using Svg.Model.Painting;

namespace Svg.Model.Primitives.CanvasCommands
{
    public sealed class ClipRectCanvasCommand : CanvasCommand
    {
        public SKRect Rect { get; }
        public SKClipOperation Operation { get; }
        public bool Antialias { get; }

        public ClipRectCanvasCommand(SKRect rect, SKClipOperation operation, bool antialias)
        {
            Rect = rect;
            Operation = operation;
            Antialias = antialias;
        }
    }
}
