namespace Svg.Model
{
    public class DistantLitSpecularImageFilter : ImageFilter
    {
        public Point3 Direction;
        public Color LightColor;
        public float SurfaceScale;
        public float KS;
        public float Shininess;
        public ImageFilter? Input;
        public CropRect? CropRect;
    }
}
