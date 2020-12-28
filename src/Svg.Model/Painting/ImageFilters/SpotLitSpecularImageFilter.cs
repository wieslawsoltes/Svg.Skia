using Svg.Model.Primitives;

namespace Svg.Model.Painting.ImageFilters
{
    public sealed class SpotLitSpecularImageFilter : ImageFilter
    {
        public Point3 Location { get; set; }
        public Point3 Target { get; set; }
        public float SpecularExponent { get; set; }
        public float CutoffAngle { get; set; }
        public Color LightColor { get; set; }
        public float SurfaceScale { get; set; }
        public float Ks { get; set; }
        public float Shininess { get; set; }
        public ImageFilter? Input { get; set; }
        public CropRect? CropRect { get; set; }
    }
}
