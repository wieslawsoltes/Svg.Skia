using ShimSkiaSharp.Primitives;

namespace ShimSkiaSharp.Painting.ImageFilters
{
    public sealed class PictureImageFilter : SKImageFilter
    {
        public SKPicture? Picture { get; set; }
        public SKRect? CropRect { get; set; }
    }
}
