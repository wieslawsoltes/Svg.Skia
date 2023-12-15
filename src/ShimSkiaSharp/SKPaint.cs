/*
 * Svg.Skia SVG rendering library.
 * Copyright (C) 2023  Wiesław Šoltés
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as
 * published by the Free Software Foundation, either version 3 of the
 * License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
namespace ShimSkiaSharp;

public sealed class SKPaint
{
    public SKPaintStyle Style { get; set; }

    public bool IsAntialias { get; set; }

    public float StrokeWidth { get; set; }

    public SKStrokeCap StrokeCap { get; set; }

    public SKStrokeJoin StrokeJoin { get; set; }

    public float StrokeMiter { get; set; }

    public SKTypeface? Typeface { get; set; }

    public float TextSize { get; set; }

    public SKTextAlign TextAlign { get; set; }

    public bool LcdRenderText { get; set; }

    public bool SubpixelText { get; set; }

    public SKTextEncoding TextEncoding { get; set; }

    public SKColor? Color { get; set; }

    public SKShader? Shader { get; set; }

    public SKColorFilter? ColorFilter { get; set; }

    public SKImageFilter? ImageFilter { get; set; }

    public SKPathEffect? PathEffect { get; set; }

    public SKBlendMode BlendMode { get; set; }

    public SKFilterQuality FilterQuality { get; set; }

    public SKPaint()
    {
        Style = SKPaintStyle.Fill;
        IsAntialias = false;
        StrokeWidth = 0;
        StrokeCap = SKStrokeCap.Butt;
        StrokeJoin = SKStrokeJoin.Miter;
        StrokeMiter = 4;
        Typeface = null;
        TextSize = 12;
        TextAlign = SKTextAlign.Left;
        LcdRenderText = false;
        SubpixelText = false;
        TextEncoding = SKTextEncoding.Utf8;
        Color = new SKColor(0x00, 0x00, 0x00, 0xFF);
        Shader = null;
        ColorFilter = null;
        ImageFilter = null;
        PathEffect = null;
        BlendMode = SKBlendMode.SrcOver;
        FilterQuality = SKFilterQuality.None;
    }

    public SKPaint Clone()
    {
        return new SKPaint
        {
            Style = Style,
            IsAntialias = IsAntialias,
            StrokeWidth = StrokeWidth,
            StrokeCap = StrokeCap,
            StrokeJoin = StrokeJoin,
            StrokeMiter = StrokeMiter,
            Typeface = Typeface,
            TextSize = TextSize,
            TextAlign = TextAlign,
            LcdRenderText = LcdRenderText,
            SubpixelText = SubpixelText,
            TextEncoding = TextEncoding,
            Color = Color,
            Shader = Shader,
            ColorFilter = ColorFilter,
            ImageFilter = ImageFilter,
            PathEffect = PathEffect,
            BlendMode = BlendMode,
            FilterQuality = FilterQuality
        };
    }
}
