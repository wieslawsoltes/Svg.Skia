
namespace Svg.Model.Picture.Commands
{
    public sealed class DrawPathCanvasCommand : CanvasCommand
    {
        public Path.Path? Path { get; }
        public Paint.Paint? Paint { get; }

        public DrawPathCanvasCommand(Path.Path path, Paint.Paint paint)
        {
            Path = path;
            Paint = paint;
        }
    }
}
