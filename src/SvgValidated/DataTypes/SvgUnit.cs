
namespace SvgValidated
{
    public struct SvgUnit
    {
        public float Value { get; set; }
        public SvgUnitType Type { get; set; }
    }

    public enum UnitRenderingType
    {
        Other,
        Horizontal,
        HorizontalOffset,
        Vertical,
        VerticalOffset
    }

    public enum SvgUnitType
    {
        None,
        Pixel,
        Em,
        Ex,
        Percentage,
        User,
        Inch,
        Centimeter,
        Millimeter,
        Pica,
        Point
    }
}
