namespace Svg.Picture
{
    public sealed class PaintImageFilter : ImageFilter
    {
        public Paint? Paint { get; set; }
        public CropRect? CropRect { get; set; }
    }
}
