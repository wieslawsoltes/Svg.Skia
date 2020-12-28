
using Svg.Model.Painting;

namespace Svg.Model.Primitives.CanvasCommands
{
    public sealed class ClipPathCanvasCommand : CanvasCommand
    {
        public ClipPath ClipPath { get; }
        public ClipOperation Operation { get; }
        public bool Antialias { get; }

        public ClipPathCanvasCommand(ClipPath clipPath, ClipOperation operation, bool antialias)
        {
            ClipPath = clipPath;
            Operation = operation;
            Antialias = antialias;
        }
    }
}
