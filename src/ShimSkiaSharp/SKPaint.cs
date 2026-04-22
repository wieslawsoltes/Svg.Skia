// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

namespace ShimSkiaSharp;

public sealed class SKPaint : ICloneable, IDeepCloneable<SKPaint>
{
    private SKPaintStyle _style = SKPaintStyle.Fill;
    private bool _isAntialias;
    private bool _isDither;
    private float _strokeWidth;
    private SKStrokeCap _strokeCap = SKStrokeCap.Butt;
    private SKStrokeJoin _strokeJoin = SKStrokeJoin.Miter;
    private float _strokeMiter = 4;
    private bool _isStrokeNonScaling;
    private SKTypeface? _typeface;
    private float _textSize = 12;
    private SKTextAlign _textAlign = SKTextAlign.Left;
    private bool _lcdRenderText;
    private bool _subpixelText;
    private SKTextEncoding _textEncoding = SKTextEncoding.Utf8;
    private SKColor? _color = new SKColor(0x00, 0x00, 0x00, 0xFF);
    private SKShader? _shader;
    private SKColorFilter? _colorFilter;
    private SKImageFilter? _imageFilter;
    private SKPathEffect? _pathEffect;
    private SKBlendMode _blendMode = SKBlendMode.SrcOver;
    private SKFilterQuality _filterQuality = SKFilterQuality.None;
    private int _version;

    internal int Version => _version;

    public SKPaintStyle Style
    {
        get => _style;
        set
        {
            if (_style == value)
            {
                return;
            }

            _style = value;
            _version++;
        }
    }

    public bool IsAntialias
    {
        get => _isAntialias;
        set
        {
            if (_isAntialias == value)
            {
                return;
            }

            _isAntialias = value;
            _version++;
        }
    }

    public bool IsDither
    {
        get => _isDither;
        set
        {
            if (_isDither == value)
            {
                return;
            }

            _isDither = value;
            _version++;
        }
    }

    public float StrokeWidth
    {
        get => _strokeWidth;
        set
        {
            if (_strokeWidth.Equals(value))
            {
                return;
            }

            _strokeWidth = value;
            _version++;
        }
    }

    public SKStrokeCap StrokeCap
    {
        get => _strokeCap;
        set
        {
            if (_strokeCap == value)
            {
                return;
            }

            _strokeCap = value;
            _version++;
        }
    }

    public SKStrokeJoin StrokeJoin
    {
        get => _strokeJoin;
        set
        {
            if (_strokeJoin == value)
            {
                return;
            }

            _strokeJoin = value;
            _version++;
        }
    }

    public float StrokeMiter
    {
        get => _strokeMiter;
        set
        {
            if (_strokeMiter.Equals(value))
            {
                return;
            }

            _strokeMiter = value;
            _version++;
        }
    }

    public bool IsStrokeNonScaling
    {
        get => _isStrokeNonScaling;
        set
        {
            if (_isStrokeNonScaling == value)
            {
                return;
            }

            _isStrokeNonScaling = value;
            _version++;
        }
    }

    public SKTypeface? Typeface
    {
        get => _typeface;
        set
        {
            if (ReferenceEquals(_typeface, value))
            {
                return;
            }

            _typeface = value;
            _version++;
        }
    }

    public float TextSize
    {
        get => _textSize;
        set
        {
            if (_textSize.Equals(value))
            {
                return;
            }

            _textSize = value;
            _version++;
        }
    }

    public SKTextAlign TextAlign
    {
        get => _textAlign;
        set
        {
            if (_textAlign == value)
            {
                return;
            }

            _textAlign = value;
            _version++;
        }
    }

    public bool LcdRenderText
    {
        get => _lcdRenderText;
        set
        {
            if (_lcdRenderText == value)
            {
                return;
            }

            _lcdRenderText = value;
            _version++;
        }
    }

    public bool SubpixelText
    {
        get => _subpixelText;
        set
        {
            if (_subpixelText == value)
            {
                return;
            }

            _subpixelText = value;
            _version++;
        }
    }

    public SKTextEncoding TextEncoding
    {
        get => _textEncoding;
        set
        {
            if (_textEncoding == value)
            {
                return;
            }

            _textEncoding = value;
            _version++;
        }
    }

    public SKColor? Color
    {
        get => _color;
        set
        {
            if (_color.Equals(value))
            {
                return;
            }

            _color = value;
            _version++;
        }
    }

    public SKShader? Shader
    {
        get => _shader;
        set
        {
            if (ReferenceEquals(_shader, value))
            {
                return;
            }

            _shader = value;
            _version++;
        }
    }

    public SKColorFilter? ColorFilter
    {
        get => _colorFilter;
        set
        {
            if (ReferenceEquals(_colorFilter, value))
            {
                return;
            }

            _colorFilter = value;
            _version++;
        }
    }

    public SKImageFilter? ImageFilter
    {
        get => _imageFilter;
        set
        {
            if (ReferenceEquals(_imageFilter, value))
            {
                return;
            }

            _imageFilter = value;
            _version++;
        }
    }

    public SKPathEffect? PathEffect
    {
        get => _pathEffect;
        set
        {
            if (ReferenceEquals(_pathEffect, value))
            {
                return;
            }

            _pathEffect = value;
            _version++;
        }
    }

    public SKBlendMode BlendMode
    {
        get => _blendMode;
        set
        {
            if (_blendMode == value)
            {
                return;
            }

            _blendMode = value;
            _version++;
        }
    }

    public SKFilterQuality FilterQuality
    {
        get => _filterQuality;
        set
        {
            if (_filterQuality == value)
            {
                return;
            }

            _filterQuality = value;
            _version++;
        }
    }

    public SKPaint Clone() => DeepClone(new CloneContext());

    public SKPaint DeepClone() => Clone();

    object ICloneable.Clone() => Clone();

    internal SKPaint DeepClone(CloneContext context)
    {
        if (context.TryGet(this, out SKPaint existing))
        {
            return existing;
        }

        var clone = new SKPaint();
        context.Add(this, clone);

        clone.Style = Style;
        clone.IsAntialias = IsAntialias;
        clone.IsDither = IsDither;
        clone.StrokeWidth = StrokeWidth;
        clone.StrokeCap = StrokeCap;
        clone.StrokeJoin = StrokeJoin;
        clone.StrokeMiter = StrokeMiter;
        clone.IsStrokeNonScaling = IsStrokeNonScaling;
        clone.Typeface = Typeface?.DeepClone(context);
        clone.TextSize = TextSize;
        clone.TextAlign = TextAlign;
        clone.LcdRenderText = LcdRenderText;
        clone.SubpixelText = SubpixelText;
        clone.TextEncoding = TextEncoding;
        clone.Color = Color;
        clone.Shader = Shader?.DeepClone(context);
        clone.ColorFilter = ColorFilter?.DeepClone(context);
        clone.ImageFilter = ImageFilter?.DeepClone(context);
        clone.PathEffect = PathEffect?.DeepClone(context);
        clone.BlendMode = BlendMode;
        clone.FilterQuality = FilterQuality;

        return clone;
    }
}
