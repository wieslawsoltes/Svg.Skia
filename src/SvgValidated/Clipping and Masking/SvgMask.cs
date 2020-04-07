
namespace SvgValidated
{
    public class SvgMask : SvgElement
    {
        public SvgCoordinateUnits MaskUnits { get; set; }
        public SvgCoordinateUnits MaskContentUnits { get; set; }
        public SvgUnit X { get; set; }
        public SvgUnit Y { get; set; }
        public SvgUnit Width { get; set; }
        public SvgUnit Height { get; set; }
    }
}
