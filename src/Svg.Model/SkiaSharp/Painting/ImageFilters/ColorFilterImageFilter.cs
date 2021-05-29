
namespace Svg.Model.Painting.ImageFilters
{
    public sealed class ColorFilterImageFilter : SKImageFilter
    {
        public SKColorFilter? ColorFilter { get; set; }
        public SKImageFilter? Input { get; set; }
        public SKCropRect? CropRect { get; set; }
    }
}
