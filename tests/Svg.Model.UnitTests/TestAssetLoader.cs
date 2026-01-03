using System.Collections.Generic;
using System.IO;
using ShimSkiaSharp;
using Svg.Model;

namespace Svg.Model.UnitTests;

internal sealed class TestAssetLoader : ISvgAssetLoader
{
    public SKImage LoadImage(Stream stream)
        => new() { Data = SKImage.FromStream(stream) };

    public List<TypefaceSpan> FindTypefaces(string? text, SKPaint paintPreferredTypeface)
        => new();

    public SKFontMetrics GetFontMetrics(SKPaint paint)
        => default;

    public float MeasureText(string? text, SKPaint paint, ref SKRect bounds)
        => 0f;

    public SKPath? GetTextPath(string? text, SKPaint paint, float x, float y)
        => new SKPath();
}
