namespace ShimSkiaSharp.Painting.ImageFilters
{
    public sealed class MergeImageFilter : SKImageFilter
    {
        public SKImageFilter[]? Filters { get; set; }
        public CropRect? Clip { get; set; }
    }
}
