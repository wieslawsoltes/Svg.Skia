using SvgValidated.Pathing;

namespace SvgValidated
{
    public class SvgGlyph : SvgPathBasedElement, ISvgPathElement
    {
        public SvgPathSegmentList PathData { get; set; }
        public string GlyphName { get; set; }
        public float HorizAdvX { get; set; }
        public string Unicode { get; set; }
        public float VertAdvY { get; set; }
        public float VertOriginX { get; set; }
        public float VertOriginY { get; set; }
    }
}
