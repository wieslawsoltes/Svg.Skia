namespace Svg.Picture
{
    public class ColorFilterImageFilter : ImageFilter
    {
        public ColorFilter? ColorFilter { get; set; }
        public ImageFilter? Input { get; set; }
        public CropRect? CropRect { get; set; }
    }
}
