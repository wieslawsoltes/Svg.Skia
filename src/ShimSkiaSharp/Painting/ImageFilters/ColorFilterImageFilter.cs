namespace ShimSkiaSharp.Painting.ImageFilters
{
    public sealed class ColorFilterImageFilter : SKImageFilter
    {
        public SKColorFilter? ColorFilter { get; set; }
        public SKImageFilter? Input { get; set; }
        public CropRect? CropRect { get; set; }
    }
}
