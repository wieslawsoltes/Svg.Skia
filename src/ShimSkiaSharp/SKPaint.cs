// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

namespace ShimSkiaSharp;

public sealed class SKPaint : ICloneable, IDeepCloneable<SKPaint>
{
    public SKPaintStyle Style { get; set; } = SKPaintStyle.Fill;

    public bool IsAntialias { get; set; } = false;

    public bool IsDither { get; set; } = false;

    public float StrokeWidth { get; set; } = 0;

    public SKStrokeCap StrokeCap { get; set; } = SKStrokeCap.Butt;

    public SKStrokeJoin StrokeJoin { get; set; } = SKStrokeJoin.Miter;

    public float StrokeMiter { get; set; } = 4;

    public SKTypeface? Typeface { get; set; } = null;

    public float TextSize { get; set; } = 12;

    public SKTextAlign TextAlign { get; set; } = SKTextAlign.Left;

    public bool LcdRenderText { get; set; } = false;

    public bool SubpixelText { get; set; } = false;

    public SKTextEncoding TextEncoding { get; set; } = SKTextEncoding.Utf8;

    public SKColor? Color { get; set; } = new SKColor(0x00, 0x00, 0x00, 0xFF);

    public SKShader? Shader { get; set; } = null;

    public SKColorFilter? ColorFilter { get; set; } = null;

    public SKImageFilter? ImageFilter { get; set; } = null;

    public SKPathEffect? PathEffect { get; set; } = null;

    public SKBlendMode BlendMode { get; set; } = SKBlendMode.SrcOver;

    public SKFilterQuality FilterQuality { get; set; } = SKFilterQuality.None;

    public SKPaint Clone()
    {
        return new SKPaint
        {
            Style = Style,
            IsAntialias = IsAntialias,
            IsDither = IsDither,
            StrokeWidth = StrokeWidth,
            StrokeCap = StrokeCap,
            StrokeJoin = StrokeJoin,
            StrokeMiter = StrokeMiter,
            Typeface = Typeface?.Clone(),
            TextSize = TextSize,
            TextAlign = TextAlign,
            LcdRenderText = LcdRenderText,
            SubpixelText = SubpixelText,
            TextEncoding = TextEncoding,
            Color = Color,
            Shader = Shader?.DeepClone(),
            ColorFilter = ColorFilter?.DeepClone(),
            ImageFilter = ImageFilter?.DeepClone(),
            PathEffect = PathEffect?.DeepClone(),
            BlendMode = BlendMode,
            FilterQuality = FilterQuality
        };
    }

    public SKPaint DeepClone() => Clone();

    object ICloneable.Clone() => Clone();
}
