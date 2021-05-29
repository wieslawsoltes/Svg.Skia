
using ShimSkiaSharp.Painting;

namespace ShimSkiaSharp.Primitives.CanvasCommands
{
    public sealed class SaveLayerCanvasCommand : CanvasCommand
    {
        public int Count { get; }
        public SKPaint? Paint { get; }

        public SaveLayerCanvasCommand(int count)
        {
            Count = count;
        }

        public SaveLayerCanvasCommand(int count, SKPaint paint)
        {
            Count = count;
            Paint = paint;
        }
    }
}
