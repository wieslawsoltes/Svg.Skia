using System.Collections.Generic;

namespace ShimSkiaSharp.Primitives.PathCommands
{
    public sealed class AddPolyPathCommand : PathCommand
    {
        public IList<SKPoint>? Points { get; }
        public bool Close { get; }

        public AddPolyPathCommand(IList<SKPoint> points, bool close)
        {
            Points = points;
            Close = close;
        }
    }
}
