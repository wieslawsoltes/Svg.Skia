
using Svg.Model.Painting;

namespace Svg.Model.ImageFilters
{
    public sealed class PaintImageFilter : ImageFilter
    {
        public Painting.Paint? Paint { get; set; }
        public CropRect? CropRect { get; set; }
    }
}
