
namespace SvgValidated.FilterEffects
{
    public class SvgSpotLight : SvgElement
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float PointsAtX { get; set; }
        public float PointsAtY { get; set; }
        public float PointsAtZ { get; set; }
        public float SpecularExponent { get; set; }
        public float LimitingConeAngle { get; set; }
    }
}
