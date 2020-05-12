namespace Svg.Model
{
    public class Typeface
    {
        public string? FamilyName { get; }
        public FontStyleWeight Weight { get; set; }
        public FontStyleWidth Width { get; set; }
        public FontStyleSlant Style { get; set; }
    }
}
