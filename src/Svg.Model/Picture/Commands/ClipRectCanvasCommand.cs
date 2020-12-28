
using Svg.Model.Paint;
using Svg.Model.Primitives;

namespace Svg.Model.Picture.Commands
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
