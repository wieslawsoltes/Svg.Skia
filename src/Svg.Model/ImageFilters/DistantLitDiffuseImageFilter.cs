namespace Svg.Model
{
    public class DistantLitDiffuseImageFilter : ImageFilter
    {
        public Point3 Direction;
        public Color LightColor;
        public float SurfaceScale;
        public float Kd;
        public ImageFilter? Input;
        public CropRect? CropRect;
    }
}
