namespace Svg.Model
{
    public class DistantLitSpecularImageFilter : ImageFilter
    {
        public Point3 Direction { get; set; }
        public Color LightColor { get; set; }
        public float SurfaceScale { get; set; }
        public float KS { get; set; }
        public float Shininess { get; set; }
        public ImageFilter? Input { get; set; }
        public CropRect? CropRect { get; set; }
    }
}
