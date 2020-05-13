namespace Svg.Model
{
    public class PointLitSpecularImageFilter : ImageFilter
    {
        public Point3 Location { get; set; }
        public Color LightColor { get; set; }
        public float SurfaceScale { get; set; }
        public float KS { get; set; }
        public float Shininess { get; set; }
        public ImageFilter? Input { get; set; }
        public CropRect? CropRect { get; set; }
    }
}
