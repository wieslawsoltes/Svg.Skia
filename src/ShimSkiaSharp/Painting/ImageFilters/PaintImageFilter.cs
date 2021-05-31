namespace ShimSkiaSharp.Painting.ImageFilters
{
    public sealed class PaintImageFilter : SKImageFilter
    {
        public SKPaint? Paint { get; set; }
        public CropRect? Clip { get; set; }
    }
}
