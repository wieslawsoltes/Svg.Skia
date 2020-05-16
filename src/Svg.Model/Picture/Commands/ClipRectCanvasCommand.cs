namespace Svg.Model
{
    public class ClipRectCanvasCommand : CanvasCommand
    {
        public Rect Rect;
        public ClipOperation Operation;
        public bool Antialias;

        public ClipRectCanvasCommand(Rect rect, ClipOperation operation, bool antialias)
        {
            Rect = rect;
            Operation = operation;
            Antialias = antialias;
        }
    }
}
