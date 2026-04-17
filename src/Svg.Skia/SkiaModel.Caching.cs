// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using ShimSkiaSharp;
using Svg.Skia.TypefaceProviders;

namespace Svg.Skia;

public partial class SkiaModel
{
    private const int TypefaceCacheLimit = 512;
    private const int ResolvedTypefaceCacheLimit = 512;
    private const int PositionedTextCacheRefTrimThreshold = 1024;
    private const int SharedRenderPaintTemplateCacheLimit = 8192;
    private const int SharedRenderPathCacheLimit = 8192;

    private sealed class PictureReferenceEqualityComparer : IEqualityComparer<ShimSkiaSharp.SKPicture>
    {
        public static readonly PictureReferenceEqualityComparer Instance = new();

        public bool Equals(ShimSkiaSharp.SKPicture? x, ShimSkiaSharp.SKPicture? y) => ReferenceEquals(x, y);

        public int GetHashCode(ShimSkiaSharp.SKPicture obj) => RuntimeHelpers.GetHashCode(obj);
    }

    private sealed class ObjectReferenceEqualityComparer<T> : IEqualityComparer<T>
        where T : class
    {
        public static readonly ObjectReferenceEqualityComparer<T> Instance = new();

        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

        public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
    }

    private struct RevisionBuilder
    {
        private int _hash;

        public void Add<T>(T value)
        {
            _hash = unchecked((_hash * 397) ^ (value?.GetHashCode() ?? 0));
        }

        public int ToRevision()
        {
            return _hash;
        }
    }

    private struct PathSignatureBuilder
    {
        private int _hash1;
        private int _hash2;

        public void Add<T>(T value)
        {
            var hash = value?.GetHashCode() ?? 0;
            _hash1 = unchecked((_hash1 * 397) ^ hash);
            _hash2 = unchecked((_hash2 * 1009) ^ (hash * 16777619));
        }

        public RenderPathSignature ToSignature(int commandCount, ShimSkiaSharp.SKPathFillType fillType)
        {
            return new RenderPathSignature(commandCount, fillType, _hash1, _hash2);
        }
    }

    private readonly struct TypefaceKey : IEquatable<TypefaceKey>
    {
        public TypefaceKey(
            string? familyName,
            SkiaSharp.SKFontStyleWeight weight,
            SkiaSharp.SKFontStyleWidth width,
            SkiaSharp.SKFontStyleSlant slant)
        {
            FamilyName = familyName;
            Weight = weight;
            Width = width;
            Slant = slant;
        }

        public string? FamilyName { get; }
        public SkiaSharp.SKFontStyleWeight Weight { get; }
        public SkiaSharp.SKFontStyleWidth Width { get; }
        public SkiaSharp.SKFontStyleSlant Slant { get; }

        public bool Equals(TypefaceKey other)
        {
            return string.Equals(FamilyName, other.FamilyName, StringComparison.Ordinal)
                && Weight == other.Weight
                && Width == other.Width
                && Slant == other.Slant;
        }

        public override bool Equals(object? obj)
        {
            return obj is TypefaceKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = FamilyName?.GetHashCode() ?? 0;
                hash = (hash * 397) ^ (int)Weight;
                hash = (hash * 397) ^ (int)Width;
                hash = (hash * 397) ^ (int)Slant;
                return hash;
            }
        }
    }

    private readonly struct FontSignature : IEquatable<FontSignature>
    {
        public FontSignature(SkiaSharp.SKPaint paint)
        {
            TypefaceHandle = paint.Typeface?.Handle ?? IntPtr.Zero;
            TextSize = paint.TextSize;
            TextScaleX = paint.TextScaleX;
            TextSkewX = paint.TextSkewX;
            LcdRenderText = paint.LcdRenderText;
            SubpixelText = paint.SubpixelText;
            FakeBoldText = paint.FakeBoldText;
        }

        public IntPtr TypefaceHandle { get; }
        public float TextSize { get; }
        public float TextScaleX { get; }
        public float TextSkewX { get; }
        public bool LcdRenderText { get; }
        public bool SubpixelText { get; }
        public bool FakeBoldText { get; }

        public bool Equals(FontSignature other)
        {
            return TypefaceHandle == other.TypefaceHandle
                && TextSize.Equals(other.TextSize)
                && TextScaleX.Equals(other.TextScaleX)
                && TextSkewX.Equals(other.TextSkewX)
                && LcdRenderText == other.LcdRenderText
                && SubpixelText == other.SubpixelText
                && FakeBoldText == other.FakeBoldText;
        }

        public override bool Equals(object? obj)
        {
            return obj is FontSignature other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = TypefaceHandle.GetHashCode();
                hash = (hash * 397) ^ TextSize.GetHashCode();
                hash = (hash * 397) ^ TextScaleX.GetHashCode();
                hash = (hash * 397) ^ TextSkewX.GetHashCode();
                hash = (hash * 397) ^ LcdRenderText.GetHashCode();
                hash = (hash * 397) ^ SubpixelText.GetHashCode();
                hash = (hash * 397) ^ FakeBoldText.GetHashCode();
                return hash;
            }
        }
    }

    private sealed class PositionedTextCacheSet
    {
        public object SyncRoot { get; } = new();
        public Dictionary<FontSignature, SkiaSharp.SKTextBlob> Entries { get; } = new();
    }

    private sealed class NativePaintCacheEntry
    {
        public NativePaintCacheEntry(int version, SkiaSharp.SKPaint paint)
        {
            Version = version;
            Paint = paint;
        }

        public int Version { get; }
        public SkiaSharp.SKPaint Paint { get; }
    }

    private readonly struct RenderPaintSignature : IEquatable<RenderPaintSignature>
    {
        public RenderPaintSignature(ShimSkiaSharp.SKPaint paint)
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
        public bool HasColor { get; }
        public ShimSkiaSharp.SKColor Color { get; }

        public bool Equals(RenderPaintSignature other)
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
                && HasColor == other.HasColor
                && (!HasColor || (Color.Red == other.Color.Red
                    && Color.Green == other.Color.Green
                    && Color.Blue == other.Color.Blue
                    && Color.Alpha == other.Color.Alpha));
        }

        public override bool Equals(object? obj)
        {
            return obj is RenderPaintSignature other && Equals(other);
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

    private readonly struct SharedRenderPaintTemplate
    {
        public SharedRenderPaintTemplate(
            SkiaSharp.SKPaintStyle style,
            bool isAntialias,
            float strokeWidth,
            SkiaSharp.SKStrokeCap strokeCap,
            SkiaSharp.SKStrokeJoin strokeJoin,
            float strokeMiter,
            float textSize,
            SkiaSharp.SKTextAlign textAlign,
            SkiaSharp.SKTypeface? typeface,
            bool lcdRenderText,
            bool subpixelText,
            SkiaSharp.SKTextEncoding textEncoding,
            SkiaSharp.SKColor color,
            SkiaSharp.SKBlendMode blendMode,
            SkiaSharp.SKFilterQuality filterQuality,
            bool fakeBoldText)
        {
            Style = style;
            IsAntialias = isAntialias;
            StrokeWidth = strokeWidth;
            StrokeCap = strokeCap;
            StrokeJoin = strokeJoin;
            StrokeMiter = strokeMiter;
            TextSize = textSize;
            TextAlign = textAlign;
            Typeface = typeface;
            LcdRenderText = lcdRenderText;
            SubpixelText = subpixelText;
            TextEncoding = textEncoding;
            Color = color;
            BlendMode = blendMode;
            FilterQuality = filterQuality;
            FakeBoldText = fakeBoldText;
        }

        public SkiaSharp.SKPaintStyle Style { get; }
        public bool IsAntialias { get; }
        public float StrokeWidth { get; }
        public SkiaSharp.SKStrokeCap StrokeCap { get; }
        public SkiaSharp.SKStrokeJoin StrokeJoin { get; }
        public float StrokeMiter { get; }
        public float TextSize { get; }
        public SkiaSharp.SKTextAlign TextAlign { get; }
        public SkiaSharp.SKTypeface? Typeface { get; }
        public bool LcdRenderText { get; }
        public bool SubpixelText { get; }
        public SkiaSharp.SKTextEncoding TextEncoding { get; }
        public SkiaSharp.SKColor Color { get; }
        public SkiaSharp.SKBlendMode BlendMode { get; }
        public SkiaSharp.SKFilterQuality FilterQuality { get; }
        public bool FakeBoldText { get; }
    }

    private readonly struct RenderPathSignature : IEquatable<RenderPathSignature>
    {
        public RenderPathSignature(int commandCount, ShimSkiaSharp.SKPathFillType fillType, int hash1, int hash2)
        {
            CommandCount = commandCount;
            FillType = fillType;
            Hash1 = hash1;
            Hash2 = hash2;
        }

        public int CommandCount { get; }
        public ShimSkiaSharp.SKPathFillType FillType { get; }
        public int Hash1 { get; }
        public int Hash2 { get; }

        public bool Equals(RenderPathSignature other)
        {
            return CommandCount == other.CommandCount &&
                   FillType == other.FillType &&
                   Hash1 == other.Hash1 &&
                   Hash2 == other.Hash2;
        }

        public override bool Equals(object? obj)
        {
            return obj is RenderPathSignature other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = CommandCount;
                hash = (hash * 397) ^ (int)FillType;
                hash = (hash * 397) ^ Hash1;
                hash = (hash * 397) ^ Hash2;
                return hash;
            }
        }
    }

    private sealed class NativePathCacheEntry
    {
        public NativePathCacheEntry(int revision, SkiaSharp.SKPath path)
        {
            Revision = revision;
            Path = path;
        }

        public int Revision { get; }
        public SkiaSharp.SKPath Path { get; }
    }

    private sealed class RenderPathSignatureCacheEntry
    {
        public RenderPathSignatureCacheEntry(int revision, RenderPathSignature signature)
        {
            Revision = revision;
            Signature = signature;
        }

        public int Revision { get; }
        public RenderPathSignature Signature { get; }
    }

    private sealed class NativeImageCacheEntry
    {
        public NativeImageCacheEntry(int revision, SkiaSharp.SKImage image)
        {
            Revision = revision;
            Image = image;
        }

        public int Revision { get; }
        public SkiaSharp.SKImage Image { get; }
    }

    private sealed class NativeShaderCacheEntry
    {
        public NativeShaderCacheEntry(int revision, SkiaSharp.SKShader shader)
        {
            Revision = revision;
            Shader = shader;
        }

        public int Revision { get; }
        public SkiaSharp.SKShader Shader { get; }
    }

    private sealed class NativeColorFilterCacheEntry
    {
        public NativeColorFilterCacheEntry(int revision, SkiaSharp.SKColorFilter colorFilter)
        {
            Revision = revision;
            ColorFilter = colorFilter;
        }

        public int Revision { get; }
        public SkiaSharp.SKColorFilter ColorFilter { get; }
    }

    private sealed class NativePathEffectCacheEntry
    {
        public NativePathEffectCacheEntry(int revision, SkiaSharp.SKPathEffect pathEffect)
        {
            Revision = revision;
            PathEffect = pathEffect;
        }

        public int Revision { get; }
        public SkiaSharp.SKPathEffect PathEffect { get; }
    }

    private sealed class NativeImageFilterCacheEntry
    {
        public NativeImageFilterCacheEntry(int revision, SkiaSharp.SKImageFilter imageFilter)
        {
            Revision = revision;
            ImageFilter = imageFilter;
        }

        public int Revision { get; }
        public SkiaSharp.SKImageFilter ImageFilter { get; }
    }

    private static readonly ConcurrentDictionary<TypefaceKey, SkiaSharp.SKTypeface?> s_sharedTypefaceCache = new();
    private static readonly ConcurrentDictionary<TypefaceKey, SkiaSharp.SKTypeface?> s_sharedResolvedTypefaceCache = new();
    private static readonly ConcurrentDictionary<RenderPaintSignature, SharedRenderPaintTemplate> s_sharedRenderPaintTemplateCache = new();
    private static readonly ConcurrentDictionary<RenderPathSignature, SkiaSharp.SKPath> s_sharedRenderPathCache = new();
    private static readonly ConditionalWeakTable<ShimSkiaSharp.SKPath, RenderPathSignatureCacheEntry> s_sharedRenderPathSignatureCache = new();
    private static readonly ConditionalWeakTable<ShimSkiaSharp.SKTextBlob, PositionedTextCacheSet> s_sharedPositionedTextCache = new();
    private ConcurrentDictionary<TypefaceKey, SkiaSharp.SKTypeface?>? _typefaceCache;
    private ConcurrentDictionary<TypefaceKey, SkiaSharp.SKTypeface?>? _resolvedTypefaceCache;
    private object? _positionedTextCacheLock;
    private object? _pictureCacheLock;
    private object? _nativeObjectCacheLock;
    private ConditionalWeakTable<ShimSkiaSharp.SKTextBlob, PositionedTextCacheSet>? _positionedTextCache;
    private ConditionalWeakTable<ShimSkiaSharp.SKPaint, NativePaintCacheEntry>? _nativePaintCache;
    private ConditionalWeakTable<ShimSkiaSharp.SKPath, NativePathCacheEntry>? _nativePathCache;
    private ConditionalWeakTable<ShimSkiaSharp.SKImage, NativeImageCacheEntry>? _nativeImageCache;
    private ConditionalWeakTable<ShimSkiaSharp.SKShader, NativeShaderCacheEntry>? _nativeShaderCache;
    private ConditionalWeakTable<ShimSkiaSharp.SKColorFilter, NativeColorFilterCacheEntry>? _nativeColorFilterCache;
    private ConditionalWeakTable<ShimSkiaSharp.SKPathEffect, NativePathEffectCacheEntry>? _nativePathEffectCache;
    private ConditionalWeakTable<ShimSkiaSharp.SKImageFilter, NativeImageFilterCacheEntry>? _nativeImageFilterCache;
    private List<WeakReference<SkiaSharp.SKTextBlob>>? _positionedTextCacheRefs;
    private Dictionary<ShimSkiaSharp.SKPicture, SkiaSharp.SKPicture>? _pictureCache;
    private IList<ITypefaceProvider>? _providerStateList;
    private int _providerStateHash;

    private static bool CanCacheRenderPaint(ShimSkiaSharp.SKPaint paint)
    {
        return paint.Shader is null
            && paint.ColorFilter is null
            && paint.ImageFilter is null
            && paint.PathEffect is null;
    }

    private bool CanUseSharedRenderPaintTemplates(ShimSkiaSharp.SKPaint paint)
    {
        return paint.Typeface is null || UsesSharedTypefaceCaches();
    }

    private static SharedRenderPaintTemplate CreateSharedRenderPaintTemplate(SkiaSharp.SKPaint paint)
    {
        return new SharedRenderPaintTemplate(
            paint.Style,
            paint.IsAntialias,
            paint.StrokeWidth,
            paint.StrokeCap,
            paint.StrokeJoin,
            paint.StrokeMiter,
            paint.TextSize,
            paint.TextAlign,
            paint.Typeface,
            paint.LcdRenderText,
            paint.SubpixelText,
            paint.TextEncoding,
            paint.Color,
            paint.BlendMode,
            paint.FilterQuality,
            paint.FakeBoldText);
    }

    private static SkiaSharp.SKPaint CreateRenderPaint(in SharedRenderPaintTemplate template)
    {
        return new SkiaSharp.SKPaint
        {
            Style = template.Style,
            IsAntialias = template.IsAntialias,
            StrokeWidth = template.StrokeWidth,
            StrokeCap = template.StrokeCap,
            StrokeJoin = template.StrokeJoin,
            StrokeMiter = template.StrokeMiter,
            TextSize = template.TextSize,
            TextAlign = template.TextAlign,
            Typeface = template.Typeface,
            LcdRenderText = template.LcdRenderText,
            SubpixelText = template.SubpixelText,
            TextEncoding = template.TextEncoding,
            Color = template.Color,
            BlendMode = template.BlendMode,
            FilterQuality = template.FilterQuality,
            FakeBoldText = template.FakeBoldText
        };
    }

    private static void TrimSharedRenderPaintTemplateCacheIfNeeded()
    {
        if (s_sharedRenderPaintTemplateCache.Count > SharedRenderPaintTemplateCacheLimit)
        {
            s_sharedRenderPaintTemplateCache.Clear();
        }
    }

    private static void TrimSharedRenderPathCacheIfNeeded()
    {
        if (s_sharedRenderPathCache.Count > SharedRenderPathCacheLimit)
        {
            s_sharedRenderPathCache.Clear();
        }
    }

    private static void AddValues<T>(ref RevisionBuilder hash, T[]? values)
    {
        if (values is null)
        {
            hash.Add(0);
            return;
        }

        hash.Add(values.Length);
        for (var i = 0; i < values.Length; i++)
        {
            hash.Add(values[i]);
        }
    }


    private static void AddBytes(ref RevisionBuilder hash, byte[]? bytes)
    {
        if (bytes is null)
        {
            hash.Add(0);
            return;
        }

        hash.Add(bytes.Length);
        for (var i = 0; i < bytes.Length; i++)
        {
            hash.Add(bytes[i]);
        }
    }

    private static void AddPoints(ref RevisionBuilder hash, IList<SKPoint>? points)
    {
        if (points is null)
        {
            hash.Add(0);
            return;
        }

        hash.Add(points.Count);
        for (var i = 0; i < points.Count; i++)
        {
            hash.Add(points[i].X);
            hash.Add(points[i].Y);
        }
    }

    private static void AddPoints(ref PathSignatureBuilder hash, IList<SKPoint>? points)
    {
        if (points is null)
        {
            hash.Add(0);
            return;
        }

        hash.Add(points.Count);
        for (var i = 0; i < points.Count; i++)
        {
            hash.Add(points[i].X);
            hash.Add(points[i].Y);
        }
    }

    private static void AddRect(ref PathSignatureBuilder hash, SKRect rect)
    {
        hash.Add(rect.Left);
        hash.Add(rect.Top);
        hash.Add(rect.Right);
        hash.Add(rect.Bottom);
    }

    private static int GetPathRevision(ShimSkiaSharp.SKPath path)
    {
        var hash = new RevisionBuilder();
        hash.Add(path.Version);

        var commands = path.Commands;
        if (commands is null)
        {
            hash.Add(0);
            return hash.ToRevision();
        }

        hash.Add(commands.Count);
        for (var i = 0; i < commands.Count; i++)
        {
            if (commands[i] is AddPolyPathCommand addPolyPathCommand)
            {
                hash.Add(i);
                hash.Add(addPolyPathCommand.Close);
                AddPoints(ref hash, addPolyPathCommand.Points);
            }
        }

        return hash.ToRevision();
    }

    private static RenderPathSignature CreateRenderPathSignature(ShimSkiaSharp.SKPath path)
    {
        var hash = new PathSignatureBuilder();
        hash.Add(path.FillType);

        var commands = path.Commands;
        if (commands is null)
        {
            return hash.ToSignature(0, path.FillType);
        }

        for (var i = 0; i < commands.Count; i++)
        {
            hash.Add(i);
            switch (commands[i])
            {
                case AddCirclePathCommand addCirclePathCommand:
                    hash.Add(1);
                    hash.Add(addCirclePathCommand.X);
                    hash.Add(addCirclePathCommand.Y);
                    hash.Add(addCirclePathCommand.Radius);
                    break;
                case AddOvalPathCommand addOvalPathCommand:
                    hash.Add(2);
                    AddRect(ref hash, addOvalPathCommand.Rect);
                    break;
                case AddPolyPathCommand addPolyPathCommand:
                    hash.Add(3);
                    hash.Add(addPolyPathCommand.Close);
                    AddPoints(ref hash, addPolyPathCommand.Points);
                    break;
                case AddRectPathCommand addRectPathCommand:
                    hash.Add(4);
                    AddRect(ref hash, addRectPathCommand.Rect);
                    break;
                case AddRoundRectPathCommand addRoundRectPathCommand:
                    hash.Add(5);
                    AddRect(ref hash, addRoundRectPathCommand.Rect);
                    hash.Add(addRoundRectPathCommand.Rx);
                    hash.Add(addRoundRectPathCommand.Ry);
                    break;
                case ArcToPathCommand arcToPathCommand:
                    hash.Add(6);
                    hash.Add(arcToPathCommand.Rx);
                    hash.Add(arcToPathCommand.Ry);
                    hash.Add(arcToPathCommand.XAxisRotate);
                    hash.Add(arcToPathCommand.LargeArc);
                    hash.Add(arcToPathCommand.Sweep);
                    hash.Add(arcToPathCommand.X);
                    hash.Add(arcToPathCommand.Y);
                    break;
                case ClosePathCommand:
                    hash.Add(7);
                    break;
                case CubicToPathCommand cubicToPathCommand:
                    hash.Add(8);
                    hash.Add(cubicToPathCommand.X0);
                    hash.Add(cubicToPathCommand.Y0);
                    hash.Add(cubicToPathCommand.X1);
                    hash.Add(cubicToPathCommand.Y1);
                    hash.Add(cubicToPathCommand.X2);
                    hash.Add(cubicToPathCommand.Y2);
                    break;
                case LineToPathCommand lineToPathCommand:
                    hash.Add(9);
                    hash.Add(lineToPathCommand.X);
                    hash.Add(lineToPathCommand.Y);
                    break;
                case MoveToPathCommand moveToPathCommand:
                    hash.Add(10);
                    hash.Add(moveToPathCommand.X);
                    hash.Add(moveToPathCommand.Y);
                    break;
                case QuadToPathCommand quadToPathCommand:
                    hash.Add(11);
                    hash.Add(quadToPathCommand.X0);
                    hash.Add(quadToPathCommand.Y0);
                    hash.Add(quadToPathCommand.X1);
                    hash.Add(quadToPathCommand.Y1);
                    break;
                default:
                    hash.Add(255);
                    hash.Add(commands[i]?.GetHashCode() ?? 0);
                    break;
            }
        }

        return hash.ToSignature(commands.Count, path.FillType);
    }

    private static RenderPathSignature GetCachedRenderPathSignature(ShimSkiaSharp.SKPath path, int revision)
    {
        if (s_sharedRenderPathSignatureCache.TryGetValue(path, out var cached) &&
            cached.Revision == revision)
        {
            return cached.Signature;
        }

        var signature = CreateRenderPathSignature(path);
        s_sharedRenderPathSignatureCache.Remove(path);
        s_sharedRenderPathSignatureCache.Add(path, new RenderPathSignatureCacheEntry(revision, signature));
        return signature;
    }

    private static bool CanUseSharedRenderPathCache(ShimSkiaSharp.SKPath path)
    {
        var commands = path.Commands;
        return commands is not [AddPolyPathCommand];
    }

    private static int GetImageRevision(ShimSkiaSharp.SKImage image)
    {
        var hash = new RevisionBuilder();
        hash.Add(image.Version);
        hash.Add(image.Width);
        hash.Add(image.Height);
        AddBytes(ref hash, image.Data);
        return hash.ToRevision();
    }

    private static HashSet<object> CreateVisitedSet()
    {
        return new HashSet<object>(ObjectReferenceEqualityComparer<object>.Instance);
    }

    private static bool TryGetShaderRevision(ShimSkiaSharp.SKShader? shader, HashSet<object> visited, out int revision)
    {
        if (shader is null)
        {
            revision = 0;
            return true;
        }

        if (!visited.Add(shader))
        {
            revision = 0;
            return false;
        }

        try
        {
            var hash = new RevisionBuilder();
            hash.Add(shader.GetType());

            switch (shader)
            {
                case ColorShader colorShader:
                    hash.Add(colorShader.Color);
                    hash.Add(colorShader.ColorSpace);
                    break;
                case LinearGradientShader linearGradientShader:
                    hash.Add(linearGradientShader.Start);
                    hash.Add(linearGradientShader.End);
                    hash.Add(linearGradientShader.ColorSpace);
                    hash.Add(linearGradientShader.Mode);
                    hash.Add(linearGradientShader.LocalMatrix);
                    AddValues(ref hash, linearGradientShader.Colors);
                    AddValues(ref hash, linearGradientShader.ColorPos);
                    break;
                case PerlinNoiseFractalNoiseShader perlinNoiseFractalNoiseShader:
                    hash.Add(perlinNoiseFractalNoiseShader.BaseFrequencyX);
                    hash.Add(perlinNoiseFractalNoiseShader.BaseFrequencyY);
                    hash.Add(perlinNoiseFractalNoiseShader.NumOctaves);
                    hash.Add(perlinNoiseFractalNoiseShader.Seed);
                    hash.Add(perlinNoiseFractalNoiseShader.TileSize);
                    break;
                case PerlinNoiseTurbulenceShader perlinNoiseTurbulenceShader:
                    hash.Add(perlinNoiseTurbulenceShader.BaseFrequencyX);
                    hash.Add(perlinNoiseTurbulenceShader.BaseFrequencyY);
                    hash.Add(perlinNoiseTurbulenceShader.NumOctaves);
                    hash.Add(perlinNoiseTurbulenceShader.Seed);
                    hash.Add(perlinNoiseTurbulenceShader.TileSize);
                    break;
                case PictureShader:
                    revision = 0;
                    return false;
                case RadialGradientShader radialGradientShader:
                    hash.Add(radialGradientShader.Center);
                    hash.Add(radialGradientShader.Radius);
                    hash.Add(radialGradientShader.ColorSpace);
                    hash.Add(radialGradientShader.Mode);
                    hash.Add(radialGradientShader.LocalMatrix);
                    AddValues(ref hash, radialGradientShader.Colors);
                    AddValues(ref hash, radialGradientShader.ColorPos);
                    break;
                case TwoPointConicalGradientShader twoPointConicalGradientShader:
                    hash.Add(twoPointConicalGradientShader.Start);
                    hash.Add(twoPointConicalGradientShader.StartRadius);
                    hash.Add(twoPointConicalGradientShader.End);
                    hash.Add(twoPointConicalGradientShader.EndRadius);
                    hash.Add(twoPointConicalGradientShader.ColorSpace);
                    hash.Add(twoPointConicalGradientShader.Mode);
                    hash.Add(twoPointConicalGradientShader.LocalMatrix);
                    AddValues(ref hash, twoPointConicalGradientShader.Colors);
                    AddValues(ref hash, twoPointConicalGradientShader.ColorPos);
                    break;
                default:
                    revision = 0;
                    return false;
            }

            revision = hash.ToRevision();
            return true;
        }
        finally
        {
            visited.Remove(shader);
        }
    }

    private static bool TryGetColorFilterRevision(ShimSkiaSharp.SKColorFilter? colorFilter, HashSet<object> visited, out int revision)
    {
        if (colorFilter is null)
        {
            revision = 0;
            return true;
        }

        if (!visited.Add(colorFilter))
        {
            revision = 0;
            return false;
        }

        try
        {
            var hash = new RevisionBuilder();
            hash.Add(colorFilter.GetType());

            switch (colorFilter)
            {
                case BlendModeColorFilter blendModeColorFilter:
                    hash.Add(blendModeColorFilter.Color);
                    hash.Add(blendModeColorFilter.Mode);
                    break;
                case ColorMatrixColorFilter colorMatrixColorFilter:
                    AddValues(ref hash, colorMatrixColorFilter.Matrix);
                    break;
                case LumaColorColorFilter:
                    break;
                case TableColorFilter tableColorFilter:
                    AddValues(ref hash, tableColorFilter.TableA);
                    AddValues(ref hash, tableColorFilter.TableR);
                    AddValues(ref hash, tableColorFilter.TableG);
                    AddValues(ref hash, tableColorFilter.TableB);
                    break;
                default:
                    revision = 0;
                    return false;
            }

            revision = hash.ToRevision();
            return true;
        }
        finally
        {
            visited.Remove(colorFilter);
        }
    }

    private static bool TryGetPathEffectRevision(ShimSkiaSharp.SKPathEffect? pathEffect, HashSet<object> visited, out int revision)
    {
        if (pathEffect is null)
        {
            revision = 0;
            return true;
        }

        if (!visited.Add(pathEffect))
        {
            revision = 0;
            return false;
        }

        try
        {
            var hash = new RevisionBuilder();
            hash.Add(pathEffect.GetType());

            switch (pathEffect)
            {
                case DashPathEffect dashPathEffect:
                    AddValues(ref hash, dashPathEffect.Intervals);
                    hash.Add(dashPathEffect.Phase);
                    break;
                default:
                    revision = 0;
                    return false;
            }

            revision = hash.ToRevision();
            return true;
        }
        finally
        {
            visited.Remove(pathEffect);
        }
    }

    private static bool TryGetPaintRevision(ShimSkiaSharp.SKPaint paint, HashSet<object> visited, out int revision)
    {
        if (!visited.Add(paint))
        {
            revision = 0;
            return false;
        }

        try
        {
            var hash = new RevisionBuilder();
            hash.Add(paint.Version);

            if (!TryGetShaderRevision(paint.Shader, visited, out var shaderRevision) ||
                !TryGetColorFilterRevision(paint.ColorFilter, visited, out var colorFilterRevision) ||
                !TryGetImageFilterRevision(paint.ImageFilter, visited, out var imageFilterRevision) ||
                !TryGetPathEffectRevision(paint.PathEffect, visited, out var pathEffectRevision))
            {
                revision = 0;
                return false;
            }

            hash.Add(shaderRevision);
            hash.Add(colorFilterRevision);
            hash.Add(imageFilterRevision);
            hash.Add(pathEffectRevision);
            revision = hash.ToRevision();
            return true;
        }
        finally
        {
            visited.Remove(paint);
        }
    }

    private static bool TryGetImageFilterRevision(ShimSkiaSharp.SKImageFilter? imageFilter, HashSet<object> visited, out int revision)
    {
        if (imageFilter is null)
        {
            revision = 0;
            return true;
        }

        if (!visited.Add(imageFilter))
        {
            revision = 0;
            return false;
        }

        try
        {
            var hash = new RevisionBuilder();
            hash.Add(imageFilter.GetType());

            switch (imageFilter)
            {
                case ArithmeticImageFilter arithmeticImageFilter:
                    hash.Add(arithmeticImageFilter.K1);
                    hash.Add(arithmeticImageFilter.K2);
                    hash.Add(arithmeticImageFilter.K3);
                    hash.Add(arithmeticImageFilter.K4);
                    hash.Add(arithmeticImageFilter.EforcePMColor);
                    hash.Add(arithmeticImageFilter.Clip);
                    if (!TryGetImageFilterRevision(arithmeticImageFilter.Background, visited, out var arithmeticBackgroundRevision) ||
                        !TryGetImageFilterRevision(arithmeticImageFilter.Foreground, visited, out var arithmeticForegroundRevision))
                    {
                        revision = 0;
                        return false;
                    }
                    hash.Add(arithmeticBackgroundRevision);
                    hash.Add(arithmeticForegroundRevision);
                    break;
                case BlendModeImageFilter blendModeImageFilter:
                    hash.Add(blendModeImageFilter.Mode);
                    hash.Add(blendModeImageFilter.Clip);
                    if (!TryGetImageFilterRevision(blendModeImageFilter.Background, visited, out var blendBackgroundRevision) ||
                        !TryGetImageFilterRevision(blendModeImageFilter.Foreground, visited, out var blendForegroundRevision))
                    {
                        revision = 0;
                        return false;
                    }
                    hash.Add(blendBackgroundRevision);
                    hash.Add(blendForegroundRevision);
                    break;
                case BlurImageFilter blurImageFilter:
                    hash.Add(blurImageFilter.SigmaX);
                    hash.Add(blurImageFilter.SigmaY);
                    hash.Add(blurImageFilter.Clip);
                    if (!TryGetImageFilterRevision(blurImageFilter.Input, visited, out var blurInputRevision))
                    {
                        revision = 0;
                        return false;
                    }
                    hash.Add(blurInputRevision);
                    break;
                case ColorFilterImageFilter colorFilterImageFilter:
                    hash.Add(colorFilterImageFilter.Clip);
                    if (!TryGetColorFilterRevision(colorFilterImageFilter.ColorFilter, visited, out var imageColorFilterRevision) ||
                        !TryGetImageFilterRevision(colorFilterImageFilter.Input, visited, out var colorFilterInputRevision))
                    {
                        revision = 0;
                        return false;
                    }
                    hash.Add(imageColorFilterRevision);
                    hash.Add(colorFilterInputRevision);
                    break;
                case DilateImageFilter dilateImageFilter:
                    hash.Add(dilateImageFilter.RadiusX);
                    hash.Add(dilateImageFilter.RadiusY);
                    hash.Add(dilateImageFilter.Clip);
                    if (!TryGetImageFilterRevision(dilateImageFilter.Input, visited, out var dilateInputRevision))
                    {
                        revision = 0;
                        return false;
                    }
                    hash.Add(dilateInputRevision);
                    break;
                case DisplacementMapEffectImageFilter displacementMapEffectImageFilter:
                    hash.Add(displacementMapEffectImageFilter.XChannelSelector);
                    hash.Add(displacementMapEffectImageFilter.YChannelSelector);
                    hash.Add(displacementMapEffectImageFilter.Scale);
                    hash.Add(displacementMapEffectImageFilter.Clip);
                    if (!TryGetImageFilterRevision(displacementMapEffectImageFilter.Displacement, visited, out var displacementRevision) ||
                        !TryGetImageFilterRevision(displacementMapEffectImageFilter.Input, visited, out var displacementInputRevision))
                    {
                        revision = 0;
                        return false;
                    }
                    hash.Add(displacementRevision);
                    hash.Add(displacementInputRevision);
                    break;
                case DistantLitDiffuseImageFilter distantLitDiffuseImageFilter:
                    hash.Add(distantLitDiffuseImageFilter.Direction);
                    hash.Add(distantLitDiffuseImageFilter.LightColor);
                    hash.Add(distantLitDiffuseImageFilter.SurfaceScale);
                    hash.Add(distantLitDiffuseImageFilter.Kd);
                    hash.Add(distantLitDiffuseImageFilter.Clip);
                    if (!TryGetImageFilterRevision(distantLitDiffuseImageFilter.Input, visited, out var distantDiffuseInputRevision))
                    {
                        revision = 0;
                        return false;
                    }
                    hash.Add(distantDiffuseInputRevision);
                    break;
                case DistantLitSpecularImageFilter distantLitSpecularImageFilter:
                    hash.Add(distantLitSpecularImageFilter.Direction);
                    hash.Add(distantLitSpecularImageFilter.LightColor);
                    hash.Add(distantLitSpecularImageFilter.SurfaceScale);
                    hash.Add(distantLitSpecularImageFilter.Ks);
                    hash.Add(distantLitSpecularImageFilter.Shininess);
                    hash.Add(distantLitSpecularImageFilter.Clip);
                    if (!TryGetImageFilterRevision(distantLitSpecularImageFilter.Input, visited, out var distantSpecularInputRevision))
                    {
                        revision = 0;
                        return false;
                    }
                    hash.Add(distantSpecularInputRevision);
                    break;
                case ErodeImageFilter erodeImageFilter:
                    hash.Add(erodeImageFilter.RadiusX);
                    hash.Add(erodeImageFilter.RadiusY);
                    hash.Add(erodeImageFilter.Clip);
                    if (!TryGetImageFilterRevision(erodeImageFilter.Input, visited, out var erodeInputRevision))
                    {
                        revision = 0;
                        return false;
                    }
                    hash.Add(erodeInputRevision);
                    break;
                case ImageImageFilter imageImageFilter:
                    hash.Add(imageImageFilter.Image?.Version ?? 0);
                    hash.Add(imageImageFilter.Src);
                    hash.Add(imageImageFilter.Dst);
                    hash.Add(imageImageFilter.FilterQuality);
                    break;
                case MatrixConvolutionImageFilter matrixConvolutionImageFilter:
                    hash.Add(matrixConvolutionImageFilter.KernelSize);
                    AddValues(ref hash, matrixConvolutionImageFilter.Kernel);
                    hash.Add(matrixConvolutionImageFilter.Gain);
                    hash.Add(matrixConvolutionImageFilter.Bias);
                    hash.Add(matrixConvolutionImageFilter.KernelOffset);
                    hash.Add(matrixConvolutionImageFilter.TileMode);
                    hash.Add(matrixConvolutionImageFilter.ConvolveAlpha);
                    hash.Add(matrixConvolutionImageFilter.Clip);
                    if (!TryGetImageFilterRevision(matrixConvolutionImageFilter.Input, visited, out var matrixInputRevision))
                    {
                        revision = 0;
                        return false;
                    }
                    hash.Add(matrixInputRevision);
                    break;
                case MergeImageFilter mergeImageFilter:
                    hash.Add(mergeImageFilter.Clip);
                    if (mergeImageFilter.Filters is null)
                    {
                        hash.Add(0);
                        break;
                    }
                    hash.Add(mergeImageFilter.Filters.Length);
                    for (var i = 0; i < mergeImageFilter.Filters.Length; i++)
                    {
                        if (!TryGetImageFilterRevision(mergeImageFilter.Filters[i], visited, out var mergeFilterRevision))
                        {
                            revision = 0;
                            return false;
                        }
                        hash.Add(mergeFilterRevision);
                    }
                    break;
                case OffsetImageFilter offsetImageFilter:
                    hash.Add(offsetImageFilter.Dx);
                    hash.Add(offsetImageFilter.Dy);
                    hash.Add(offsetImageFilter.Clip);
                    if (!TryGetImageFilterRevision(offsetImageFilter.Input, visited, out var offsetInputRevision))
                    {
                        revision = 0;
                        return false;
                    }
                    hash.Add(offsetInputRevision);
                    break;
                case PaintImageFilter paintImageFilter:
                    hash.Add(paintImageFilter.Clip);
                    if (paintImageFilter.Paint is not { } nestedPaint ||
                        !TryGetPaintRevision(nestedPaint, visited, out var nestedPaintRevision))
                    {
                        revision = 0;
                        return false;
                    }
                    hash.Add(nestedPaintRevision);
                    break;
                case ShaderImageFilter shaderImageFilter:
                    hash.Add(shaderImageFilter.Dither);
                    hash.Add(shaderImageFilter.Clip);
                    if (!TryGetShaderRevision(shaderImageFilter.Shader, visited, out var shaderImageFilterRevision))
                    {
                        revision = 0;
                        return false;
                    }
                    hash.Add(shaderImageFilterRevision);
                    break;
                case PictureImageFilter:
                    revision = 0;
                    return false;
                case PointLitDiffuseImageFilter pointLitDiffuseImageFilter:
                    hash.Add(pointLitDiffuseImageFilter.Location);
                    hash.Add(pointLitDiffuseImageFilter.LightColor);
                    hash.Add(pointLitDiffuseImageFilter.SurfaceScale);
                    hash.Add(pointLitDiffuseImageFilter.Kd);
                    hash.Add(pointLitDiffuseImageFilter.Clip);
                    if (!TryGetImageFilterRevision(pointLitDiffuseImageFilter.Input, visited, out var pointDiffuseInputRevision))
                    {
                        revision = 0;
                        return false;
                    }
                    hash.Add(pointDiffuseInputRevision);
                    break;
                case PointLitSpecularImageFilter pointLitSpecularImageFilter:
                    hash.Add(pointLitSpecularImageFilter.Location);
                    hash.Add(pointLitSpecularImageFilter.LightColor);
                    hash.Add(pointLitSpecularImageFilter.SurfaceScale);
                    hash.Add(pointLitSpecularImageFilter.Ks);
                    hash.Add(pointLitSpecularImageFilter.Shininess);
                    hash.Add(pointLitSpecularImageFilter.Clip);
                    if (!TryGetImageFilterRevision(pointLitSpecularImageFilter.Input, visited, out var pointSpecularInputRevision))
                    {
                        revision = 0;
                        return false;
                    }
                    hash.Add(pointSpecularInputRevision);
                    break;
                case SpotLitDiffuseImageFilter spotLitDiffuseImageFilter:
                    hash.Add(spotLitDiffuseImageFilter.Location);
                    hash.Add(spotLitDiffuseImageFilter.Target);
                    hash.Add(spotLitDiffuseImageFilter.SpecularExponent);
                    hash.Add(spotLitDiffuseImageFilter.CutoffAngle);
                    hash.Add(spotLitDiffuseImageFilter.LightColor);
                    hash.Add(spotLitDiffuseImageFilter.SurfaceScale);
                    hash.Add(spotLitDiffuseImageFilter.Kd);
                    hash.Add(spotLitDiffuseImageFilter.Clip);
                    if (!TryGetImageFilterRevision(spotLitDiffuseImageFilter.Input, visited, out var spotDiffuseInputRevision))
                    {
                        revision = 0;
                        return false;
                    }
                    hash.Add(spotDiffuseInputRevision);
                    break;
                case SpotLitSpecularImageFilter spotLitSpecularImageFilter:
                    hash.Add(spotLitSpecularImageFilter.Location);
                    hash.Add(spotLitSpecularImageFilter.Target);
                    hash.Add(spotLitSpecularImageFilter.SpecularExponent);
                    hash.Add(spotLitSpecularImageFilter.CutoffAngle);
                    hash.Add(spotLitSpecularImageFilter.LightColor);
                    hash.Add(spotLitSpecularImageFilter.SurfaceScale);
                    hash.Add(spotLitSpecularImageFilter.Ks);
                    hash.Add(spotLitSpecularImageFilter.Shininess);
                    hash.Add(spotLitSpecularImageFilter.Clip);
                    if (!TryGetImageFilterRevision(spotLitSpecularImageFilter.Input, visited, out var spotSpecularInputRevision))
                    {
                        revision = 0;
                        return false;
                    }
                    hash.Add(spotSpecularInputRevision);
                    break;
                case TileImageFilter tileImageFilter:
                    hash.Add(tileImageFilter.Src);
                    hash.Add(tileImageFilter.Dst);
                    if (!TryGetImageFilterRevision(tileImageFilter.Input, visited, out var tileInputRevision))
                    {
                        revision = 0;
                        return false;
                    }
                    hash.Add(tileInputRevision);
                    break;
                default:
                    revision = 0;
                    return false;
            }

            revision = hash.ToRevision();
            return true;
        }
        finally
        {
            visited.Remove(imageFilter);
        }
    }

    private SkiaSharp.SKPaint? CreateRenderPaint(ShimSkiaSharp.SKPaint paint)
    {
        var style = ToSKPaintStyle(paint.Style);
        var strokeCap = ToSKStrokeCap(paint.StrokeCap);
        var strokeJoin = ToSKStrokeJoin(paint.StrokeJoin);
        var textAlign = ToSKTextAlign(paint.TextAlign);
        var typeface = ToSKTypeface(paint.Typeface);
        var textEncoding = ToSKTextEncoding(paint.TextEncoding);
        var color = paint.Color is null
            ? SkiaSharp.SKColor.Empty
            : ToSKColor(paint.Color.Value);
        var shader = GetRenderShader(paint.Shader);
        var colorFilter = GetRenderColorFilter(paint.ColorFilter);
        var imageFilter = GetRenderImageFilter(paint.ImageFilter);
        var pathEffect = GetRenderPathEffect(paint.PathEffect);
        var blendMode = ToSKBlendMode(paint.BlendMode);
        var filterQuality = ToSKFilterQuality(paint.FilterQuality);

        var skPaint = new SkiaSharp.SKPaint
        {
            Style = style,
            IsAntialias = paint.IsAntialias,
            StrokeWidth = paint.StrokeWidth,
            StrokeCap = strokeCap,
            StrokeJoin = strokeJoin,
            StrokeMiter = paint.StrokeMiter,
            TextSize = paint.TextSize,
            TextAlign = textAlign,
            Typeface = typeface,
            LcdRenderText = paint.LcdRenderText,
            SubpixelText = paint.SubpixelText,
            TextEncoding = textEncoding,
            Color = color,
            Shader = shader,
            ColorFilter = colorFilter,
            ImageFilter = imageFilter,
            PathEffect = pathEffect,
            BlendMode = blendMode,
            FilterQuality = filterQuality
        };

        ApplyTypefaceAdjustments(paint, skPaint);
        return skPaint;
    }

    internal SkiaSharp.SKPaint? GetRenderPaint(ShimSkiaSharp.SKPaint? paint)
    {
        if (paint is null)
        {
            return null;
        }

        if (!CanCacheRenderPaint(paint))
        {
            return CreateRenderPaint(paint);
        }

        var signature = new RenderPaintSignature(paint);
        lock (GetNativeObjectCacheLock())
        {
            var nativePaintCache = GetNativePaintCache();
            if (nativePaintCache.TryGetValue(paint, out var cached) &&
                cached.Version == paint.Version &&
                cached.Paint.Handle != IntPtr.Zero)
            {
                return cached.Paint;
            }

            SkiaSharp.SKPaint? created;
            if (CanUseSharedRenderPaintTemplates(paint) &&
                s_sharedRenderPaintTemplateCache.TryGetValue(signature, out var sharedTemplate))
            {
                created = CreateRenderPaint(sharedTemplate);
            }
            else
            {
                created = CreateRenderPaint(paint);
                if (created is not null && CanUseSharedRenderPaintTemplates(paint))
                {
                    s_sharedRenderPaintTemplateCache[signature] = CreateSharedRenderPaintTemplate(created);
                    TrimSharedRenderPaintTemplateCacheIfNeeded();
                }
            }

            if (created is null)
            {
                return null;
            }

            nativePaintCache.Remove(paint);
            nativePaintCache.Add(paint, new NativePaintCacheEntry(paint.Version, created));
            return created;
        }
    }

    internal SkiaSharp.SKShader? GetRenderShader(ShimSkiaSharp.SKShader? shader)
    {
        if (shader is null)
        {
            return null;
        }

        if (!TryGetShaderRevision(shader, CreateVisitedSet(), out var revision))
        {
            return ToSKShader(shader);
        }

        lock (GetNativeObjectCacheLock())
        {
            var nativeShaderCache = GetNativeShaderCache();
            if (nativeShaderCache.TryGetValue(shader, out var cached) &&
                cached.Revision == revision &&
                cached.Shader.Handle != IntPtr.Zero)
            {
                return cached.Shader;
            }

            var created = ToSKShader(shader);
            if (created is null)
            {
                return null;
            }

            nativeShaderCache.Remove(shader);
            nativeShaderCache.Add(shader, new NativeShaderCacheEntry(revision, created));
            return created;
        }
    }

    internal SkiaSharp.SKColorFilter? GetRenderColorFilter(ShimSkiaSharp.SKColorFilter? colorFilter)
    {
        if (colorFilter is null)
        {
            return null;
        }

        if (!TryGetColorFilterRevision(colorFilter, CreateVisitedSet(), out var revision))
        {
            return ToSKColorFilter(colorFilter);
        }

        lock (GetNativeObjectCacheLock())
        {
            var nativeColorFilterCache = GetNativeColorFilterCache();
            if (nativeColorFilterCache.TryGetValue(colorFilter, out var cached) &&
                cached.Revision == revision &&
                cached.ColorFilter.Handle != IntPtr.Zero)
            {
                return cached.ColorFilter;
            }

            var created = ToSKColorFilter(colorFilter);
            if (created is null)
            {
                return null;
            }

            nativeColorFilterCache.Remove(colorFilter);
            nativeColorFilterCache.Add(colorFilter, new NativeColorFilterCacheEntry(revision, created));
            return created;
        }
    }

    internal SkiaSharp.SKPathEffect? GetRenderPathEffect(ShimSkiaSharp.SKPathEffect? pathEffect)
    {
        if (pathEffect is null)
        {
            return null;
        }

        if (!TryGetPathEffectRevision(pathEffect, CreateVisitedSet(), out var revision))
        {
            return ToSKPathEffect(pathEffect);
        }

        lock (GetNativeObjectCacheLock())
        {
            var nativePathEffectCache = GetNativePathEffectCache();
            if (nativePathEffectCache.TryGetValue(pathEffect, out var cached) &&
                cached.Revision == revision &&
                cached.PathEffect.Handle != IntPtr.Zero)
            {
                return cached.PathEffect;
            }
        }

        lock (GetNativeObjectCacheLock())
        {
            var nativePathEffectCache = GetNativePathEffectCache();
            if (nativePathEffectCache.TryGetValue(pathEffect, out var cached) &&
                cached.Revision == revision &&
                cached.PathEffect.Handle != IntPtr.Zero)
            {
                return cached.PathEffect;
            }

            var created = ToSKPathEffect(pathEffect);
            if (created is null)
            {
                return null;
            }

            nativePathEffectCache.Remove(pathEffect);
            nativePathEffectCache.Add(pathEffect, new NativePathEffectCacheEntry(revision, created));
            return created;
        }
    }

    internal SkiaSharp.SKImageFilter? GetRenderImageFilter(ShimSkiaSharp.SKImageFilter? imageFilter)
    {
        if (imageFilter is null)
        {
            return null;
        }

        if (!TryGetImageFilterRevision(imageFilter, CreateVisitedSet(), out var revision))
        {
            return ToSKImageFilter(imageFilter);
        }

        lock (GetNativeObjectCacheLock())
        {
            var nativeImageFilterCache = GetNativeImageFilterCache();
            if (nativeImageFilterCache.TryGetValue(imageFilter, out var cached) &&
                cached.Revision == revision &&
                cached.ImageFilter.Handle != IntPtr.Zero)
            {
                return cached.ImageFilter;
            }

            var created = ToSKImageFilter(imageFilter);
            if (created is null)
            {
                return null;
            }

            nativeImageFilterCache.Remove(imageFilter);
            nativeImageFilterCache.Add(imageFilter, new NativeImageFilterCacheEntry(revision, created));
            return created;
        }
    }

    internal SkiaSharp.SKPath? GetRenderPath(ShimSkiaSharp.SKPath? path)
    {
        if (path is null)
        {
            return null;
        }

        var revision = GetPathRevision(path);

        lock (GetNativeObjectCacheLock())
        {
            var nativePathCache = GetNativePathCache();
            if (nativePathCache.TryGetValue(path, out var cached) &&
                cached.Revision == revision &&
                cached.Path.Handle != IntPtr.Zero)
            {
                return cached.Path;
            }

            if (CanUseSharedRenderPathCache(path))
            {
                var signature = GetCachedRenderPathSignature(path, revision);
                if (s_sharedRenderPathCache.TryGetValue(signature, out var sharedPath) &&
                    sharedPath.Handle != IntPtr.Zero)
                {
                    nativePathCache.Remove(path);
                    nativePathCache.Add(path, new NativePathCacheEntry(revision, sharedPath));
                    return sharedPath;
                }

                var sharedCreated = ToSKPath(path);
                s_sharedRenderPathCache[signature] = sharedCreated;
                TrimSharedRenderPathCacheIfNeeded();
                nativePathCache.Remove(path);
                nativePathCache.Add(path, new NativePathCacheEntry(revision, sharedCreated));
                return sharedCreated;
            }

            var created = ToSKPath(path);
            nativePathCache.Remove(path);
            nativePathCache.Add(path, new NativePathCacheEntry(revision, created));
            return created;
        }
    }

    internal SkiaSharp.SKImage? GetRenderImage(ShimSkiaSharp.SKImage? image)
    {
        if (image is null)
        {
            return null;
        }

        var revision = GetImageRevision(image);

        lock (GetNativeObjectCacheLock())
        {
            var nativeImageCache = GetNativeImageCache();
            if (nativeImageCache.TryGetValue(image, out var cached) &&
                cached.Revision == revision &&
                cached.Image.Handle != IntPtr.Zero)
            {
                return cached.Image;
            }

            var created = ToSKImage(image);
            nativeImageCache.Remove(image);
            nativeImageCache.Add(image, new NativeImageCacheEntry(revision, created));
            return created;
        }
    }

    internal void ClearReusableRenderCaches()
    {
        var nativeObjectCacheLock = _nativeObjectCacheLock;
        if (nativeObjectCacheLock is null)
        {
            return;
        }

        lock (nativeObjectCacheLock)
        {
            _nativePaintCache = null;
            _nativePathCache = null;
            _nativeImageCache = null;
            _nativeShaderCache = null;
            _nativeColorFilterCache = null;
            _nativePathEffectCache = null;
            _nativeImageFilterCache = null;
        }
    }

    internal void RegisterCachedPicture(ShimSkiaSharp.SKPicture picture, SkiaSharp.SKPicture skPicture)
    {
        lock (GetPictureCacheLock())
        {
            GetPictureCache()[picture] = skPicture;
        }
    }

    internal void UnregisterCachedPicture(ShimSkiaSharp.SKPicture? picture)
    {
        if (picture is null)
        {
            return;
        }

        lock (GetPictureCacheLock())
        {
            _ = GetPictureCache().Remove(picture);
        }
    }

    internal bool TryGetCachedPicture(ShimSkiaSharp.SKPicture picture, out SkiaSharp.SKPicture skPicture)
    {
        lock (GetPictureCacheLock())
        {
            return GetPictureCache().TryGetValue(picture, out skPicture!);
        }
    }

    internal void ClearCachedPictures()
    {
        var pictureCacheLock = _pictureCacheLock;
        if (pictureCacheLock is null)
        {
            return;
        }

        lock (pictureCacheLock)
        {
            _pictureCache?.Clear();
        }
    }
}
