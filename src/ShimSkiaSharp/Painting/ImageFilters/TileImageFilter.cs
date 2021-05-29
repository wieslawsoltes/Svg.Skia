using ShimSkiaSharp.Primitives;

namespace ShimSkiaSharp.Painting.ImageFilters
{
    public sealed class TileImageFilter : SKImageFilter
    {
        public SKRect Src { get; set; }
        public SKRect Dst { get; set; }
        public SKImageFilter? Input { get; set; }
    }
}
