using Svg.Model.Primitives;

namespace Svg.Model.Painting.ImageFilters
{
    public sealed class ImageImageFilter : SKImageFilter
    {
        public SKImage? Image { get; set; }
        public SKRect Src { get; set; }
        public SKRect Dst { get; set; }
        public SKFilterQuality FilterQuality { get; set; }
    }
}
