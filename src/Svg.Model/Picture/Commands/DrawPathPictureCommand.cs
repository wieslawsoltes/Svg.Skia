namespace Svg.Model
{
    public class DrawPathPictureCommand : PictureCommand
    {
        public Path Path { get; set; }
        public Paint Paint { get; set; }

        public DrawPathPictureCommand(Path path, Paint paint)
        {
            Path = path;
            Paint = paint;
        }
    }
}
