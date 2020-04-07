
namespace Svg
{
    public abstract class SvgKern : SvgElement
    {
        public string Glyph1 { get; set; }
        public string Glyph2 { get; set; }
        public string Unicode1 { get; set; }
        public string Unicode2 { get; set; }
        public float Kerning { get; set; }
    }

    public class SvgVerticalKern : SvgKern
    {
    }

    public class SvgHorizontalKern : SvgKern
    {
    }
}
