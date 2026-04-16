// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Collections.Generic;
using System.IO;
using ShimSkiaSharp;

namespace Svg.Model;

public record struct TypefaceSpan(string Text, float Advance, SKTypeface? Typeface);
public readonly record struct ShapedGlyphRun(ushort[] Glyphs, SKPoint[] Points, int[] Clusters, float Advance);

public interface ISvgAssetLoader
{
    bool EnableSvgFonts { get; }
    SKImage LoadImage(Stream stream);
    List<TypefaceSpan> FindTypefaces(string? text, SKPaint paintPreferredTypeface);
    SKFontMetrics GetFontMetrics(SKPaint paint);
    float MeasureText(string? text, SKPaint paint, ref SKRect bounds);
    SKPath? GetTextPath(string? text, SKPaint paint, float x, float y);
}

public interface ISvgTextMeasurementCacheKeyProvider
{
    int TextMeasurementCacheKey { get; }
}

public interface ISvgTextReferenceRenderingOptions
{
    bool EnableTextReferences { get; }
}

public interface ISvgTextRunTypefaceResolver
{
    SKTypeface? FindRunTypeface(string? text, SKPaint paintPreferredTypeface);
}

public interface ISvgTextGlyphRunResolver
{
    bool TryShapeGlyphRun(string? text, SKPaint paint, out ShapedGlyphRun shapedRun);
}

public interface ISvgTextDirectedGlyphRunResolver
{
    bool TryShapeGlyphRun(string? text, SKPaint paint, bool rightToLeft, out ShapedGlyphRun shapedRun);
}
