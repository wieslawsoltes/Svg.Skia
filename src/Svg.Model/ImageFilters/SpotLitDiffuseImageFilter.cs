namespace Svg.Model
{
    public class SpotLitDiffuseImageFilter : ImageFilter
    {
        public Point3 Location;
        public Point3 Target;
        public float SpecularExponent;
        public float CutoffAngle;
        public Color LightColor;
        public float SurfaceScale;
        public float KD;
        public ImageFilter? Input;
        public CropRect? CropRect;
    }
}
