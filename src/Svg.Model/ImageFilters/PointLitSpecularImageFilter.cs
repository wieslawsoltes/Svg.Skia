namespace Svg.Model
{
    public class PointLitSpecularImageFilter : ImageFilter
    {
        public Point3 Location;
        public Color LightColor;
        public float SurfaceScale;
        public float Ks;
        public float Shininess;
        public ImageFilter? Input;
        public CropRect? CropRect;
    }
}
