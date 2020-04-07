using System;

namespace SvgValidated
{
    public abstract class SvgMarkerElement : SvgPathBasedElement
    {
        public Uri MarkerEnd { get; set; }
        public Uri MarkerMid { get; set; }
        public Uri MarkerStart { get; set; }
    }
}
