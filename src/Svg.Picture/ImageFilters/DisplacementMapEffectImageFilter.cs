
namespace Svg.Picture
{
    public sealed class DisplacementMapEffectImageFilter : ImageFilter
    {
        public ColorChannel XChannelSelector { get; set; }
        public ColorChannel YChannelSelector { get; set; }
        public float Scale { get; set; }
        public ImageFilter? Displacement { get; set; }
        public ImageFilter? Input { get; set; }
        public CropRect? CropRect { get; set; }
    }
}
