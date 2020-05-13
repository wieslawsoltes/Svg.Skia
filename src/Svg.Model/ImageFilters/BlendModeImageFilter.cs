namespace Svg.Model
{
    public class BlendModeImageFilter : ImageFilter
    {
        public BlendMode Mode;
        public ImageFilter? Background;
        public ImageFilter? Foreground;
        public CropRect? CropRect;
    }
}
