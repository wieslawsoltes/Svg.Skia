namespace Svg.Model
{
    public class ClipPathCanvasCommand : CanvasCommand
    {
        public Path Path;
        public ClipOperation Operation;
        public bool Antialias;

        public ClipPathCanvasCommand(Path path, ClipOperation operation, bool antialias)
        {
            Path = path;
            Operation = operation;
            Antialias = antialias;
        }
    }
}
