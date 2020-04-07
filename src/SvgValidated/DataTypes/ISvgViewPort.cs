using System;

namespace Svg
{
    public interface ISvgViewPort
    {
        SvgViewBox ViewBox { get; set; }
        SvgAspectRatio AspectRatio { get; set; }
        SvgOverflow Overflow { get; set; }
    }
}
