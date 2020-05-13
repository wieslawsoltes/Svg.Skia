namespace Svg.Model
{
    public class OffsetImageFilter : ImageFilter
    {
        public float DX;
        public float DY;
        public ImageFilter? Input;
        public CropRect? CropRect;
    }
}
