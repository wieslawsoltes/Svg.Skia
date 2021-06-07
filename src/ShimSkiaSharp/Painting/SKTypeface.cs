namespace ShimSkiaSharp.Painting
{
    public class SKTypeface
    {
        public string? FamilyName { get; private set; }
        public SKFontStyleWeight FontWeight { get; private set; }
        public SKFontStyleWidth FontWidth { get; private set; }
        public SKFontStyleSlant Style { get; private set; }

        private SKTypeface()
        {
        }

        public static SKTypeface FromFamilyName(
            string familyName,
            SKFontStyleWeight weight,
            SKFontStyleWidth width,
            SKFontStyleSlant slant)
        {
            return new()
            {
                FamilyName = familyName,
                FontWeight = weight,
                FontWidth = width,
                Style = slant
            };
        }
    }
}
