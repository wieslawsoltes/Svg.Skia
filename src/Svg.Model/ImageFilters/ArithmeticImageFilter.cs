
using Svg.Model.Painting;

namespace Svg.Model.ImageFilters
{
    public sealed class ArithmeticImageFilter : ImageFilter
    {
        public float K1 { get; set; }
        public float K2 { get; set; }
        public float K3 { get; set; }
        public float K4 { get; set; }
        public bool EforcePMColor { get; set; }
        public ImageFilter? Background { get; set; }
        public ImageFilter? Foreground { get; set; }
        public CropRect? CropRect { get; set; }
    }
}
