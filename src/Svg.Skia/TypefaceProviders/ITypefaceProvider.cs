using SkiaSharp;

namespace Svg.Skia.TypefaceProviders
{
    public interface ITypefaceProvider
    {
        SKTypeface? FromFamilyName(string fontFamily, SKFontStyleWeight fontWeight, SKFontStyleWidth fontWidth, SKFontStyleSlant fontStyle);
    }
}
