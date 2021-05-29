
namespace Svg.Model.Painting.ImageFilters
{
    public sealed class DisplacementMapEffectImageFilter : SKImageFilter
    {
        public ColorChannel XChannelSelector { get; set; }
        public ColorChannel YChannelSelector { get; set; }
        public float Scale { get; set; }
        public SKImageFilter? Displacement { get; set; }
        public SKImageFilter? Input { get; set; }
        public SKCropRect? CropRect { get; set; }
    }
}
