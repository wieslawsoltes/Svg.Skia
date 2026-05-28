// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.IO;
using ShimSkiaSharp;

namespace Svg.Model;

public record struct TypefaceSpan(string Text, float Advance, SKTypeface? Typeface);
public readonly record struct ShapedGlyphRun(ushort[] Glyphs, SKPoint[] Points, int[] Clusters, float Advance);
public readonly record struct ShapedTextCluster(int StartCharIndex, int CharLength, int StartGlyphIndex, int GlyphCount, float Offset, float Advance);
public readonly record struct SvgImageLoadContext(Uri ResourceUri, string? CrossOrigin, SvgElement? OwnerElement);

public interface ISvgAssetLoader
{
    bool EnableSvgFonts { get; }
    SKImage LoadImage(Stream stream);
    List<TypefaceSpan> FindTypefaces(string? text, SKPaint paintPreferredTypeface);
    SKFontMetrics GetFontMetrics(SKPaint paint);
    float MeasureText(string? text, SKPaint paint, ref SKRect bounds);
    SKPath? GetTextPath(string? text, SKPaint paint, float x, float y);
}

public interface ISvgImageAssetLoader
{
    SKImage LoadImage(Stream stream, SvgImageLoadContext context);
}

public interface ISvgImageAlphaProvider
{
    bool TryGetImageAlpha(SKImage image, out int width, out int height, out byte[] alpha);
}

public interface ISvgTextReferenceRenderingOptions
{
    bool EnableTextReferences { get; }
}

public interface ISvgFilterBackgroundInputOptions
{
    bool EnableFilterBackgroundInputs { get; }
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

public interface ISvgTextGlyphClusterResolver
{
    bool TryShapeGlyphClusters(string? text, SKPaint paint, bool rightToLeft, out ShapedGlyphRun shapedRun, out ShapedTextCluster[] clusters);
}

public interface ISvgTextGlyphRunPathResolver
{
    bool TryGetGlyphRunPath(ShapedGlyphRun shapedRun, SKPaint paint, float x, float y, out SKPath path);
}
