
using Svg.Model.Paint;

namespace Svg.Model.ImageFilters
{
    public sealed class DilateImageFilter : ImageFilter
    {
        public int RadiusX { get; set; }
        public int RadiusY { get; set; }
        public ImageFilter? Input { get; set; }
        public CropRect? CropRect { get; set; }
    }
}
