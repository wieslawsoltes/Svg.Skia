
namespace ShimSkiaSharp.Painting.ImageFilters
{
    public sealed class ArithmeticImageFilter : SKImageFilter
    {
        public float K1 { get; set; }
        public float K2 { get; set; }
        public float K3 { get; set; }
        public float K4 { get; set; }
        public bool EforcePMColor { get; set; }
        public SKImageFilter? Background { get; set; }
        public SKImageFilter? Foreground { get; set; }
        public SKCropRect? CropRect { get; set; }
    }
}
