// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ShimSkiaSharp;
using Svg.Skia.TypefaceProviders;

namespace Svg.Skia;

public partial class SkiaModel
{
    private const int TypefaceCacheLimit = 512;
    private const int ResolvedTypefaceCacheLimit = 512;
    private const int NativePathValueCacheLimit = 128;
    private const int ShapedTextLayoutCacheLimit = 1024;
    private const int ShapedTextLayoutCacheMaxTextLength = 8;
    private const int PositionedTextCacheRefTrimThreshold = 1024;
    private const int RevisionVisitedSetRetainLimit = 256;

    [ThreadStatic]
    private static HashSet<object>? s_revisionVisitedSet;

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
            _hash = unchecked((_hash * 397) ^ EqualityComparer<T>.Default.GetHashCode(value!));
        }

        public int ToRevision()
        {
            return _hash;
        }
    }

    private readonly struct RevisionVisitedSetLease : IDisposable
    {
        public RevisionVisitedSetLease(HashSet<object> visited)
        {
            Visited = visited;
        }

        public HashSet<object> Visited { get; }

        public void Dispose()
        {
            if (Visited.Count > RevisionVisitedSetRetainLimit)
            {
                Visited.Clear();
                return;
            }

            Visited.Clear();
            s_revisionVisitedSet = Visited;
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

    private readonly struct TypefaceResolution
    {
        public TypefaceResolution(SkiaSharp.SKTypeface typeface, bool suppressSyntheticBold)
        {
            Typeface = typeface;
            SuppressSyntheticBold = suppressSyntheticBold;
        }

        public SkiaSharp.SKTypeface Typeface { get; }
        public bool SuppressSyntheticBold { get; }
    }

    private readonly struct FontSignature : IEquatable<FontSignature>
    {
        public FontSignature(SkiaSharp.SKFont font)
        {
            TypefaceHandle = font.Typeface?.Handle ?? IntPtr.Zero;
            Size = font.Size;
            ScaleX = font.ScaleX;
            SkewX = font.SkewX;
            Edging = font.Edging;
            Subpixel = font.Subpixel;
            Embolden = font.Embolden;
        }

        public IntPtr TypefaceHandle { get; }
        public float Size { get; }
        public float ScaleX { get; }
        public float SkewX { get; }
        public SkiaSharp.SKFontEdging Edging { get; }
        public bool Subpixel { get; }
        public bool Embolden { get; }

        public bool Equals(FontSignature other)
        {
            return TypefaceHandle == other.TypefaceHandle
                && Size.Equals(other.Size)
                && ScaleX.Equals(other.ScaleX)
                && SkewX.Equals(other.SkewX)
                && Edging == other.Edging
                && Subpixel == other.Subpixel
                && Embolden == other.Embolden;
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
                hash = (hash * 397) ^ Size.GetHashCode();
                hash = (hash * 397) ^ ScaleX.GetHashCode();
                hash = (hash * 397) ^ SkewX.GetHashCode();
                hash = (hash * 397) ^ Edging.GetHashCode();
                hash = (hash * 397) ^ Subpixel.GetHashCode();
                hash = (hash * 397) ^ Embolden.GetHashCode();
                return hash;
            }
        }
    }

    private readonly struct SmallPolyPathCacheKey : IEquatable<SmallPolyPathCacheKey>
    {
        public const int MaxPointCount = 4;

        public SmallPolyPathCacheKey(SKPathFillType fillType, bool close, IList<SKPoint> points)
        {
            FillType = fillType;
            Close = close;
            Count = points.Count;
            X0 = points[0].X;
            Y0 = points[0].Y;
            X1 = points[1].X;
            Y1 = points[1].Y;
            X2 = points[2].X;
            Y2 = points[2].Y;
            X3 = Count > 3 ? points[3].X : 0f;
            Y3 = Count > 3 ? points[3].Y : 0f;
        }

        public SKPathFillType FillType { get; }
        public bool Close { get; }
        public int Count { get; }
        public float X0 { get; }
        public float Y0 { get; }
        public float X1 { get; }
        public float Y1 { get; }
        public float X2 { get; }
        public float Y2 { get; }
        public float X3 { get; }
        public float Y3 { get; }

        public bool Equals(SmallPolyPathCacheKey other)
        {
            return FillType == other.FillType
                && Close == other.Close
                && Count == other.Count
                && X0.Equals(other.X0)
                && Y0.Equals(other.Y0)
                && X1.Equals(other.X1)
                && Y1.Equals(other.Y1)
                && X2.Equals(other.X2)
                && Y2.Equals(other.Y2)
                && X3.Equals(other.X3)
                && Y3.Equals(other.Y3);
        }

        public override bool Equals(object? obj)
        {
            return obj is SmallPolyPathCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)FillType;
                hash = (hash * 397) ^ Close.GetHashCode();
                hash = (hash * 397) ^ Count;
                hash = (hash * 397) ^ X0.GetHashCode();
                hash = (hash * 397) ^ Y0.GetHashCode();
                hash = (hash * 397) ^ X1.GetHashCode();
                hash = (hash * 397) ^ Y1.GetHashCode();
                hash = (hash * 397) ^ X2.GetHashCode();
                hash = (hash * 397) ^ Y2.GetHashCode();
                hash = (hash * 397) ^ X3.GetHashCode();
                hash = (hash * 397) ^ Y3.GetHashCode();
                return hash;
            }
        }
    }

    private sealed class PositionedTextCache
    {
        public PositionedTextCache(FontSignature signature, SkiaSharp.SKTextBlob textBlob)
        {
            Signature = signature;
            TextBlob = textBlob;
        }

        public FontSignature Signature { get; }
        public SkiaSharp.SKTextBlob TextBlob { get; }
    }

    private readonly struct ShapedTextSignature : IEquatable<ShapedTextSignature>
    {
        public ShapedTextSignature(
            FontSignature font,
            string? fontFeatureSettings,
            string? fontKerning,
            string? fontVariantLigatures)
        {
            Font = font;
            FontFeatureSettings = fontFeatureSettings;
            FontKerning = fontKerning;
            FontVariantLigatures = fontVariantLigatures;
        }

        public FontSignature Font { get; }
        public string? FontFeatureSettings { get; }
        public string? FontKerning { get; }
        public string? FontVariantLigatures { get; }

        public bool Equals(ShapedTextSignature other)
        {
            return Font.Equals(other.Font) &&
                   string.Equals(FontFeatureSettings, other.FontFeatureSettings, StringComparison.Ordinal) &&
                   string.Equals(FontKerning, other.FontKerning, StringComparison.Ordinal) &&
                   string.Equals(FontVariantLigatures, other.FontVariantLigatures, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is ShapedTextSignature other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Font.GetHashCode();
                hash = (hash * 397) ^ (FontFeatureSettings?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (FontKerning?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (FontVariantLigatures?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }

    private sealed class ShapedTextCache
    {
        public ShapedTextCache(ShapedTextSignature signature, SkiaSharp.SKTextBlob textBlob, float width)
        {
            Signature = signature;
            TextBlob = textBlob;
            Width = width;
        }

        public ShapedTextSignature Signature { get; }
        public SkiaSharp.SKTextBlob TextBlob { get; }
        public float Width { get; }
    }

    private readonly struct ShapedTextLayoutCacheKey : IEquatable<ShapedTextLayoutCacheKey>
    {
        public ShapedTextLayoutCacheKey(string text, ShapedTextSignature signature)
        {
            Text = text;
            Signature = signature;
        }

        public string Text { get; }
        public ShapedTextSignature Signature { get; }

        public bool Equals(ShapedTextLayoutCacheKey other)
        {
            return string.Equals(Text, other.Text, StringComparison.Ordinal) &&
                   Signature.Equals(other.Signature);
        }

        public override bool Equals(object? obj)
        {
            return obj is ShapedTextLayoutCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(Text) * 397) ^ Signature.GetHashCode();
            }
        }
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

    private sealed class NativePictureCacheEntry
    {
        public NativePictureCacheEntry(int revision, SkiaSharp.SKPicture picture)
        {
            Revision = revision;
            Picture = picture;
        }

        public int Revision { get; }
        public SkiaSharp.SKPicture Picture { get; }
    }

    private readonly ConcurrentDictionary<TypefaceKey, TypefaceResolution> _typefaceCache = new();
    private readonly ConcurrentDictionary<TypefaceKey, SkiaSharp.SKTypeface?> _resolvedTypefaceCache = new();
    private readonly object _positionedTextCacheLock = new();
    private readonly object _pictureCacheLock = new();
    private readonly object _nativeObjectCacheLock = new();
    private ConditionalWeakTable<DrawTextBlobCanvasCommand, PositionedTextCache> _positionedTextCache = new();
    private ConditionalWeakTable<DrawTextCanvasCommand, ShapedTextCache>? _shapedTextCache;
    private readonly Dictionary<ShapedTextLayoutCacheKey, ShapedTextResult> _shapedTextLayoutCache = new();
    private ConditionalWeakTable<ShimSkiaSharp.SKPaint, NativePaintCacheEntry> _nativePaintCache = new();
    private ConditionalWeakTable<ShimSkiaSharp.SKPath, NativePathCacheEntry> _nativePathCache = new();
    private ConditionalWeakTable<ShimSkiaSharp.SKImage, NativeImageCacheEntry> _nativeImageCache = new();
    private ConditionalWeakTable<ShimSkiaSharp.SKShader, NativeShaderCacheEntry> _nativeShaderCache = new();
    private ConditionalWeakTable<ShimSkiaSharp.SKColorFilter, NativeColorFilterCacheEntry> _nativeColorFilterCache = new();
    private ConditionalWeakTable<ShimSkiaSharp.SKPathEffect, NativePathEffectCacheEntry> _nativePathEffectCache = new();
    private ConditionalWeakTable<ShimSkiaSharp.SKImageFilter, NativeImageFilterCacheEntry> _nativeImageFilterCache = new();
    private ConditionalWeakTable<ShimSkiaSharp.SKPicture, NativePictureCacheEntry> _nativePictureCache = new();
    private readonly List<WeakReference<SkiaSharp.SKTextBlob>> _positionedTextCacheRefs = new();
    private readonly Dictionary<SmallPolyPathCacheKey, SkiaSharp.SKPath> _smallPolyNativePathCache = new();
    private readonly Dictionary<ShimSkiaSharp.SKPicture, SkiaSharp.SKPicture> _pictureCache = new(PictureReferenceEqualityComparer.Instance);
    private IList<ITypefaceProvider>? _providerStateList;
    private int _providerStateHash;
    private ShimSkiaSharp.SKPicture? _lastConvertedPicture;
    private ShimSkiaSharp.SKPicture? _previousConvertedPicture;
    private bool _cacheShapedTextBlobsForCurrentPicture;
    private bool _cacheComplexRenderPaintsForCurrentPicture;

    private static bool CanCacheRenderPaint(ShimSkiaSharp.SKPaint paint)
    {
        return paint.Shader is null
            && paint.ColorFilter is null
            && paint.ImageFilter is null
            && paint.PathEffect is null;
    }

    private static bool CanCacheShapedTextLayout(string text)
    {
        return text.Length <= ShapedTextLayoutCacheMaxTextLength;
    }

    private bool TryGetCachedShapedTextLayout(
        string text,
        ShapedTextSignature signature,
        out ShapedTextResult result)
    {
        result = default;
        if (!CanCacheShapedTextLayout(text))
        {
            return false;
        }

        var key = new ShapedTextLayoutCacheKey(text, signature);
        lock (_positionedTextCacheLock)
        {
            if (!_shapedTextLayoutCache.TryGetValue(key, out var cached))
            {
                return false;
            }

            result = cached;
            return true;
        }
    }

    private void CacheShapedTextLayout(
        string text,
        ShapedTextSignature signature,
        ShapedTextResult result)
    {
        if (!CanCacheShapedTextLayout(text) || result.Codepoints.Length == 0)
        {
            return;
        }

        var key = new ShapedTextLayoutCacheKey(text, signature);
        lock (_positionedTextCacheLock)
        {
            if (_shapedTextLayoutCache.ContainsKey(key))
            {
                _ = _shapedTextLayoutCache.Remove(key);
            }
            else if (_shapedTextLayoutCache.Count >= ShapedTextLayoutCacheLimit)
            {
                _shapedTextLayoutCache.Clear();
            }

            _shapedTextLayoutCache[key] = result;
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
            hash.Add(commands[i].GetType());

            switch (commands[i])
            {
                case AddCirclePathCommand addCirclePathCommand:
                    hash.Add(addCirclePathCommand.X);
                    hash.Add(addCirclePathCommand.Y);
                    hash.Add(addCirclePathCommand.Radius);
                    break;
                case AddOvalPathCommand addOvalPathCommand:
                    hash.Add(addOvalPathCommand.Rect);
                    break;
                case AddPolyPathCommand addPolyPathCommand:
                    hash.Add(addPolyPathCommand.Close);
                    AddPoints(ref hash, addPolyPathCommand.Points);
                    break;
                case AddRectPathCommand addRectPathCommand:
                    hash.Add(addRectPathCommand.Rect);
                    break;
                case AddRoundRectPathCommand addRoundRectPathCommand:
                    hash.Add(addRoundRectPathCommand.Rect);
                    hash.Add(addRoundRectPathCommand.Rx);
                    hash.Add(addRoundRectPathCommand.Ry);
                    break;
                case ArcToPathCommand arcToPathCommand:
                    hash.Add(arcToPathCommand.Rx);
                    hash.Add(arcToPathCommand.Ry);
                    hash.Add(arcToPathCommand.XAxisRotate);
                    hash.Add(arcToPathCommand.LargeArc);
                    hash.Add(arcToPathCommand.Sweep);
                    hash.Add(arcToPathCommand.X);
                    hash.Add(arcToPathCommand.Y);
                    break;
                case ClosePathCommand:
                    break;
                case CubicToPathCommand cubicToPathCommand:
                    hash.Add(cubicToPathCommand.X0);
                    hash.Add(cubicToPathCommand.Y0);
                    hash.Add(cubicToPathCommand.X1);
                    hash.Add(cubicToPathCommand.Y1);
                    hash.Add(cubicToPathCommand.X2);
                    hash.Add(cubicToPathCommand.Y2);
                    break;
                case LineToPathCommand lineToPathCommand:
                    hash.Add(lineToPathCommand.X);
                    hash.Add(lineToPathCommand.Y);
                    break;
                case MoveToPathCommand moveToPathCommand:
                    hash.Add(moveToPathCommand.X);
                    hash.Add(moveToPathCommand.Y);
                    break;
                case QuadToPathCommand quadToPathCommand:
                    hash.Add(quadToPathCommand.X0);
                    hash.Add(quadToPathCommand.Y0);
                    hash.Add(quadToPathCommand.X1);
                    hash.Add(quadToPathCommand.Y1);
                    break;
            }
        }

        return hash.ToRevision();
    }

    private static int GetRenderPathCacheRevision(ShimSkiaSharp.SKPath path)
    {
        var commands = path.Commands;
        if (commands is null)
        {
            return path.Version;
        }

        for (var i = 0; i < commands.Count; i++)
        {
            if (commands[i] is AddPolyPathCommand)
            {
                return GetPathRevision(path);
            }
        }

        return path.Version;
    }

    private static bool TryCreateSmallPolyPathCacheKey(ShimSkiaSharp.SKPath path, out SmallPolyPathCacheKey key)
    {
        key = default;

        if (path.Commands is not { Count: 1 } commands ||
            commands[0] is not AddPolyPathCommand addPoly ||
            addPoly.Points is not { } points ||
            points.Count < 3 ||
            points.Count > SmallPolyPathCacheKey.MaxPointCount)
        {
            return false;
        }

        key = new SmallPolyPathCacheKey(path.FillType, addPoly.Close, points);
        return true;
    }

    private void CacheSmallPolyRenderPath(SmallPolyPathCacheKey key, SkiaSharp.SKPath path)
    {
        if (_smallPolyNativePathCache.Count >= NativePathValueCacheLimit)
        {
            _smallPolyNativePathCache.Clear();
        }

        _smallPolyNativePathCache[key] = path;
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

    private static int GetTypefaceRevision(ShimSkiaSharp.SKTypeface? typeface)
    {
        if (typeface is null)
        {
            return 0;
        }

        var hash = new RevisionBuilder();
        hash.Add(typeface.FamilyName);
        hash.Add(typeface.FontWeight);
        hash.Add(typeface.FontWidth);
        hash.Add(typeface.FontSlant);
        return hash.ToRevision();
    }

    private static RevisionVisitedSetLease RentRevisionVisitedSet()
    {
        var visited = s_revisionVisitedSet;
        if (visited is null)
        {
            return new RevisionVisitedSetLease(new HashSet<object>(ObjectReferenceEqualityComparer<object>.Instance));
        }

        s_revisionVisitedSet = null;
        return new RevisionVisitedSetLease(visited);
    }

    private static bool TryGetPaintRevision(ShimSkiaSharp.SKPaint paint, out int revision)
    {
        using var visited = RentRevisionVisitedSet();
        return TryGetPaintRevision(paint, visited.Visited, out revision);
    }

    private static bool TryGetShaderRevision(ShimSkiaSharp.SKShader shader, out int revision)
    {
        using var visited = RentRevisionVisitedSet();
        return TryGetShaderRevision(shader, visited.Visited, out revision);
    }

    private static bool TryGetColorFilterRevision(ShimSkiaSharp.SKColorFilter colorFilter, out int revision)
    {
        using var visited = RentRevisionVisitedSet();
        return TryGetColorFilterRevision(colorFilter, visited.Visited, out revision);
    }

    private static bool TryGetPathEffectRevision(ShimSkiaSharp.SKPathEffect pathEffect, out int revision)
    {
        using var visited = RentRevisionVisitedSet();
        return TryGetPathEffectRevision(pathEffect, visited.Visited, out revision);
    }

    private static bool TryGetImageFilterRevision(ShimSkiaSharp.SKImageFilter imageFilter, out int revision)
    {
        using var visited = RentRevisionVisitedSet();
        return TryGetImageFilterRevision(imageFilter, visited.Visited, out revision);
    }

    private static bool TryGetPictureRevision(ShimSkiaSharp.SKPicture picture, out int revision)
    {
        using var visited = RentRevisionVisitedSet();
        return TryGetPictureRevision(picture, visited.Visited, out revision);
    }

    private static bool TryGetFontRevision(ShimSkiaSharp.SKFont? font, HashSet<object> visited, out int revision)
    {
        if (font is null)
        {
            revision = 0;
            return true;
        }

        if (!visited.Add(font))
        {
            revision = 0;
            return false;
        }

        try
        {
            var hash = new RevisionBuilder();
            hash.Add(font.Version);
            hash.Add(GetTypefaceRevision(font.Typeface));
            hash.Add(font.Size);
            hash.Add(font.ScaleX);
            hash.Add(font.SkewX);
            hash.Add(font.Subpixel);
            hash.Add(font.Embolden);
            hash.Add(font.Edging);
            revision = hash.ToRevision();
            return true;
        }
        finally
        {
            visited.Remove(font);
        }
    }

    private static bool TryGetTextBlobRevision(ShimSkiaSharp.SKTextBlob? textBlob, HashSet<object> visited, out int revision)
    {
        if (textBlob is null)
        {
            revision = 0;
            return true;
        }

        if (!visited.Add(textBlob))
        {
            revision = 0;
            return false;
        }

        try
        {
            if (!TryGetFontRevision(textBlob.Font, visited, out var fontRevision))
            {
                revision = 0;
                return false;
            }

            var hash = new RevisionBuilder();
            hash.Add(textBlob.Text);
            AddValues(ref hash, textBlob.Glyphs);
            AddValues(ref hash, textBlob.Points);
            hash.Add(fontRevision);
            revision = hash.ToRevision();
            return true;
        }
        finally
        {
            visited.Remove(textBlob);
        }
    }

    private static bool TryGetClipPathRevision(ShimSkiaSharp.ClipPath? clipPath, HashSet<object> visited, out int revision)
    {
        if (clipPath is null)
        {
            revision = 0;
            return true;
        }

        if (!visited.Add(clipPath))
        {
            revision = 0;
            return false;
        }

        try
        {
            var hash = new RevisionBuilder();
            hash.Add(clipPath.Transform);

            if (!TryGetClipPathRevision(clipPath.Clip, visited, out var nestedClipRevision))
            {
                revision = 0;
                return false;
            }

            hash.Add(nestedClipRevision);

            if (clipPath.Clips is null)
            {
                hash.Add(0);
            }
            else
            {
                hash.Add(clipPath.Clips.Count);
                for (var i = 0; i < clipPath.Clips.Count; i++)
                {
                    if (!TryGetPathClipRevision(clipPath.Clips[i], visited, out var pathClipRevision))
                    {
                        revision = 0;
                        return false;
                    }

                    hash.Add(pathClipRevision);
                }
            }

            revision = hash.ToRevision();
            return true;
        }
        finally
        {
            visited.Remove(clipPath);
        }
    }

    private static bool TryGetPathClipRevision(ShimSkiaSharp.PathClip? pathClip, HashSet<object> visited, out int revision)
    {
        if (pathClip is null)
        {
            revision = 0;
            return true;
        }

        if (!visited.Add(pathClip))
        {
            revision = 0;
            return false;
        }

        try
        {
            if (!TryGetClipPathRevision(pathClip.Clip, visited, out var nestedClipRevision))
            {
                revision = 0;
                return false;
            }

            var hash = new RevisionBuilder();
            hash.Add(pathClip.Path is null ? 0 : GetPathRevision(pathClip.Path));
            hash.Add(pathClip.Transform);
            hash.Add(nestedClipRevision);
            revision = hash.ToRevision();
            return true;
        }
        finally
        {
            visited.Remove(pathClip);
        }
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
                case PictureShader pictureShader:
                    hash.Add(pictureShader.TmX);
                    hash.Add(pictureShader.TmY);
                    hash.Add(pictureShader.LocalMatrix);
                    hash.Add(pictureShader.Tile);
                    if (!TryGetPictureRevision(pictureShader.Src, visited, out var pictureRevision))
                    {
                        revision = 0;
                        return false;
                    }
                    hash.Add(pictureRevision);
                    break;
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
            hash.Add(paint.Style);
            hash.Add(paint.IsAntialias);
            hash.Add(paint.IsDither);
            hash.Add(paint.StrokeWidth);
            hash.Add(paint.StrokeCap);
            hash.Add(paint.StrokeJoin);
            hash.Add(paint.StrokeMiter);
            hash.Add(paint.IsStrokeNonScaling);
            hash.Add(GetTypefaceRevision(paint.Typeface));
            hash.Add(paint.TextSize);
            hash.Add(paint.TextAlign);
            hash.Add(paint.LcdRenderText);
            hash.Add(paint.SubpixelText);
            hash.Add(paint.TextEncoding);
            hash.Add(paint.FontFeatureSettings);
            hash.Add(paint.FontKerning);
            hash.Add(paint.FontVariantLigatures);
            hash.Add(paint.Color);
            hash.Add(paint.BlendMode);
            hash.Add(paint.FilterQuality);

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
                    hash.Add(imageImageFilter.Image is null ? 0 : GetImageRevision(imageImageFilter.Image));
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
                case PictureImageFilter pictureImageFilter:
                    hash.Add(pictureImageFilter.Clip);
                    if (!TryGetPictureRevision(pictureImageFilter.Picture, visited, out var filterPictureRevision))
                    {
                        revision = 0;
                        return false;
                    }
                    hash.Add(filterPictureRevision);
                    break;
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

    private static bool TryGetPictureRevision(ShimSkiaSharp.SKPicture? picture, HashSet<object> visited, out int revision)
    {
        if (picture is null)
        {
            revision = 0;
            return true;
        }

        if (!visited.Add(picture))
        {
            revision = 0;
            return false;
        }

        try
        {
            var hash = new RevisionBuilder();
            hash.Add(picture.CullRect);

            if (picture.Commands is null)
            {
                hash.Add(0);
            }
            else
            {
                hash.Add(picture.Commands.Count);
                for (var i = 0; i < picture.Commands.Count; i++)
                {
                    if (!TryGetCanvasCommandRevision(picture.Commands[i], visited, out var commandRevision))
                    {
                        revision = 0;
                        return false;
                    }

                    hash.Add(commandRevision);
                }
            }

            revision = hash.ToRevision();
            return true;
        }
        finally
        {
            visited.Remove(picture);
        }
    }

    private static bool TryGetCanvasCommandRevision(CanvasCommand command, HashSet<object> visited, out int revision)
    {
        var hash = new RevisionBuilder();
        hash.Add(command.GetType());

        switch (command)
        {
            case ClipPathCanvasCommand clipPathCanvasCommand:
                hash.Add(clipPathCanvasCommand.Operation);
                hash.Add(clipPathCanvasCommand.Antialias);
                if (!TryGetClipPathRevision(clipPathCanvasCommand.ClipPath, visited, out var clipPathRevision))
                {
                    revision = 0;
                    return false;
                }
                hash.Add(clipPathRevision);
                break;
            case ClipRectCanvasCommand clipRectCanvasCommand:
                hash.Add(clipRectCanvasCommand.Rect);
                hash.Add(clipRectCanvasCommand.Operation);
                hash.Add(clipRectCanvasCommand.Antialias);
                break;
            case DrawImageCanvasCommand drawImageCanvasCommand:
                hash.Add(drawImageCanvasCommand.Image is null ? 0 : GetImageRevision(drawImageCanvasCommand.Image));
                hash.Add(drawImageCanvasCommand.Source);
                hash.Add(drawImageCanvasCommand.Dest);
                hash.Add(drawImageCanvasCommand.Sampling);
                if (!TryAddPaintRevision(ref hash, drawImageCanvasCommand.Paint, visited))
                {
                    revision = 0;
                    return false;
                }
                break;
            case DrawPictureCanvasCommand drawPictureCanvasCommand:
                if (!TryGetPictureRevision(drawPictureCanvasCommand.Picture, visited, out var nestedPictureRevision))
                {
                    revision = 0;
                    return false;
                }
                hash.Add(nestedPictureRevision);
                break;
            case DrawPathCanvasCommand drawPathCanvasCommand:
                hash.Add(drawPathCanvasCommand.Path is null ? 0 : GetPathRevision(drawPathCanvasCommand.Path));
                if (!TryAddPaintRevision(ref hash, drawPathCanvasCommand.Paint, visited))
                {
                    revision = 0;
                    return false;
                }
                break;
            case DrawTextBlobCanvasCommand drawTextBlobCanvasCommand:
                hash.Add(drawTextBlobCanvasCommand.X);
                hash.Add(drawTextBlobCanvasCommand.Y);
                if (!TryGetTextBlobRevision(drawTextBlobCanvasCommand.TextBlob, visited, out var textBlobRevision) ||
                    !TryAddPaintRevision(ref hash, drawTextBlobCanvasCommand.Paint, visited))
                {
                    revision = 0;
                    return false;
                }
                hash.Add(textBlobRevision);
                break;
            case DrawTextCanvasCommand drawTextCanvasCommand:
                hash.Add(drawTextCanvasCommand.Text);
                hash.Add(drawTextCanvasCommand.X);
                hash.Add(drawTextCanvasCommand.Y);
                hash.Add(drawTextCanvasCommand.TextAlign);
                if (!TryAddPaintRevision(ref hash, drawTextCanvasCommand.Paint, visited) ||
                    !TryGetFontRevision(drawTextCanvasCommand.Font, visited, out var drawTextFontRevision))
                {
                    revision = 0;
                    return false;
                }
                hash.Add(drawTextFontRevision);
                break;
            case DrawTextOnPathCanvasCommand drawTextOnPathCanvasCommand:
                hash.Add(drawTextOnPathCanvasCommand.Text);
                hash.Add(drawTextOnPathCanvasCommand.Path is null ? 0 : GetPathRevision(drawTextOnPathCanvasCommand.Path));
                hash.Add(drawTextOnPathCanvasCommand.HOffset);
                hash.Add(drawTextOnPathCanvasCommand.VOffset);
                hash.Add(drawTextOnPathCanvasCommand.TextAlign);
                if (!TryAddPaintRevision(ref hash, drawTextOnPathCanvasCommand.Paint, visited) ||
                    !TryGetFontRevision(drawTextOnPathCanvasCommand.Font, visited, out var drawTextOnPathFontRevision))
                {
                    revision = 0;
                    return false;
                }
                hash.Add(drawTextOnPathFontRevision);
                break;
            case RestoreCanvasCommand restoreCanvasCommand:
                hash.Add(restoreCanvasCommand.Count);
                break;
            case SaveCanvasCommand saveCanvasCommand:
                hash.Add(saveCanvasCommand.Count);
                break;
            case SaveLayerCanvasCommand saveLayerCanvasCommand:
                hash.Add(saveLayerCanvasCommand.Count);
                hash.Add(saveLayerCanvasCommand.Bounds);
                if (!TryAddPaintRevision(ref hash, saveLayerCanvasCommand.Paint, visited))
                {
                    revision = 0;
                    return false;
                }
                break;
            case SetMatrixCanvasCommand setMatrixCanvasCommand:
                hash.Add(setMatrixCanvasCommand.DeltaMatrix);
                hash.Add(setMatrixCanvasCommand.TotalMatrix);
                break;
            default:
                revision = 0;
                return false;
        }

        revision = hash.ToRevision();
        return true;
    }

    private static bool TryAddPaintRevision(ref RevisionBuilder hash, ShimSkiaSharp.SKPaint? paint, HashSet<object> visited)
    {
        if (paint is null)
        {
            hash.Add(0);
            return true;
        }

        if (!TryGetPaintRevision(paint, visited, out var paintRevision))
        {
            return false;
        }

        hash.Add(paintRevision);
        return true;
    }

    private SkiaSharp.SKPaint? CreateRenderPaint(ShimSkiaSharp.SKPaint paint)
    {
        var style = ToSKPaintStyle(paint.Style);
        var strokeCap = ToSKStrokeCap(paint.StrokeCap);
        var strokeJoin = ToSKStrokeJoin(paint.StrokeJoin);
        var textAlign = ToSKTextAlign(paint.TextAlign);
        var typefaceResolution = ResolvePaintTypeface(paint);
        var typeface = typefaceResolution?.Typeface;
        var textEncoding = ToSKTextEncoding(paint.TextEncoding);
        var color = paint.Color is null
            ? SkiaSharp.SKColor.Empty
            : ToSKColor(paint.Color.Value);
        var shader = GetRenderShader(paint.Shader);
        var colorFilter = GetRenderColorFilter(paint.ColorFilter);
        var imageFilter = GetRenderImageFilter(paint.ImageFilter);
        var pathEffect = GetRenderPathEffect(paint.PathEffect);
        var blendMode = ToSKBlendMode(paint.BlendMode);
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
            BlendMode = blendMode
        };

        ApplyTypefaceAdjustments(paint, skPaint, typefaceResolution?.SuppressSyntheticBold ?? false);
        return skPaint;
    }

    internal SkiaSharp.SKPaint? GetRenderPaint(ShimSkiaSharp.SKPaint? paint)
    {
        if (paint is null)
        {
            return null;
        }

        var canCacheSimplePaint = CanCacheRenderPaint(paint);
        if (!canCacheSimplePaint && !_cacheComplexRenderPaintsForCurrentPicture)
        {
            return CreateRenderPaint(paint);
        }

        var revision = paint.Version;
        if (!canCacheSimplePaint && !TryGetPaintRevision(paint, out revision))
        {
            return CreateRenderPaint(paint);
        }

        lock (_nativeObjectCacheLock)
        {
            if (_nativePaintCache.TryGetValue(paint, out var cached) &&
                cached.Version == revision &&
                cached.Paint.Handle != IntPtr.Zero)
            {
                return cached.Paint;
            }

            var created = CreateRenderPaint(paint);
            if (created is null)
            {
                return null;
            }

            _nativePaintCache.Remove(paint);
            _nativePaintCache.Add(paint, new NativePaintCacheEntry(revision, created));
            return created;
        }
    }

    internal SkiaSharp.SKShader? GetRenderShader(ShimSkiaSharp.SKShader? shader)
    {
        if (shader is null)
        {
            return null;
        }

        if (!TryGetShaderRevision(shader, out var revision))
        {
            return ToSKShader(shader);
        }

        lock (_nativeObjectCacheLock)
        {
            if (_nativeShaderCache.TryGetValue(shader, out var cached) &&
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

            _nativeShaderCache.Remove(shader);
            _nativeShaderCache.Add(shader, new NativeShaderCacheEntry(revision, created));
            return created;
        }
    }

    internal SkiaSharp.SKColorFilter? GetRenderColorFilter(ShimSkiaSharp.SKColorFilter? colorFilter)
    {
        if (colorFilter is null)
        {
            return null;
        }

        if (!TryGetColorFilterRevision(colorFilter, out var revision))
        {
            return ToSKColorFilter(colorFilter);
        }

        lock (_nativeObjectCacheLock)
        {
            if (_nativeColorFilterCache.TryGetValue(colorFilter, out var cached) &&
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

            _nativeColorFilterCache.Remove(colorFilter);
            _nativeColorFilterCache.Add(colorFilter, new NativeColorFilterCacheEntry(revision, created));
            return created;
        }
    }

    internal SkiaSharp.SKPathEffect? GetRenderPathEffect(ShimSkiaSharp.SKPathEffect? pathEffect)
    {
        if (pathEffect is null)
        {
            return null;
        }

        if (!TryGetPathEffectRevision(pathEffect, out var revision))
        {
            return ToSKPathEffect(pathEffect);
        }

        lock (_nativeObjectCacheLock)
        {
            if (_nativePathEffectCache.TryGetValue(pathEffect, out var cached) &&
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

            _nativePathEffectCache.Remove(pathEffect);
            _nativePathEffectCache.Add(pathEffect, new NativePathEffectCacheEntry(revision, created));
            return created;
        }
    }

    internal SkiaSharp.SKImageFilter? GetRenderImageFilter(ShimSkiaSharp.SKImageFilter? imageFilter)
    {
        if (imageFilter is null)
        {
            return null;
        }

        if (!TryGetImageFilterRevision(imageFilter, out var revision))
        {
            return ToSKImageFilter(imageFilter);
        }

        lock (_nativeObjectCacheLock)
        {
            if (_nativeImageFilterCache.TryGetValue(imageFilter, out var cached) &&
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

            _nativeImageFilterCache.Remove(imageFilter);
            _nativeImageFilterCache.Add(imageFilter, new NativeImageFilterCacheEntry(revision, created));
            return created;
        }
    }

    internal SkiaSharp.SKPath? GetRenderPath(ShimSkiaSharp.SKPath? path)
    {
        if (path is null)
        {
            return null;
        }

        var revision = GetRenderPathCacheRevision(path);
        var canUseValueCache = TryCreateSmallPolyPathCacheKey(path, out var valueKey);

        lock (_nativeObjectCacheLock)
        {
            if (_nativePathCache.TryGetValue(path, out var cached) &&
                cached.Revision == revision &&
                cached.Path.Handle != IntPtr.Zero)
            {
                return cached.Path;
            }

            if (canUseValueCache &&
                _smallPolyNativePathCache.TryGetValue(valueKey, out var cachedValuePath))
            {
                if (cachedValuePath.Handle != IntPtr.Zero)
                {
                    _nativePathCache.Remove(path);
                    _nativePathCache.Add(path, new NativePathCacheEntry(revision, cachedValuePath));
                    return cachedValuePath;
                }

                _ = _smallPolyNativePathCache.Remove(valueKey);
            }

            var created = ToSKPath(path);
            _nativePathCache.Remove(path);
            _nativePathCache.Add(path, new NativePathCacheEntry(revision, created));
            if (canUseValueCache)
            {
                CacheSmallPolyRenderPath(valueKey, created);
            }
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

        lock (_nativeObjectCacheLock)
        {
            if (_nativeImageCache.TryGetValue(image, out var cached) &&
                cached.Revision == revision &&
                cached.Image.Handle != IntPtr.Zero)
            {
                return cached.Image;
            }

            var created = ToSKImage(image);
            _nativeImageCache.Remove(image);
            _nativeImageCache.Add(image, new NativeImageCacheEntry(revision, created));
            return created;
        }
    }

    internal SkiaSharp.SKPicture? GetRenderPicture(ShimSkiaSharp.SKPicture? picture)
    {
        if (picture is null)
        {
            return null;
        }

        if (TryGetCachedPicture(picture, out var registeredPicture) &&
            registeredPicture.Handle != IntPtr.Zero)
        {
            return registeredPicture;
        }

        if (!TryGetPictureRevision(picture, out var revision))
        {
            return ToSKPicture(picture);
        }

        lock (_nativeObjectCacheLock)
        {
            if (_nativePictureCache.TryGetValue(picture, out var cached) &&
                cached.Revision == revision &&
                cached.Picture.Handle != IntPtr.Zero)
            {
                return cached.Picture;
            }

            var created = ToSKPicture(picture);
            if (created is null)
            {
                return null;
            }

            _nativePictureCache.Remove(picture);
            _nativePictureCache.Add(picture, new NativePictureCacheEntry(revision, created));
            return created;
        }
    }

    internal bool TryGetReusableRenderPicture(ShimSkiaSharp.SKPicture? picture, bool createIfMissing, out SkiaSharp.SKPicture? nativePicture)
    {
        nativePicture = null;
        if (picture is null)
        {
            return false;
        }

        if (TryGetCachedPicture(picture, out var registeredPicture) &&
            registeredPicture.Handle != IntPtr.Zero)
        {
            nativePicture = registeredPicture;
            return true;
        }

        if (!TryGetPictureRevision(picture, out var revision))
        {
            return false;
        }

        lock (_nativeObjectCacheLock)
        {
            if (_nativePictureCache.TryGetValue(picture, out var cached) &&
                cached.Revision == revision &&
                cached.Picture.Handle != IntPtr.Zero)
            {
                nativePicture = cached.Picture;
                return true;
            }

            if (!createIfMissing)
            {
                return false;
            }

            var created = ToSKPicture(picture);
            if (created is null)
            {
                return false;
            }

            _nativePictureCache.Remove(picture);
            _nativePictureCache.Add(picture, new NativePictureCacheEntry(revision, created));
            nativePicture = created;
            return true;
        }
    }

    internal void ClearReusableRenderCaches()
    {
        lock (_nativeObjectCacheLock)
        {
            _nativePaintCache = new ConditionalWeakTable<ShimSkiaSharp.SKPaint, NativePaintCacheEntry>();
            _nativePathCache = new ConditionalWeakTable<ShimSkiaSharp.SKPath, NativePathCacheEntry>();
            _nativeImageCache = new ConditionalWeakTable<ShimSkiaSharp.SKImage, NativeImageCacheEntry>();
            _nativeShaderCache = new ConditionalWeakTable<ShimSkiaSharp.SKShader, NativeShaderCacheEntry>();
            _nativeColorFilterCache = new ConditionalWeakTable<ShimSkiaSharp.SKColorFilter, NativeColorFilterCacheEntry>();
            _nativePathEffectCache = new ConditionalWeakTable<ShimSkiaSharp.SKPathEffect, NativePathEffectCacheEntry>();
            _nativeImageFilterCache = new ConditionalWeakTable<ShimSkiaSharp.SKImageFilter, NativeImageFilterCacheEntry>();
            _nativePictureCache = new ConditionalWeakTable<ShimSkiaSharp.SKPicture, NativePictureCacheEntry>();
            _smallPolyNativePathCache.Clear();
        }
    }

    internal void RegisterCachedPicture(ShimSkiaSharp.SKPicture picture, SkiaSharp.SKPicture skPicture)
    {
        lock (_pictureCacheLock)
        {
            _pictureCache[picture] = skPicture;
        }
    }

    internal void UnregisterCachedPicture(ShimSkiaSharp.SKPicture? picture)
    {
        if (picture is null)
        {
            return;
        }

        lock (_pictureCacheLock)
        {
            _ = _pictureCache.Remove(picture);
        }
    }

    internal bool TryGetCachedPicture(ShimSkiaSharp.SKPicture picture, out SkiaSharp.SKPicture skPicture)
    {
        lock (_pictureCacheLock)
        {
            return _pictureCache.TryGetValue(picture, out skPicture!);
        }
    }

    internal void ClearCachedPictures()
    {
        lock (_pictureCacheLock)
        {
            _pictureCache.Clear();
        }
    }
}
