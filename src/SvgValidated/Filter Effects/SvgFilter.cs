using System;
using Svg.DataTypes;

namespace Svg.FilterEffects
{
    public sealed class SvgFilter : SvgElement
    {
        public SvgCoordinateUnits FilterUnits { get; set; }
        public SvgCoordinateUnits PrimitiveUnits { get; set; }
        public SvgUnit X { get; set; }
        public SvgUnit Y { get; set; }
        public SvgUnit Width { get; set; }
        public SvgUnit Height { get; set; }
        public Uri Href { get; set; }
    }
}
