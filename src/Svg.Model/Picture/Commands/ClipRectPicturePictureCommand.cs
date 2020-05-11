namespace Svg.Model
{
    public class ClipRectPicturePictureCommand : PictureCommand
    {
        public Rect? Rect { get; set; }
        public ClipOperation Operation { get; set; }
        public bool Antialias { get; set; }
    }
}
