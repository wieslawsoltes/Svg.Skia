using Svg.Model.Primitives;

namespace Svg.Model.Painting.ImageFilters
{
    public sealed class PointLitDiffuseImageFilter : SKImageFilter
    {
        public SKPoint3 Location { get; set; }
        public SKColor LightColor { get; set; }
        public float SurfaceScale { get; set; }
        public float Kd { get; set; }
        public SKImageFilter? Input { get; set; }
        public SKCropRect? CropRect { get; set; }
    }
}
