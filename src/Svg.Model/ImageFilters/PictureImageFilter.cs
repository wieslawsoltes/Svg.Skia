
using Svg.Model.Painting;
using Svg.Model.Primitives;

namespace Svg.Model.ImageFilters
{
    public sealed class PictureImageFilter : ImageFilter
    {
        public Picture? Picture { get; set; }
        public Rect? CropRect { get; set; }
    }
}
