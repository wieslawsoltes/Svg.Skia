namespace Svg.Model
{
    public class DisplacementMapEffectImageFilter : ImageFilter
    {
        public DisplacementMapEffectChannelSelectorType XChannelSelector { get; set; }
        public DisplacementMapEffectChannelSelectorType YChannelSelector { get; set; }
        public float Scale { get; set; }
        public ImageFilter? Displacement { get; set; }
        public ImageFilter? Input { get; set; }
        public CropRect? CropRect { get; set; }
    }
}
