namespace ShimSkiaSharp.Painting.ImageFilters
{
    public sealed class DilateImageFilter : SKImageFilter
    {
        public int RadiusX { get; set; }
        public int RadiusY { get; set; }
        public SKImageFilter? Input { get; set; }
        public CropRect? Clip { get; set; }
    }
}
