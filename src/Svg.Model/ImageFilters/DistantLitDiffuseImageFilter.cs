
namespace Svg.Model
{
    public sealed class DistantLitDiffuseImageFilter : ImageFilter
    {
        public Point3 Direction { get; set; }
        public Color LightColor { get; set; }
        public float SurfaceScale { get; set; }
        public float Kd { get; set; }
        public ImageFilter? Input { get; set; }
        public CropRect? CropRect { get; set; }
    }
}
