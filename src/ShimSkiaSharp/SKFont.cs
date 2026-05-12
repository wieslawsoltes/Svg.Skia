// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

namespace ShimSkiaSharp;

public enum SKFontEdging
{
    Alias,
    Antialias,
    SubpixelAntialias
}

public sealed class SKFont : ICloneable, IDeepCloneable<SKFont>
{
    private const float DefaultSize = 12f;
    private const float DefaultScaleX = 1f;
    private const float DefaultSkewX = 0f;

    private SKTypeface? _typeface;
    private float _size = DefaultSize;
    private float _scaleX = DefaultScaleX;
    private float _skewX = DefaultSkewX;
    private bool _subpixel;
    private bool _embolden;
    private SKFontEdging _edging = SKFontEdging.Antialias;
    private int _version;

    public SKFont()
    {
    }

    public SKFont(SKTypeface? typeface, float size = DefaultSize, float scaleX = DefaultScaleX, float skewX = DefaultSkewX)
    {
        _typeface = typeface;
        _size = size;
        _scaleX = scaleX;
        _skewX = skewX;
    }

    internal int Version => _version;

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

    public float Size
    {
        get => _size;
        set
        {
            if (_size.Equals(value))
            {
                return;
            }

            _size = value;
            _version++;
        }
    }

    public float ScaleX
    {
        get => _scaleX;
        set
        {
            if (_scaleX.Equals(value))
            {
                return;
            }

            _scaleX = value;
            _version++;
        }
    }

    public float SkewX
    {
        get => _skewX;
        set
        {
            if (_skewX.Equals(value))
            {
                return;
            }

            _skewX = value;
            _version++;
        }
    }

    public bool Subpixel
    {
        get => _subpixel;
        set
        {
            if (_subpixel == value)
            {
                return;
            }

            _subpixel = value;
            _version++;
        }
    }

    public bool Embolden
    {
        get => _embolden;
        set
        {
            if (_embolden == value)
            {
                return;
            }

            _embolden = value;
            _version++;
        }
    }

    public SKFontEdging Edging
    {
        get => _edging;
        set
        {
            if (_edging == value)
            {
                return;
            }

            _edging = value;
            _version++;
        }
    }

    public SKFont Clone() => DeepClone(new CloneContext());

    public SKFont DeepClone() => Clone();

    object ICloneable.Clone() => Clone();

    internal SKFont DeepClone(CloneContext context)
    {
        if (context.TryGet(this, out SKFont existing))
        {
            return existing;
        }

        var clone = new SKFont();
        context.Add(this, clone);

        clone.Typeface = Typeface?.DeepClone(context);
        clone.Size = Size;
        clone.ScaleX = ScaleX;
        clone.SkewX = SkewX;
        clone.Subpixel = Subpixel;
        clone.Embolden = Embolden;
        clone.Edging = Edging;

        return clone;
    }
}
