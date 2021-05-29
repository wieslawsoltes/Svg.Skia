using ShimSkiaSharp.Primitives;

namespace ShimSkiaSharp.Painting.ImageFilters
{
    public sealed class SpotLitSpecularImageFilter : SKImageFilter
    {
        public SKPoint3 Location { get; set; }
        public SKPoint3 Target { get; set; }
        public float SpecularExponent { get; set; }
        public float CutoffAngle { get; set; }
        public SKColor LightColor { get; set; }
        public float SurfaceScale { get; set; }
        public float Ks { get; set; }
        public float Shininess { get; set; }
        public SKImageFilter? Input { get; set; }
        public SKCropRect? CropRect { get; set; }
    }
}
