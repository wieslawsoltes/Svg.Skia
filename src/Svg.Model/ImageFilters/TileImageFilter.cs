
using Svg.Model.Painting;
using Svg.Model.Primitives;

namespace Svg.Model.ImageFilters
{
    public sealed class TileImageFilter : ImageFilter
    {
        public Rect Src { get; set; }
        public Rect Dst { get; set; }
        public ImageFilter? Input { get; set; }
    }
}
