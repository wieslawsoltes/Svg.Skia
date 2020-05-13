namespace Svg.Model
{
    public class ErodeImageFilter : ImageFilter
    {
        public int RadiusX;
        public int RadiusY;
        public ImageFilter? Input;
        public CropRect? CropRect;
    }
}
