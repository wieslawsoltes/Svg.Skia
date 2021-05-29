using Svg.Model.Primitives;

namespace Svg.Model.Painting.ImageFilters
{
    public sealed class PictureImageFilter : SKImageFilter
    {
        public SKPicture? Picture { get; set; }
        public SKRect? CropRect { get; set; }
    }
}
