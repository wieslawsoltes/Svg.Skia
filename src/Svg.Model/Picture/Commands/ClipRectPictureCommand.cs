namespace Svg.Model
{
    public class ClipRectPictureCommand : PictureCommand
    {
        public Rect Rect { get; set; }
        public ClipOperation Operation { get; set; }
        public bool Antialias { get; set; }

        public ClipRectPictureCommand(Rect rect, ClipOperation operation, bool antialias)
        {
            Rect = rect;
            Operation = operation;
            Antialias = antialias;
        }
    }
}
