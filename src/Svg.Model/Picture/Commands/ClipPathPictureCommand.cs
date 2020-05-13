namespace Svg.Model
{
    public class ClipPathPictureCommand : PictureCommand
    {
        public Path Path;
        public ClipOperation Operation;
        public bool Antialias;

        public ClipPathPictureCommand(Path path, ClipOperation operation, bool antialias)
        {
            Path = path;
            Operation = operation;
            Antialias = antialias;
        }
    }
}
