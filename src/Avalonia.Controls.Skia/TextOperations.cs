
using SkiaSharp;

namespace Avalonia.Controls.Skia;

public class TextOperations
{
    public static float MeasureTextLength(string fontFamily, float fontSize, string text)
    {
        var paint = new SKPaint
        {
            Typeface = SKTypeface.FromFamilyName(fontFamily),
            TextSize = fontSize,
            IsAntialias = true
        };

        return paint.MeasureText(text) * 1.05f;
    }

    public static float MeasureTextHeight(string fontFamily, float fontSize, string text)
    {
        var paint = new SKPaint
        {
            Typeface = SKTypeface.FromFamilyName(fontFamily),
            TextSize = fontSize,
            IsAntialias = true
        };

        SKRect textBounds = new SKRect();
        paint.MeasureText(text, ref textBounds);
        return textBounds.Height;
    }
}
