
namespace Svg.Model.Picture.Commands
{
    public sealed class DrawPathCanvasCommand : CanvasCommand
    {
        public Path.Path? Path { get; }
        public Painting.Paint? Paint { get; }

        public DrawPathCanvasCommand(Path.Path path, Painting.Paint paint)
        {
            Path = path;
            Paint = paint;
        }
    }
}
