
namespace Svg.Model.Painting.ImageFilters
{
    public sealed class BlendModeImageFilter : ImageFilter
    {
        public BlendMode Mode { get; set; }
        public ImageFilter? Background { get; set; }
        public ImageFilter? Foreground { get; set; }
        public CropRect? CropRect { get; set; }
    }
}
