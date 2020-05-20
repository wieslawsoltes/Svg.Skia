namespace Svg.Picture
{
    public class BlurImageFilter : ImageFilter
    {
        public float SigmaX;
        public float SigmaY;
        public ImageFilter? Input;
        public CropRect? CropRect;
    }
}
