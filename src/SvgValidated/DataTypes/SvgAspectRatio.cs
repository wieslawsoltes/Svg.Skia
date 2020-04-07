using System;
using SvgValidated.DataTypes;

namespace SvgValidated
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
