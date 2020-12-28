
namespace Svg.Model.Painting.ImageFilters
{
    public sealed class OffsetImageFilter : ImageFilter
    {
        public float Dx { get; set; }
        public float Dy { get; set; }
        public ImageFilter? Input { get; set; }
        public CropRect? CropRect { get; set; }
    }
}
