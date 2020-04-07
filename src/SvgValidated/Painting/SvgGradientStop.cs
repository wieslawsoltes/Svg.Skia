
namespace Svg
{
    public class SvgGradientStop : SvgElement
    {
        public SvgUnit Offset { get; set; }
        public SvgPaintServer StopColor { get; set; }
        public float StopOpacity { get; set; }
    }
}
