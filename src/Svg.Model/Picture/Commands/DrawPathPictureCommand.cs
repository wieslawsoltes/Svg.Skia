namespace Svg.Model
{
    public class DrawPathPictureCommand : PictureCommand
    {
        public Path? Path;
        public Paint? Paint;

        public DrawPathPictureCommand(Path path, Paint paint)
        {
            Path = path;
            Paint = paint;
        }
    }
}
