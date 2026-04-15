// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Svg.Skia.TypefaceProviders;

namespace Svg.Skia;

public partial class SkiaSvgAssetLoader
{
    private readonly struct MatchCharacterKey : System.IEquatable<MatchCharacterKey>
    {
        public MatchCharacterKey(
            string? familyName,
            SkiaSharp.SKFontStyleWeight weight,
            SkiaSharp.SKFontStyleWidth width,
            SkiaSharp.SKFontStyleSlant slant,
            int codepoint)
        {
            FamilyName = familyName;
            Weight = weight;
            Width = width;
            Slant = slant;
            Codepoint = codepoint;
        }

        public string? FamilyName { get; }
        public SkiaSharp.SKFontStyleWeight Weight { get; }
        public SkiaSharp.SKFontStyleWidth Width { get; }
        public SkiaSharp.SKFontStyleSlant Slant { get; }
        public int Codepoint { get; }

        public bool Equals(MatchCharacterKey other)
        {
            return string.Equals(FamilyName, other.FamilyName, System.StringComparison.Ordinal)
                && Weight == other.Weight
                && Width == other.Width
                && Slant == other.Slant
                && Codepoint == other.Codepoint;
        }

        public override bool Equals(object? obj)
        {
            return obj is MatchCharacterKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = FamilyName?.GetHashCode() ?? 0;
                hash = (hash * 397) ^ (int)Weight;
                hash = (hash * 397) ^ (int)Width;
                hash = (hash * 397) ^ (int)Slant;
                hash = (hash * 397) ^ Codepoint;
                return hash;
            }
        }
    }

    private readonly struct ProviderTypefaceKey : System.IEquatable<ProviderTypefaceKey>
    {
        public ProviderTypefaceKey(
            ITypefaceProvider provider,
            string familyName,
            SkiaSharp.SKFontStyleWeight weight,
            SkiaSharp.SKFontStyleWidth width,
            SkiaSharp.SKFontStyleSlant slant)
        {
            Provider = provider;
            FamilyName = familyName;
            Weight = weight;
            Width = width;
            Slant = slant;
        }

        public ITypefaceProvider Provider { get; }
        public string FamilyName { get; }
        public SkiaSharp.SKFontStyleWeight Weight { get; }
        public SkiaSharp.SKFontStyleWidth Width { get; }
        public SkiaSharp.SKFontStyleSlant Slant { get; }

        public bool Equals(ProviderTypefaceKey other)
        {
            return ReferenceEquals(Provider, other.Provider)
                && string.Equals(FamilyName, other.FamilyName, System.StringComparison.Ordinal)
                && Weight == other.Weight
                && Width == other.Width
                && Slant == other.Slant;
        }

        public override bool Equals(object? obj)
        {
            return obj is ProviderTypefaceKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = RuntimeHelpers.GetHashCode(Provider);
                hash = (hash * 397) ^ (FamilyName?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (int)Weight;
                hash = (hash * 397) ^ (int)Width;
                hash = (hash * 397) ^ (int)Slant;
                return hash;
            }
        }
    }

    private readonly struct TypefaceSpanCacheKey : System.IEquatable<TypefaceSpanCacheKey>
    {
        public TypefaceSpanCacheKey(string text, ShimSkiaSharp.SKPaint paint)
        {
            Text = text;
            TextSize = paint.TextSize;
            LcdRenderText = paint.LcdRenderText;
            SubpixelText = paint.SubpixelText;
            TextEncoding = paint.TextEncoding;

            if (paint.Typeface is { } typeface)
            {
                HasTypeface = true;
                TypefaceFamilyName = typeface.FamilyName;
                TypefaceWeight = typeface.FontWeight;
                TypefaceWidth = typeface.FontWidth;
                TypefaceSlant = typeface.FontSlant;
            }
            else
            {
                HasTypeface = false;
                TypefaceFamilyName = null;
                TypefaceWeight = default;
                TypefaceWidth = default;
                TypefaceSlant = default;
            }
        }

        public string Text { get; }
        public float TextSize { get; }
        public bool LcdRenderText { get; }
        public bool SubpixelText { get; }
        public ShimSkiaSharp.SKTextEncoding TextEncoding { get; }
        public bool HasTypeface { get; }
        public string? TypefaceFamilyName { get; }
        public ShimSkiaSharp.SKFontStyleWeight TypefaceWeight { get; }
        public ShimSkiaSharp.SKFontStyleWidth TypefaceWidth { get; }
        public ShimSkiaSharp.SKFontStyleSlant TypefaceSlant { get; }

        public bool Equals(TypefaceSpanCacheKey other)
        {
            return string.Equals(Text, other.Text, StringComparison.Ordinal)
                && TextSize.Equals(other.TextSize)
                && LcdRenderText == other.LcdRenderText
                && SubpixelText == other.SubpixelText
                && TextEncoding == other.TextEncoding
                && HasTypeface == other.HasTypeface
                && string.Equals(TypefaceFamilyName, other.TypefaceFamilyName, StringComparison.Ordinal)
                && TypefaceWeight == other.TypefaceWeight
                && TypefaceWidth == other.TypefaceWidth
                && TypefaceSlant == other.TypefaceSlant;
        }

        public override bool Equals(object? obj)
        {
            return obj is TypefaceSpanCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Text.GetHashCode(StringComparison.Ordinal);
                hash = (hash * 397) ^ TextSize.GetHashCode();
                hash = (hash * 397) ^ (LcdRenderText ? 1 : 0);
                hash = (hash * 397) ^ (SubpixelText ? 1 : 0);
                hash = (hash * 397) ^ (int)TextEncoding;
                hash = (hash * 397) ^ (HasTypeface ? 1 : 0);
                hash = (hash * 397) ^ (TypefaceFamilyName?.GetHashCode(StringComparison.Ordinal) ?? 0);
                hash = (hash * 397) ^ (int)TypefaceWeight;
                hash = (hash * 397) ^ (int)TypefaceWidth;
                hash = (hash * 397) ^ (int)TypefaceSlant;
                return hash;
            }
        }
    }

    private readonly struct PaintSignature : System.IEquatable<PaintSignature>
    {
        public PaintSignature(ShimSkiaSharp.SKPaint paint)
        {
            Style = paint.Style;
            IsAntialias = paint.IsAntialias;
            StrokeWidth = paint.StrokeWidth;
            StrokeCap = paint.StrokeCap;
            StrokeJoin = paint.StrokeJoin;
            StrokeMiter = paint.StrokeMiter;
            TextSize = paint.TextSize;
            TextAlign = paint.TextAlign;
            LcdRenderText = paint.LcdRenderText;
            SubpixelText = paint.SubpixelText;
            TextEncoding = paint.TextEncoding;
            BlendMode = paint.BlendMode;
            FilterQuality = paint.FilterQuality;
            if (paint.Typeface is { } typeface)
            {
                HasTypeface = true;
                TypefaceFamilyName = typeface.FamilyName;
                TypefaceWeight = typeface.FontWeight;
                TypefaceWidth = typeface.FontWidth;
                TypefaceSlant = typeface.FontSlant;
            }
            else
            {
                HasTypeface = false;
                TypefaceFamilyName = null;
                TypefaceWeight = default;
                TypefaceWidth = default;
                TypefaceSlant = default;
            }
            Shader = paint.Shader;
            ColorFilter = paint.ColorFilter;
            ImageFilter = paint.ImageFilter;
            PathEffect = paint.PathEffect;

            if (paint.Color is { } color)
            {
                HasColor = true;
                Color = color;
            }
            else
            {
                HasColor = false;
                Color = default;
            }
        }

        public ShimSkiaSharp.SKPaintStyle Style { get; }
        public bool IsAntialias { get; }
        public float StrokeWidth { get; }
        public ShimSkiaSharp.SKStrokeCap StrokeCap { get; }
        public ShimSkiaSharp.SKStrokeJoin StrokeJoin { get; }
        public float StrokeMiter { get; }
        public float TextSize { get; }
        public ShimSkiaSharp.SKTextAlign TextAlign { get; }
        public bool LcdRenderText { get; }
        public bool SubpixelText { get; }
        public ShimSkiaSharp.SKTextEncoding TextEncoding { get; }
        public ShimSkiaSharp.SKBlendMode BlendMode { get; }
        public ShimSkiaSharp.SKFilterQuality FilterQuality { get; }
        public bool HasTypeface { get; }
        public string? TypefaceFamilyName { get; }
        public ShimSkiaSharp.SKFontStyleWeight TypefaceWeight { get; }
        public ShimSkiaSharp.SKFontStyleWidth TypefaceWidth { get; }
        public ShimSkiaSharp.SKFontStyleSlant TypefaceSlant { get; }
        public ShimSkiaSharp.SKShader? Shader { get; }
        public ShimSkiaSharp.SKColorFilter? ColorFilter { get; }
        public ShimSkiaSharp.SKImageFilter? ImageFilter { get; }
        public ShimSkiaSharp.SKPathEffect? PathEffect { get; }
        public bool HasColor { get; }
        public ShimSkiaSharp.SKColor Color { get; }

        public bool Equals(PaintSignature other)
        {
            return Style == other.Style
                && IsAntialias == other.IsAntialias
                && StrokeWidth.Equals(other.StrokeWidth)
                && StrokeCap == other.StrokeCap
                && StrokeJoin == other.StrokeJoin
                && StrokeMiter.Equals(other.StrokeMiter)
                && TextSize.Equals(other.TextSize)
                && TextAlign == other.TextAlign
                && LcdRenderText == other.LcdRenderText
                && SubpixelText == other.SubpixelText
                && TextEncoding == other.TextEncoding
                && BlendMode == other.BlendMode
                && FilterQuality == other.FilterQuality
                && HasTypeface == other.HasTypeface
                && string.Equals(TypefaceFamilyName, other.TypefaceFamilyName, StringComparison.Ordinal)
                && TypefaceWeight == other.TypefaceWeight
                && TypefaceWidth == other.TypefaceWidth
                && TypefaceSlant == other.TypefaceSlant
                && ReferenceEquals(Shader, other.Shader)
                && ReferenceEquals(ColorFilter, other.ColorFilter)
                && ReferenceEquals(ImageFilter, other.ImageFilter)
                && ReferenceEquals(PathEffect, other.PathEffect)
                && HasColor == other.HasColor
                && (!HasColor || (Color.Red == other.Color.Red
                    && Color.Green == other.Color.Green
                    && Color.Blue == other.Color.Blue
                    && Color.Alpha == other.Color.Alpha));
        }

        public override bool Equals(object? obj)
        {
            return obj is PaintSignature other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Style;
                hash = (hash * 397) ^ (IsAntialias ? 1 : 0);
                hash = (hash * 397) ^ StrokeWidth.GetHashCode();
                hash = (hash * 397) ^ (int)StrokeCap;
                hash = (hash * 397) ^ (int)StrokeJoin;
                hash = (hash * 397) ^ StrokeMiter.GetHashCode();
                hash = (hash * 397) ^ TextSize.GetHashCode();
                hash = (hash * 397) ^ (int)TextAlign;
                hash = (hash * 397) ^ (LcdRenderText ? 1 : 0);
                hash = (hash * 397) ^ (SubpixelText ? 1 : 0);
                hash = (hash * 397) ^ (int)TextEncoding;
                hash = (hash * 397) ^ (int)BlendMode;
                hash = (hash * 397) ^ (int)FilterQuality;
                hash = (hash * 397) ^ (HasTypeface ? 1 : 0);
                hash = (hash * 397) ^ (TypefaceFamilyName?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (int)TypefaceWeight;
                hash = (hash * 397) ^ (int)TypefaceWidth;
                hash = (hash * 397) ^ (int)TypefaceSlant;
                hash = (hash * 397) ^ (Shader is null ? 0 : RuntimeHelpers.GetHashCode(Shader));
                hash = (hash * 397) ^ (ColorFilter is null ? 0 : RuntimeHelpers.GetHashCode(ColorFilter));
                hash = (hash * 397) ^ (ImageFilter is null ? 0 : RuntimeHelpers.GetHashCode(ImageFilter));
                hash = (hash * 397) ^ (PathEffect is null ? 0 : RuntimeHelpers.GetHashCode(PathEffect));
                hash = (hash * 397) ^ (HasColor ? 1 : 0);
                if (HasColor)
                {
                    hash = (hash * 397) ^ Color.Red;
                    hash = (hash * 397) ^ Color.Green;
                    hash = (hash * 397) ^ Color.Blue;
                    hash = (hash * 397) ^ Color.Alpha;
                }

                return hash;
            }
        }
    }

    private sealed class CachedSkPaint
    {
        public CachedSkPaint(PaintSignature signature, SkiaSharp.SKPaint paint)
        {
            Signature = signature;
            Paint = paint;
        }

        public PaintSignature Signature { get; }
        public SkiaSharp.SKPaint Paint { get; }

        public void Dispose()
        {
            if (Paint.Handle != IntPtr.Zero)
            {
                Paint.Dispose();
            }
        }
    }

    private const int MatchCharacterCacheLimit = 4096;
    private const int ProviderTypefaceCacheLimit = 512;
    private const int TypefaceSpanCacheLimit = 1024;
    private const int PaintCacheRefTrimThreshold = 1024;
    private readonly ConcurrentDictionary<MatchCharacterKey, SkiaSharp.SKTypeface?> _matchCharacterCache = new();
    private readonly ConcurrentDictionary<ProviderTypefaceKey, SkiaSharp.SKTypeface?> _providerTypefaceCache = new();
    private readonly ConcurrentDictionary<TypefaceSpanCacheKey, Model.TypefaceSpan[]> _typefaceSpanCache = new();
    private readonly object _paintCacheLock = new();
    private ConditionalWeakTable<ShimSkiaSharp.SKPaint, CachedSkPaint> _paintCache = new();
    private readonly List<WeakReference<SkiaSharp.SKPaint>> _paintCacheRefs = new();
    private IList<ITypefaceProvider>? _providerStateList;
    private int _providerStateHash;
}
