namespace Svg.Picture
{
    public class ColorFilterImageFilter : ImageFilter
    {
        public ColorFilter? ColorFilter;
        public ImageFilter? Input;
        public CropRect? CropRect;
    }
}
