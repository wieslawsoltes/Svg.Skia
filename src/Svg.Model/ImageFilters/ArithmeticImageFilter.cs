namespace Svg.Model
{
    public class ArithmeticImageFilter : ImageFilter
    {
        public float K1;
        public float K2;
        public float K3;
        public float K4;
        public bool EforcePMColor;
        public ImageFilter? Background;
        public ImageFilter? Foreground;
        public CropRect? CropRect;
    }
}
