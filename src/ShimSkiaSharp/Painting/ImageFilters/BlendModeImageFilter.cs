namespace ShimSkiaSharp.Painting.ImageFilters
{
    public sealed class BlendModeImageFilter : SKImageFilter
    {
        public SKBlendMode Mode { get; set; }
        public SKImageFilter? Background { get; set; }
        public SKImageFilter? Foreground { get; set; }
        public SKCropRect? CropRect { get; set; }
    }
}
