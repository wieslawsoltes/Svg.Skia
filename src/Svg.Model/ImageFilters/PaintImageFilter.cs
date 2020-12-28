
using Svg.Model.Paint;

namespace Svg.Model.ImageFilters
{
    public sealed class PaintImageFilter : ImageFilter
    {
        public Paint.Paint? Paint { get; set; }
        public CropRect? CropRect { get; set; }
    }
}
