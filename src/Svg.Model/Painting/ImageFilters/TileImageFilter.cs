using Svg.Model.Primitives;

namespace Svg.Model.Painting.ImageFilters
{
    public sealed class TileImageFilter : ImageFilter
    {
        public Rect Src { get; set; }
        public Rect Dst { get; set; }
        public ImageFilter? Input { get; set; }
    }
}
