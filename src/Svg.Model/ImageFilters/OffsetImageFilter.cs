namespace Svg.Model
{
    public class OffsetImageFilter : ImageFilter
    {
        public float Dx;
        public float Dy;
        public ImageFilter? Input;
        public CropRect? CropRect;
    }
}
