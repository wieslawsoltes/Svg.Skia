// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

namespace ShimSkiaSharp;

public abstract record SKImageFilter : IDeepCloneable<SKImageFilter>
{
    public static SKImageFilter CreateArithmetic(float k1, float k2, float k3, float k4, bool enforcePMColor, SKImageFilter background, SKImageFilter? foreground = null, SKRect? cropRect = null)
        => new ArithmeticImageFilter(k1, k2, k3, k4, enforcePMColor, background, foreground, cropRect);

    public static SKImageFilter CreateBlendMode(SKBlendMode mode, SKImageFilter background, SKImageFilter? foreground = null, SKRect? cropRect = null)
        => new BlendModeImageFilter(mode, background, foreground, cropRect);

    public static SKImageFilter CreateBlur(float sigmaX, float sigmaY, SKImageFilter? input = null, SKRect? cropRect = null)
        => new BlurImageFilter(sigmaX, sigmaY, input, cropRect);

    public static SKImageFilter CreateColorFilter(SKColorFilter cf, SKImageFilter? input = null, SKRect? cropRect = null)
        => new ColorFilterImageFilter(cf, input, cropRect);

    public static SKImageFilter CreateDilate(int radiusX, int radiusY, SKImageFilter? input = null, SKRect? cropRect = null)
        => new DilateImageFilter(radiusX, radiusY, input, cropRect);

    public static SKImageFilter CreateDisplacementMapEffect(SKColorChannel xChannelSelector, SKColorChannel yChannelSelector, float scale, SKImageFilter displacement, SKImageFilter? input = null, SKRect? cropRect = null)
        => new DisplacementMapEffectImageFilter(xChannelSelector, yChannelSelector, scale, displacement, input, cropRect);

    public static SKImageFilter CreateDistantLitDiffuse(SKPoint3 direction, SKColor lightColor, float surfaceScale, float kd, SKImageFilter? input = null, SKRect? cropRect = null)
        => new DistantLitDiffuseImageFilter(direction, lightColor, surfaceScale, kd, input, cropRect);

    public static SKImageFilter CreateDistantLitSpecular(SKPoint3 direction, SKColor lightColor, float surfaceScale, float ks, float shininess, SKImageFilter? input = null, SKRect? cropRect = null)
        => new DistantLitSpecularImageFilter(direction, lightColor, surfaceScale, ks, shininess, input, cropRect);

    public static SKImageFilter CreateErode(int radiusX, int radiusY, SKImageFilter? input = null, SKRect? cropRect = null)
        => new ErodeImageFilter(radiusX, radiusY, input, cropRect);

    public static SKImageFilter CreateImage(SKImage image, SKRect src, SKRect dst, SKFilterQuality filterQuality)
        => new ImageImageFilter(image, src, dst, filterQuality);

    public static SKImageFilter CreateMatrixConvolution(SKSizeI kernelSize, float[] kernel, float gain, float bias, SKPointI kernelOffset, SKShaderTileMode tileMode, bool convolveAlpha, SKImageFilter? input = null, SKRect? cropRect = null)
        => new MatrixConvolutionImageFilter(kernelSize, kernel, gain, bias, kernelOffset, tileMode, convolveAlpha, input, cropRect);

    public static SKImageFilter CreateMerge(SKImageFilter[] filters, SKRect? cropRect = null)
        => new MergeImageFilter(filters, cropRect);

    public static SKImageFilter CreateOffset(float dx, float dy, SKImageFilter? input = null, SKRect? cropRect = null)
        => new OffsetImageFilter(dx, dy, input, cropRect);

    public static SKImageFilter CreatePaint(SKPaint paint, SKRect? cropRect = null)
        => new PaintImageFilter(paint, cropRect);

    public static SKImageFilter CreateShader(SKShader shader, bool dither, SKRect? cropRect = null)
        => new ShaderImageFilter(shader, dither, cropRect);

    public static SKImageFilter CreatePicture(SKPicture picture, SKRect cropRect)
        => new PictureImageFilter(picture, cropRect);

    public static SKImageFilter CreatePointLitDiffuse(SKPoint3 location, SKColor lightColor, float surfaceScale, float kd, SKImageFilter? input = null, SKRect? cropRect = null)
        => new PointLitDiffuseImageFilter(location, lightColor, surfaceScale, kd, input, cropRect);

    public static SKImageFilter CreatePointLitSpecular(SKPoint3 location, SKColor lightColor, float surfaceScale, float ks, float shininess, SKImageFilter? input = null, SKRect? cropRect = null)
        => new PointLitSpecularImageFilter(location, lightColor, surfaceScale, ks, shininess, input, cropRect);

    public static SKImageFilter CreateSpotLitDiffuse(SKPoint3 location, SKPoint3 target, float specularExponent, float cutoffAngle, SKColor lightColor, float surfaceScale, float kd, SKImageFilter? input = null, SKRect? cropRect = null)
        => new SpotLitDiffuseImageFilter(location, target, specularExponent, cutoffAngle, lightColor, surfaceScale, kd, input, cropRect);

    public static SKImageFilter CreateSpotLitSpecular(SKPoint3 location, SKPoint3 target, float specularExponent, float cutoffAngle, SKColor lightColor, float surfaceScale, float ks, float shininess, SKImageFilter? input = null, SKRect? cropRect = null)
        => new SpotLitSpecularImageFilter(location, target, specularExponent, cutoffAngle, lightColor, surfaceScale, ks, shininess, input, cropRect);

    public static SKImageFilter CreateTile(SKRect src, SKRect dst, SKImageFilter? input)
        => new TileImageFilter(src, dst, input);

    public SKImageFilter DeepClone()
    {
        return this switch
        {
            ArithmeticImageFilter arithmeticImageFilter => new ArithmeticImageFilter(arithmeticImageFilter.K1, arithmeticImageFilter.K2, arithmeticImageFilter.K3, arithmeticImageFilter.K4, arithmeticImageFilter.EforcePMColor, arithmeticImageFilter.Background?.DeepClone(), arithmeticImageFilter.Foreground?.DeepClone(), arithmeticImageFilter.Clip),
            BlendModeImageFilter blendModeImageFilter => new BlendModeImageFilter(blendModeImageFilter.Mode, blendModeImageFilter.Background?.DeepClone(), blendModeImageFilter.Foreground?.DeepClone(), blendModeImageFilter.Clip),
            BlurImageFilter blurImageFilter => new BlurImageFilter(blurImageFilter.SigmaX, blurImageFilter.SigmaY, blurImageFilter.Input?.DeepClone(), blurImageFilter.Clip),
            ColorFilterImageFilter colorFilterImageFilter => new ColorFilterImageFilter(colorFilterImageFilter.ColorFilter?.DeepClone(), colorFilterImageFilter.Input?.DeepClone(), colorFilterImageFilter.Clip),
            DilateImageFilter dilateImageFilter => new DilateImageFilter(dilateImageFilter.RadiusX, dilateImageFilter.RadiusY, dilateImageFilter.Input?.DeepClone(), dilateImageFilter.Clip),
            DisplacementMapEffectImageFilter displacementMapEffectImageFilter => new DisplacementMapEffectImageFilter(displacementMapEffectImageFilter.XChannelSelector, displacementMapEffectImageFilter.YChannelSelector, displacementMapEffectImageFilter.Scale, displacementMapEffectImageFilter.Displacement?.DeepClone(), displacementMapEffectImageFilter.Input?.DeepClone(), displacementMapEffectImageFilter.Clip),
            DistantLitDiffuseImageFilter distantLitDiffuseImageFilter => new DistantLitDiffuseImageFilter(distantLitDiffuseImageFilter.Direction, distantLitDiffuseImageFilter.LightColor, distantLitDiffuseImageFilter.SurfaceScale, distantLitDiffuseImageFilter.Kd, distantLitDiffuseImageFilter.Input?.DeepClone(), distantLitDiffuseImageFilter.Clip),
            DistantLitSpecularImageFilter distantLitSpecularImageFilter => new DistantLitSpecularImageFilter(distantLitSpecularImageFilter.Direction, distantLitSpecularImageFilter.LightColor, distantLitSpecularImageFilter.SurfaceScale, distantLitSpecularImageFilter.Ks, distantLitSpecularImageFilter.Shininess, distantLitSpecularImageFilter.Input?.DeepClone(), distantLitSpecularImageFilter.Clip),
            ErodeImageFilter erodeImageFilter => new ErodeImageFilter(erodeImageFilter.RadiusX, erodeImageFilter.RadiusY, erodeImageFilter.Input?.DeepClone(), erodeImageFilter.Clip),
            ImageImageFilter imageImageFilter => new ImageImageFilter(imageImageFilter.Image?.Clone(), imageImageFilter.Src, imageImageFilter.Dst, imageImageFilter.FilterQuality),
            MatrixConvolutionImageFilter matrixConvolutionImageFilter => new MatrixConvolutionImageFilter(matrixConvolutionImageFilter.KernelSize, CloneHelpers.CloneArray(matrixConvolutionImageFilter.Kernel), matrixConvolutionImageFilter.Gain, matrixConvolutionImageFilter.Bias, matrixConvolutionImageFilter.KernelOffset, matrixConvolutionImageFilter.TileMode, matrixConvolutionImageFilter.ConvolveAlpha, matrixConvolutionImageFilter.Input?.DeepClone(), matrixConvolutionImageFilter.Clip),
            MergeImageFilter mergeImageFilter => new MergeImageFilter(CloneHelpers.CloneArray(mergeImageFilter.Filters, filter => filter.DeepClone()), mergeImageFilter.Clip),
            OffsetImageFilter offsetImageFilter => new OffsetImageFilter(offsetImageFilter.Dx, offsetImageFilter.Dy, offsetImageFilter.Input?.DeepClone(), offsetImageFilter.Clip),
            PaintImageFilter paintImageFilter => new PaintImageFilter(paintImageFilter.Paint?.Clone(), paintImageFilter.Clip),
            ShaderImageFilter shaderImageFilter => new ShaderImageFilter(shaderImageFilter.Shader?.DeepClone(), shaderImageFilter.Dither, shaderImageFilter.Clip),
            PictureImageFilter pictureImageFilter => new PictureImageFilter(pictureImageFilter.Picture?.DeepClone(), pictureImageFilter.Clip),
            PointLitDiffuseImageFilter pointLitDiffuseImageFilter => new PointLitDiffuseImageFilter(pointLitDiffuseImageFilter.Location, pointLitDiffuseImageFilter.LightColor, pointLitDiffuseImageFilter.SurfaceScale, pointLitDiffuseImageFilter.Kd, pointLitDiffuseImageFilter.Input?.DeepClone(), pointLitDiffuseImageFilter.Clip),
            PointLitSpecularImageFilter pointLitSpecularImageFilter => new PointLitSpecularImageFilter(pointLitSpecularImageFilter.Location, pointLitSpecularImageFilter.LightColor, pointLitSpecularImageFilter.SurfaceScale, pointLitSpecularImageFilter.Ks, pointLitSpecularImageFilter.Shininess, pointLitSpecularImageFilter.Input?.DeepClone(), pointLitSpecularImageFilter.Clip),
            SpotLitDiffuseImageFilter spotLitDiffuseImageFilter => new SpotLitDiffuseImageFilter(spotLitDiffuseImageFilter.Location, spotLitDiffuseImageFilter.Target, spotLitDiffuseImageFilter.SpecularExponent, spotLitDiffuseImageFilter.CutoffAngle, spotLitDiffuseImageFilter.LightColor, spotLitDiffuseImageFilter.SurfaceScale, spotLitDiffuseImageFilter.Kd, spotLitDiffuseImageFilter.Input?.DeepClone(), spotLitDiffuseImageFilter.Clip),
            SpotLitSpecularImageFilter spotLitSpecularImageFilter => new SpotLitSpecularImageFilter(spotLitSpecularImageFilter.Location, spotLitSpecularImageFilter.Target, spotLitSpecularImageFilter.SpecularExponent, spotLitSpecularImageFilter.CutoffAngle, spotLitSpecularImageFilter.LightColor, spotLitSpecularImageFilter.SurfaceScale, spotLitSpecularImageFilter.Ks, spotLitSpecularImageFilter.Shininess, spotLitSpecularImageFilter.Input?.DeepClone(), spotLitSpecularImageFilter.Clip),
            TileImageFilter tileImageFilter => new TileImageFilter(tileImageFilter.Src, tileImageFilter.Dst, tileImageFilter.Input?.DeepClone()),
            _ => throw new NotSupportedException($"Unsupported {nameof(SKImageFilter)} type: {GetType().Name}.")
        };
    }
}

public record ArithmeticImageFilter(float K1, float K2, float K3, float K4, bool EforcePMColor, SKImageFilter? Background, SKImageFilter? Foreground, SKRect? Clip) : SKImageFilter;

public record BlendModeImageFilter(SKBlendMode Mode, SKImageFilter? Background, SKImageFilter? Foreground, SKRect? Clip) : SKImageFilter;

public record BlurImageFilter(float SigmaX, float SigmaY, SKImageFilter? Input, SKRect? Clip) : SKImageFilter;

public record ColorFilterImageFilter(SKColorFilter? ColorFilter, SKImageFilter? Input, SKRect? Clip) : SKImageFilter;

public record DilateImageFilter(int RadiusX, int RadiusY, SKImageFilter? Input, SKRect? Clip) : SKImageFilter;

public record DisplacementMapEffectImageFilter(SKColorChannel XChannelSelector, SKColorChannel YChannelSelector, float Scale, SKImageFilter? Displacement, SKImageFilter? Input, SKRect? Clip) : SKImageFilter;

public record DistantLitDiffuseImageFilter(SKPoint3 Direction, SKColor LightColor, float SurfaceScale, float Kd, SKImageFilter? Input, SKRect? Clip) : SKImageFilter;

public record DistantLitSpecularImageFilter(SKPoint3 Direction, SKColor LightColor, float SurfaceScale, float Ks, float Shininess, SKImageFilter? Input, SKRect? Clip) : SKImageFilter;

public record ErodeImageFilter(int RadiusX, int RadiusY, SKImageFilter? Input, SKRect? Clip) : SKImageFilter;

public record ImageImageFilter(SKImage? Image, SKRect Src, SKRect Dst, SKFilterQuality FilterQuality) : SKImageFilter;

public record MatrixConvolutionImageFilter(SKSizeI KernelSize, float[]? Kernel, float Gain, float Bias, SKPointI KernelOffset, SKShaderTileMode TileMode, bool ConvolveAlpha, SKImageFilter? Input, SKRect? Clip) : SKImageFilter;

public record MergeImageFilter(SKImageFilter[]? Filters, SKRect? Clip) : SKImageFilter;

public record OffsetImageFilter(float Dx, float Dy, SKImageFilter? Input, SKRect? Clip) : SKImageFilter;

public record PaintImageFilter(SKPaint? Paint, SKRect? Clip) : SKImageFilter;

public record ShaderImageFilter(SKShader? Shader, bool Dither, SKRect? Clip) : SKImageFilter;

public record PictureImageFilter(SKPicture? Picture, SKRect? Clip) : SKImageFilter;

public record PointLitDiffuseImageFilter(SKPoint3 Location, SKColor LightColor, float SurfaceScale, float Kd, SKImageFilter? Input, SKRect? Clip) : SKImageFilter;

public record PointLitSpecularImageFilter(SKPoint3 Location, SKColor LightColor, float SurfaceScale, float Ks, float Shininess, SKImageFilter? Input, SKRect? Clip) : SKImageFilter;

public record SpotLitDiffuseImageFilter(SKPoint3 Location, SKPoint3 Target, float SpecularExponent, float CutoffAngle, SKColor LightColor, float SurfaceScale, float Kd, SKImageFilter? Input, SKRect? Clip) : SKImageFilter;

public record SpotLitSpecularImageFilter(SKPoint3 Location, SKPoint3 Target, float SpecularExponent, float CutoffAngle, SKColor LightColor, float SurfaceScale, float Ks, float Shininess, SKImageFilter? Input, SKRect? Clip) : SKImageFilter;

public record TileImageFilter(SKRect Src, SKRect Dst, SKImageFilter? Input) : SKImageFilter;
