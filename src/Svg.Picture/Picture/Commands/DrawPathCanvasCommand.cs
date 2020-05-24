namespace Svg.Picture
{
    public class DrawPathCanvasCommand : CanvasCommand
    {
        public Path? Path { get; set; }
        public Paint? Paint { get; set; }

        public DrawPathCanvasCommand(Path path, Paint paint)
        {
            Path = path;
            Paint = paint;
        }
    }
}
