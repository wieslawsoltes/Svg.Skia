using System;
using Svg.DataTypes;

namespace Svg
{
    public class SvgAspectRatio
    {
        public SvgPreserveAspectRatio Align { get; set; }
        public bool Slice { get; set; }
        public bool Defer { get; set; }
    }

    public enum SvgPreserveAspectRatio
    {
        xMidYMid,
        none,
        xMinYMin,
        xMidYMin,
        xMaxYMin,
        xMinYMid,
        xMaxYMid,
        xMinYMax,
        xMidYMax,
        xMaxYMax
    }
}
