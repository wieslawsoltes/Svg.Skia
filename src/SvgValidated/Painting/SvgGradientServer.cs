using System;
using System.Collections.Generic;
using Svg.Transforms;

namespace Svg
{
    public abstract class SvgGradientServer : SvgPaintServer
    {
        public List<SvgGradientStop> Stops { get; set; }
        public SvgGradientSpreadMethod SpreadMethod { get; set; }
        public SvgCoordinateUnits GradientUnits { get; set; }
        public SvgDeferredPaintServer InheritGradient { get; set; }
        public SvgTransformCollection GradientTransform { get; set; }
        public SvgPaintServer StopColor { get; set; }
        public float StopOpacity { get; set; }
    }
}
