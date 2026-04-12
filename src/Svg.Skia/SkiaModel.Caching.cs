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
    private const int PositionedTextCacheRefTrimThreshold = 1024;

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
        public NativePathCacheEntry(int version, SkiaSharp.SKPath path)
        {
            Version = version;
            Path = path;
        }

        public int Version { get; }
        public SkiaSharp.SKPath Path { get; }
    }

    private sealed class NativeImageCacheEntry
    {
        public NativeImageCacheEntry(int version, SkiaSharp.SKImage image)
        {
            Version = version;
            Image = image;
        }

        public int Version { get; }
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

    private readonly ConcurrentDictionary<TypefaceKey, SkiaSharp.SKTypeface?> _typefaceCache = new();
    private readonly ConcurrentDictionary<TypefaceKey, SkiaSharp.SKTypeface?> _resolvedTypefaceCache = new();
    private readonly object _positionedTextCacheLock = new();
    private readonly object _pictureCacheLock = new();
    private readonly object _nativeObjectCacheLock = new();
    private ConditionalWeakTable<DrawTextBlobCanvasCommand, PositionedTextCache> _positionedTextCache = new();
    private ConditionalWeakTable<ShimSkiaSharp.SKPaint, NativePaintCacheEntry> _nativePaintCache = new();
    private ConditionalWeakTable<ShimSkiaSharp.SKPath, NativePathCacheEntry> _nativePathCache = new();
    private ConditionalWeakTable<ShimSkiaSharp.SKImage, NativeImageCacheEntry> _nativeImageCache = new();
    private ConditionalWeakTable<ShimSkiaSharp.SKShader, NativeShaderCacheEntry> _nativeShaderCache = new();
    private ConditionalWeakTable<ShimSkiaSharp.SKColorFilter, NativeColorFilterCacheEntry> _nativeColorFilterCache = new();
    private ConditionalWeakTable<ShimSkiaSharp.SKPathEffect, NativePathEffectCacheEntry> _nativePathEffectCache = new();
    private ConditionalWeakTable<ShimSkiaSharp.SKImageFilter, NativeImageFilterCacheEntry> _nativeImageFilterCache = new();
    private readonly List<WeakReference<SkiaSharp.SKTextBlob>> _positionedTextCacheRefs = new();
    private readonly Dictionary<ShimSkiaSharp.SKPicture, SkiaSharp.SKPicture> _pictureCache = new(PictureReferenceEqualityComparer.Instance);
    private IList<ITypefaceProvider>? _providerStateList;
    private int _providerStateHash;

    private static bool CanCacheRenderPaint(ShimSkiaSharp.SKPaint paint)
    {
        return paint.Shader is null
            && paint.ColorFilter is null
            && paint.ImageFilter is null
            && paint.PathEffect is null;
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

        lock (_nativeObjectCacheLock)
        {
            if (_nativePaintCache.TryGetValue(paint, out var cached) &&
                cached.Version == paint.Version &&
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
            _nativePaintCache.Add(paint, new NativePaintCacheEntry(paint.Version, created));
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

        if (!TryGetColorFilterRevision(colorFilter, CreateVisitedSet(), out var revision))
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

        if (!TryGetPathEffectRevision(pathEffect, CreateVisitedSet(), out var revision))
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

        if (!TryGetImageFilterRevision(imageFilter, CreateVisitedSet(), out var revision))
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

        lock (_nativeObjectCacheLock)
        {
            if (_nativePathCache.TryGetValue(path, out var cached) &&
                cached.Version == path.Version &&
                cached.Path.Handle != IntPtr.Zero)
            {
                return cached.Path;
            }

            var created = ToSKPath(path);
            _nativePathCache.Remove(path);
            _nativePathCache.Add(path, new NativePathCacheEntry(path.Version, created));
            return created;
        }
    }

    internal SkiaSharp.SKImage? GetRenderImage(ShimSkiaSharp.SKImage? image)
    {
        if (image is null)
        {
            return null;
        }

        lock (_nativeObjectCacheLock)
        {
            if (_nativeImageCache.TryGetValue(image, out var cached) &&
                cached.Version == image.Version &&
                cached.Image.Handle != IntPtr.Zero)
            {
                return cached.Image;
            }

            var created = ToSKImage(image);
            _nativeImageCache.Remove(image);
            _nativeImageCache.Add(image, new NativeImageCacheEntry(image.Version, created));
            return created;
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
