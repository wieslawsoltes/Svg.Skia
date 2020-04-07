using System;

namespace Svg
{
    public class SvgRectangle : SvgPathBasedElement
    {
        public SvgUnit X { get; set; }
        public SvgUnit Y { get; set; }
        public SvgUnit Width { get; set; }
        public SvgUnit Height { get; set; }
        public SvgUnit CornerRadiusX { get; set; }
        public SvgUnit CornerRadiusY { get; set; }
    }
}
