using Svg.Model.Primitives;

namespace Svg.Model.Painting.ImageFilters
{
    public sealed class PictureImageFilter : ImageFilter
    {
        public Picture? Picture { get; set; }
        public Rect? CropRect { get; set; }
    }
}
