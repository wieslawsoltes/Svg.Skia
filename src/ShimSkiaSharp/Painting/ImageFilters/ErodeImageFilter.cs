namespace ShimSkiaSharp.Painting.ImageFilters
{
    public sealed class ErodeImageFilter : SKImageFilter
    {
        public int RadiusX { get; set; }
        public int RadiusY { get; set; }
        public SKImageFilter? Input { get; set; }
        public CropRect? CropRect { get; set; }
    }
}
