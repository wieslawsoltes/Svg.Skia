using ShimSkiaSharp.Painting;

namespace ShimSkiaSharp.Primitives.CanvasCommands
{
    public sealed class ClipPathCanvasCommand : CanvasCommand
    {
        public ClipPath ClipPath { get; }
        public SKClipOperation Operation { get; }
        public bool Antialias { get; }

        public ClipPathCanvasCommand(ClipPath clipPath, SKClipOperation operation, bool antialias)
        {
            ClipPath = clipPath;
            Operation = operation;
            Antialias = antialias;
        }
    }
}
