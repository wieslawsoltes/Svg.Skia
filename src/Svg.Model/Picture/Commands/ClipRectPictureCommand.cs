namespace Svg.Model
{
    public class ClipRectPictureCommand : PictureCommand
    {
        public Rect Rect;
        public ClipOperation Operation;
        public bool Antialias;

        public ClipRectPictureCommand(Rect rect, ClipOperation operation, bool antialias)
        {
            Rect = rect;
            Operation = operation;
            Antialias = antialias;
        }
    }
}
