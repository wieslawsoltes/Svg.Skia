namespace Svg.Model
{
    public class DilateImageFilter : ImageFilter
    {
        public int RadiusX;
        public int RadiusY;
        public ImageFilter? Input;
        public CropRect? CropRect;
    }
}
