
namespace Svg.Model
{
    public sealed class ColorFilterImageFilter : ImageFilter
    {
        public ColorFilter? ColorFilter { get; set; }
        public ImageFilter? Input { get; set; }
        public CropRect? CropRect { get; set; }
    }
}
