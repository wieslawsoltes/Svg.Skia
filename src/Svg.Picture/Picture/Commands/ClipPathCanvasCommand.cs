namespace Svg.Picture
{
    public class ClipPathCanvasCommand : CanvasCommand
    {
        public ClipPath ClipPath;
        public ClipOperation Operation;
        public bool Antialias;

        public ClipPathCanvasCommand(ClipPath clipPath, ClipOperation operation, bool antialias)
        {
            ClipPath = clipPath;
            Operation = operation;
            Antialias = antialias;
        }
    }
}
