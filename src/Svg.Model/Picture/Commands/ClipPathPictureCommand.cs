namespace Svg.Model
{
    public class ClipPathPictureCommand : PictureCommand
    {
        public Path? Path { get; set; }
        public ClipOperation Operation { get; set; }
        public bool Antialias { get; set; }
    }
}
