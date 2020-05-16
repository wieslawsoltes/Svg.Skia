namespace Svg.Model
{
    public class PointLitDiffuseImageFilter : ImageFilter
    {
        public Point3 Location;
        public Color LightColor;
        public float SurfaceScale;
        public float Kd;
        public ImageFilter? Input;
        public CropRect? CropRect;
    }
}
