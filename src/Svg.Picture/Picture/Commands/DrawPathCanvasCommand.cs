namespace Svg.Picture
{
    public class DrawPathCanvasCommand : CanvasCommand
    {
        public Path? Path;
        public Paint? Paint;

        public DrawPathCanvasCommand(Path path, Paint paint)
        {
            Path = path;
            Paint = paint;
        }
    }
}
