namespace Svg.Model
{
    public class DisplacementMapEffectImageFilter : ImageFilter
    {
        public DisplacementMapEffectChannelSelectorType XChannelSelector;
        public DisplacementMapEffectChannelSelectorType YChannelSelector;
        public float Scale;
        public ImageFilter? Displacement;
        public ImageFilter? Input;
        public CropRect? CropRect;
    }
}
