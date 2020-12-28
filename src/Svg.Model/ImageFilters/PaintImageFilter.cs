
using Svg.Model.Painting;

namespace Svg.Model.ImageFilters
{
    public sealed class PaintImageFilter : ImageFilter
    {
        public Paint? Paint { get; set; }
        public CropRect? CropRect { get; set; }
    }
}
