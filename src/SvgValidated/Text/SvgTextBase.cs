
namespace Svg
{
    public abstract class SvgTextBase : SvgVisualElement
    {
        public SvgUnitCollection X { get; set; }
        public SvgUnitCollection Dx { get; set; }
        public SvgUnitCollection Y { get; set; }
        public SvgUnitCollection Dy { get; set; }
        public string Rotate { get; set; }
        public SvgUnit TextLength { get; set; }
        public SvgTextLengthAdjust LengthAdjust { get; set; }
        public SvgUnit LetterSpacing { get; set; }
        public SvgUnit WordSpacing { get; set; }
    }
}
