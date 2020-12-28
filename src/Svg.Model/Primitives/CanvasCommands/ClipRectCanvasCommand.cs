
using Svg.Model.Painting;

namespace Svg.Model.Primitives.CanvasCommands
{
    public sealed class ClipRectCanvasCommand : CanvasCommand
    {
        public Rect Rect { get; }
        public ClipOperation Operation { get; }
        public bool Antialias { get; }

        public ClipRectCanvasCommand(Rect rect, ClipOperation operation, bool antialias)
        {
            Rect = rect;
            Operation = operation;
            Antialias = antialias;
        }
    }
}
