// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Collections.Generic;
using System.IO;
using ShimSkiaSharp;

namespace Svg.Model;

public record struct TypefaceSpan(string Text, float Advance, SKTypeface? Typeface);

public interface ISvgAssetLoader
{
    SKImage LoadImage(Stream stream);
    List<TypefaceSpan> FindTypefaces(string? text, SKPaint paintPreferredTypeface);
    SKFontMetrics GetFontMetrics(SKPaint paint);
    float MeasureText(string? text, SKPaint paint, ref SKRect bounds);
    SKPath? GetTextPath(string? text, SKPaint paint, float x, float y);
}
