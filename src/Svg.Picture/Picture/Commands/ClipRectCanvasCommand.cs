namespace Svg.Picture
{
    public class ClipRectCanvasCommand : CanvasCommand
    {
        public Rect Rect { get; set; }
        public ClipOperation Operation { get; set; }
        public bool Antialias { get; set; }

        public ClipRectCanvasCommand(Rect rect, ClipOperation operation, bool antialias)
        {
            Rect = rect;
            Operation = operation;
            Antialias = antialias;
        }
    }
}
