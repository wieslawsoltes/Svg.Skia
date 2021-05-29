namespace ShimSkiaSharp.Painting.ImageFilters
{
    public sealed class OffsetImageFilter : SKImageFilter
    {
        public float Dx { get; set; }
        public float Dy { get; set; }
        public SKImageFilter? Input { get; set; }
        public SKCropRect? CropRect { get; set; }
    }
}
