namespace Svg.Model
{
    public class OffsetImageFilter : ImageFilter
    {
        public float DX { get; set; }
        public float DY { get; set; }
        public ImageFilter? Input { get; set; }
        public CropRect? CropRect { get; set; }
    }
}
