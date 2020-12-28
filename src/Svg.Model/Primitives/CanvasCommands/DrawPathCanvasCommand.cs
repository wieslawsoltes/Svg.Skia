
using Svg.Model.Painting;

namespace Svg.Model.Primitives.CanvasCommands
{
    public sealed class DrawPathCanvasCommand : CanvasCommand
    {
        public Path? Path { get; }
        public Paint? Paint { get; }

        public DrawPathCanvasCommand(Path path, Paint paint)
        {
            Path = path;
            Paint = paint;
        }
    }
}
