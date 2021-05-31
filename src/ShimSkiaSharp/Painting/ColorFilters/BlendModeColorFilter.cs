namespace ShimSkiaSharp.Painting.ColorFilters
{
    public sealed class BlendModeColorFilter : SKColorFilter
    {
        public SKColor Color { get; set; }
        public SKBlendMode Mode { get; set; }
    }
}
