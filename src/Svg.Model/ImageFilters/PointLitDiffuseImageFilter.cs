namespace Svg.Model
{
    public class PointLitDiffuseImageFilter : ImageFilter
    {
        public Point3 Location;
        public Color LightColor;
        public float SurfaceScale;
        public float KD;
        public ImageFilter? Input;
        public CropRect? CropRect;
    }
}
