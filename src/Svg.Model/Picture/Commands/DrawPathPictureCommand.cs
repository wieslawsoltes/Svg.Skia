namespace Svg.Model
{
    public class DrawPathPictureCommand : PictureCommand
    {
        public Path? Path { get; set; }
        public Paint? Paint { get; set; }
    }
}
