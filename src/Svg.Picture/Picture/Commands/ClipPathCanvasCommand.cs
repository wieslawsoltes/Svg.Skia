namespace Svg.Picture
{
    public class ClipPathCanvasCommand : CanvasCommand
    {
        public ClipPath ClipPath { get; set; }
        public ClipOperation Operation { get; set; }
        public bool Antialias { get; set; }

        public ClipPathCanvasCommand(ClipPath clipPath, ClipOperation operation, bool antialias)
        {
            ClipPath = clipPath;
            Operation = operation;
            Antialias = antialias;
        }
    }
}
