using SvgValidated.Transforms;

namespace SvgValidated
{
    public sealed class SvgPatternServer : SvgPaintServer, ISvgViewPort
    {
        public SvgUnit X { get; set; }
        public SvgUnit Y { get; set; }
        public SvgUnit Width { get; set; }
        public SvgUnit Height { get; set; }
        public SvgCoordinateUnits PatternUnits { get; set; }
        public SvgCoordinateUnits PatternContentUnits { get; set; }
        public SvgViewBox ViewBox { get; set; }
        public SvgDeferredPaintServer InheritGradient { get; set; }
        public SvgOverflow Overflow { get; set; }
        public SvgAspectRatio AspectRatio { get; set; }
        public SvgTransformCollection PatternTransform { get; set; }
    }
}
