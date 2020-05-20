namespace Svg.Picture
{
    public class SpotLitSpecularImageFilter : ImageFilter
    {
        public Point3 Location;
        public Point3 Target;
        public float SpecularExponent;
        public float CutoffAngle;
        public Color LightColor;
        public float SurfaceScale;
        public float Ks;
        public float Shininess;
        public ImageFilter? Input;
        public CropRect? CropRect;
    }
}
