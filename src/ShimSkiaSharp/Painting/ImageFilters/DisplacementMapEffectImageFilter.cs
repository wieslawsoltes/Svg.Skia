namespace ShimSkiaSharp.Painting.ImageFilters
{
    public sealed class DisplacementMapEffectImageFilter : SKImageFilter
    {
        public SKColorChannel XChannelSelector { get; set; }
        public SKColorChannel YChannelSelector { get; set; }
        public float Scale { get; set; }
        public SKImageFilter? Displacement { get; set; }
        public SKImageFilter? Input { get; set; }
        public SKCropRect? CropRect { get; set; }
    }
}
