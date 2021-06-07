using System;
using ShimSkiaSharp.Primitives;

namespace ShimSkiaSharp.Painting
{
    public abstract record SKImageFilter
    {
        public record CropRect(SKRect Rect)
        {
            public override string ToString() => FormattableString.Invariant($"{Rect}");
        }

        public static SKImageFilter CreateArithmetic(float k1, float k2, float k3, float k4, bool enforcePMColor, SKImageFilter background, SKImageFilter? foreground = null, CropRect? cropRect = null) 
            => new ArithmeticImageFilter(k1, k2, k3, k4, enforcePMColor, background, foreground, cropRect);

        public static SKImageFilter CreateBlendMode(SKBlendMode mode, SKImageFilter background, SKImageFilter? foreground = null, CropRect? cropRect = null) 
            => new BlendModeImageFilter(mode, background, foreground, cropRect);

        public static SKImageFilter CreateBlur(float sigmaX, float sigmaY, SKImageFilter? input = null, CropRect? cropRect = null) 
            => new BlurImageFilter(sigmaX, sigmaY, input, cropRect);

        public static SKImageFilter CreateColorFilter(SKColorFilter cf, SKImageFilter? input = null, CropRect? cropRect = null) 
            => new ColorFilterImageFilter(cf, input, cropRect);

        public static SKImageFilter CreateDilate(int radiusX, int radiusY, SKImageFilter? input = null, CropRect? cropRect = null) 
            => new DilateImageFilter(radiusX, radiusY, input, cropRect);

        public static SKImageFilter CreateDisplacementMapEffect(SKColorChannel xChannelSelector, SKColorChannel yChannelSelector, float scale, SKImageFilter displacement, SKImageFilter? input = null, CropRect? cropRect = null) 
            => new DisplacementMapEffectImageFilter(xChannelSelector, yChannelSelector, scale, displacement, input, cropRect);

        public static SKImageFilter CreateDistantLitDiffuse(SKPoint3 direction, SKColor lightColor, float surfaceScale, float kd, SKImageFilter? input = null, CropRect? cropRect = null) 
            => new DistantLitDiffuseImageFilter(direction, lightColor, surfaceScale, kd, input, cropRect);

        public static SKImageFilter CreateDistantLitSpecular(SKPoint3 direction, SKColor lightColor, float surfaceScale, float ks, float shininess, SKImageFilter? input = null, CropRect? cropRect = null) 
            => new DistantLitSpecularImageFilter(direction, lightColor, surfaceScale, ks, shininess, input, cropRect);

        public static SKImageFilter CreateErode(int radiusX, int radiusY, SKImageFilter? input = null, CropRect? cropRect = null) 
            => new ErodeImageFilter(radiusX, radiusY, input, cropRect);

        public static SKImageFilter CreateImage(SKImage image, SKRect src, SKRect dst, SKFilterQuality filterQuality) 
            => new ImageImageFilter(image, src, dst, filterQuality);

        public static SKImageFilter CreateMatrixConvolution(SKSizeI kernelSize, float[] kernel, float gain, float bias, SKPointI kernelOffset, SKShaderTileMode tileMode, bool convolveAlpha, SKImageFilter? input = null, CropRect? cropRect = null) 
            => new MatrixConvolutionImageFilter(kernelSize, kernel, gain, bias, kernelOffset, tileMode, convolveAlpha, input, cropRect);

        public static SKImageFilter CreateMerge(SKImageFilter[] filters, CropRect? cropRect = null) 
            => new MergeImageFilter(filters, cropRect);

        public static SKImageFilter CreateOffset(float dx, float dy, SKImageFilter? input = null, CropRect? cropRect = null) 
            => new OffsetImageFilter(dx, dy, input, cropRect);

        public static SKImageFilter CreatePaint(SKPaint paint, CropRect? cropRect = null) 
            => new PaintImageFilter(paint, cropRect);

        public static SKImageFilter CreatePicture(SKPicture picture, SKRect cropRect) 
            => new PictureImageFilter(picture, cropRect);

        public static SKImageFilter CreatePointLitDiffuse(SKPoint3 location, SKColor lightColor, float surfaceScale, float kd, SKImageFilter? input = null, CropRect? cropRect = null) 
            => new PointLitDiffuseImageFilter(location, lightColor, surfaceScale, kd, input, cropRect);

        public static SKImageFilter CreatePointLitSpecular(SKPoint3 location, SKColor lightColor, float surfaceScale, float ks, float shininess, SKImageFilter? input = null, CropRect? cropRect = null) 
            => new PointLitSpecularImageFilter(location, lightColor, surfaceScale, ks, shininess, input, cropRect);

        public static SKImageFilter CreateSpotLitDiffuse(SKPoint3 location, SKPoint3 target, float specularExponent, float cutoffAngle, SKColor lightColor, float surfaceScale, float kd, SKImageFilter? input = null, CropRect? cropRect = null) 
            => new SpotLitDiffuseImageFilter(location, target, specularExponent, cutoffAngle, lightColor, surfaceScale, kd, input, cropRect);

        public static SKImageFilter CreateSpotLitSpecular(SKPoint3 location, SKPoint3 target, float specularExponent, float cutoffAngle, SKColor lightColor, float surfaceScale, float ks, float shininess, SKImageFilter? input = null, CropRect? cropRect = null) 
            => new SpotLitSpecularImageFilter(location, target, specularExponent, cutoffAngle, lightColor, surfaceScale, ks, shininess, input, cropRect);

        public static SKImageFilter CreateTile(SKRect src, SKRect dst, SKImageFilter? input) 
            => new TileImageFilter(src, dst, input);
    }

    public record ArithmeticImageFilter(float K1, float K2, float K3, float K4, bool EforcePMColor, SKImageFilter? Background, SKImageFilter? Foreground, SKImageFilter.CropRect? Clip) : SKImageFilter;

    public record BlendModeImageFilter(SKBlendMode Mode, SKImageFilter? Background, SKImageFilter? Foreground, SKImageFilter.CropRect? Clip) : SKImageFilter;

    public record BlurImageFilter(float SigmaX, float SigmaY, SKImageFilter? Input, SKImageFilter.CropRect? Clip) : SKImageFilter;

    public record ColorFilterImageFilter(SKColorFilter? ColorFilter, SKImageFilter? Input, SKImageFilter.CropRect? Clip) : SKImageFilter;

    public record DilateImageFilter(int RadiusX, int RadiusY, SKImageFilter? Input, SKImageFilter.CropRect? Clip) : SKImageFilter;

    public record DisplacementMapEffectImageFilter(SKColorChannel XChannelSelector, SKColorChannel YChannelSelector, float Scale, SKImageFilter? Displacement, SKImageFilter? Input, SKImageFilter.CropRect? Clip) : SKImageFilter;

    public record DistantLitDiffuseImageFilter(SKPoint3 Direction, SKColor LightColor, float SurfaceScale, float Kd, SKImageFilter? Input, SKImageFilter.CropRect? Clip) : SKImageFilter;

    public record DistantLitSpecularImageFilter(SKPoint3 Direction, SKColor LightColor, float SurfaceScale, float Ks, float Shininess, SKImageFilter? Input, SKImageFilter.CropRect? Clip) : SKImageFilter;

    public record ErodeImageFilter(int RadiusX, int RadiusY, SKImageFilter? Input, SKImageFilter.CropRect? Clip) : SKImageFilter;

    public record ImageImageFilter(SKImage? Image, SKRect Src, SKRect Dst, SKFilterQuality FilterQuality) : SKImageFilter;

    public record MatrixConvolutionImageFilter(SKSizeI KernelSize, float[]? Kernel, float Gain, float Bias, SKPointI KernelOffset, SKShaderTileMode TileMode, bool ConvolveAlpha, SKImageFilter? Input, SKImageFilter.CropRect? Clip) : SKImageFilter;

    public record MergeImageFilter(SKImageFilter[]? Filters, SKImageFilter.CropRect? Clip) : SKImageFilter;

    public record OffsetImageFilter(float Dx, float Dy, SKImageFilter? Input, SKImageFilter.CropRect? Clip) : SKImageFilter;

    public record PaintImageFilter(SKPaint? Paint, SKImageFilter.CropRect? Clip) : SKImageFilter;

    public record PictureImageFilter(SKPicture? Picture, SKRect? Clip) : SKImageFilter;

    public record PointLitDiffuseImageFilter(SKPoint3 Location, SKColor LightColor, float SurfaceScale, float Kd, SKImageFilter? Input, SKImageFilter.CropRect? Clip) : SKImageFilter;

    public record PointLitSpecularImageFilter(SKPoint3 Location, SKColor LightColor, float SurfaceScale, float Ks, float Shininess, SKImageFilter? Input, SKImageFilter.CropRect? Clip) : SKImageFilter;

    public record SpotLitDiffuseImageFilter(SKPoint3 Location, SKPoint3 Target, float SpecularExponent, float CutoffAngle, SKColor LightColor, float SurfaceScale, float Kd, SKImageFilter? Input, SKImageFilter.CropRect? Clip) : SKImageFilter;

    public record SpotLitSpecularImageFilter(SKPoint3 Location, SKPoint3 Target, float SpecularExponent, float CutoffAngle, SKColor LightColor, float SurfaceScale, float Ks, float Shininess, SKImageFilter? Input, SKImageFilter.CropRect? Clip) : SKImageFilter;

    public record TileImageFilter(SKRect Src, SKRect Dst, SKImageFilter? Input) : SKImageFilter;
}
