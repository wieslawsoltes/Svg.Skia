namespace Svg.Model
{
    public class PointLitDiffuseImageFilter : ImageFilter
    {
        public Point3 Location { get; set; }
        public Color LightColor { get; set; }
        public float SurfaceScale { get; set; }
        public float KD { get; set; }
        public ImageFilter? Input { get; set; }
        public CropRect? CropRect { get; set; }
    }
}
