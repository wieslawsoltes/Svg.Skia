
using ShimSkiaSharp.Painting;

namespace ShimSkiaSharp.Primitives.CanvasCommands
{
    public sealed class DrawPathCanvasCommand : CanvasCommand
    {
        public SKPath? Path { get; }
        public SKPaint? Paint { get; }

        public DrawPathCanvasCommand(SKPath path, SKPaint paint)
        {
            Path = path;
            Paint = paint;
        }
    }
}
