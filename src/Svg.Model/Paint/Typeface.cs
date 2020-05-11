namespace Svg.Model
{
    public class Typeface
    {
        public string? FamilyName { get; }
        public FontStyle? FontStyle { get; }
        public int FontWidth { get; }
        public bool IsBold { get; }
        public bool IsItalic { get; }
        public FontStyleSlant FontSlant { get; }
    }
}
