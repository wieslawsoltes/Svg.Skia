
using Svg.Model.Paint;

namespace Svg.Model.ImageFilters
{
    public sealed class MergeImageFilter : ImageFilter
    {
        public ImageFilter[]? Filters { get; set; }
        public CropRect? CropRect { get; set; }
    }
}
