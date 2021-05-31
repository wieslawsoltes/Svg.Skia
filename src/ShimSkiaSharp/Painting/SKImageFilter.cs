using System;
using ShimSkiaSharp.Painting.ImageFilters;
using ShimSkiaSharp.Painting.Shaders;
using ShimSkiaSharp.Primitives;

namespace ShimSkiaSharp.Painting
{
    public abstract class SKImageFilter
    {
        public sealed class CropRect
        {
            public SKRect Rect { get; }

            public CropRect()
            {
            }

            public CropRect(SKRect rect)
            {
                Rect = rect;
            }

            public override string ToString()
            {
                return FormattableString.Invariant($"{Rect}");
            }
        }

        public static SKImageFilter CreateArithmetic(float k1, float k2, float k3, float k4, bool enforcePMColor, SKImageFilter background, SKImageFilter? foreground = null, CropRect? cropRect = null)
        {
            return new ArithmeticImageFilter
            {
                K1 = k1,
                K2 = k2,
                K3 = k3,
                K4 = k4,
                EforcePMColor = enforcePMColor,
                Background = background,
                Foreground = foreground,
                Clip = cropRect
            };
        }

        public static SKImageFilter CreateBlendMode(SKBlendMode mode, SKImageFilter background, SKImageFilter? foreground = null, CropRect? cropRect = null)
        {
            return new BlendModeImageFilter
            {
                Mode = mode,
                Background = background,
                Foreground = foreground,
                Clip = cropRect
            };
        }

        public static SKImageFilter CreateBlur(float sigmaX, float sigmaY, SKImageFilter? input = null, CropRect? cropRect = null)
        {
            return new BlurImageFilter
            {
                SigmaX = sigmaX,
                SigmaY = sigmaY,
                Input = input,
                Clip = cropRect
            };
        }

        public static SKImageFilter CreateColorFilter(SKColorFilter cf, SKImageFilter? input = null, CropRect? cropRect = null)
        {
            return new ColorFilterImageFilter
            {
                ColorFilter = cf,
                Input = input,
                Clip = cropRect
            };
        }

        public static SKImageFilter CreateDilate(int radiusX, int radiusY, SKImageFilter? input = null, CropRect? cropRect = null)
        {
            return new DilateImageFilter
            {
                RadiusX = radiusX,
                RadiusY = radiusY,
                Input = input,
                Clip = cropRect
            };
        }

        public static SKImageFilter CreateDisplacementMapEffect(SKColorChannel xChannelSelector, SKColorChannel yChannelSelector, float scale, SKImageFilter displacement, SKImageFilter? input = null, CropRect? cropRect = null)
        {
            return new DisplacementMapEffectImageFilter
            {
                XChannelSelector = xChannelSelector,
                YChannelSelector = yChannelSelector,
                Scale = scale,
                Displacement = displacement,
                Input = input,
                Clip = cropRect
            };
        }

        public static SKImageFilter CreateDistantLitDiffuse(SKPoint3 direction, SKColor lightColor, float surfaceScale, float kd, SKImageFilter? input = null, CropRect? cropRect = null)
        {
            return new DistantLitDiffuseImageFilter
            {
                Direction = direction,
                LightColor = lightColor,
                SurfaceScale = surfaceScale,
                Kd = kd,
                Input = input,
                Clip = cropRect
            };
        }

        public static SKImageFilter CreateDistantLitSpecular(SKPoint3 direction, SKColor lightColor, float surfaceScale, float ks, float shininess, SKImageFilter? input = null, CropRect? cropRect = null)
        {
            return new DistantLitSpecularImageFilter
            {
                Direction = direction,
                LightColor = lightColor,
                SurfaceScale = surfaceScale,
                Ks = ks,
                Shininess = shininess,
                Input = input,
                Clip = cropRect
            };
        }

        public static SKImageFilter CreateErode(int radiusX, int radiusY, SKImageFilter? input = null, CropRect? cropRect = null)
        {
            return new ErodeImageFilter
            {
                RadiusX = radiusX,
                RadiusY = radiusY,
                Input = input,
                Clip = cropRect
            };
        }

        public static SKImageFilter CreateImage(SKImage image, SKRect src, SKRect dst, SKFilterQuality filterQuality)
        {
            return new ImageImageFilter
            {
                Image = image,
                Src = src,
                Dst = dst,
                FilterQuality = filterQuality
            };
        }

        public static SKImageFilter CreateMatrixConvolution(SKSizeI kernelSize, float[] kernel, float gain, float bias, SKPointI kernelOffset, SKShaderTileMode tileMode, bool convolveAlpha, SKImageFilter? input = null, CropRect? cropRect = null)
        {
            return new MatrixConvolutionImageFilter
            {
                KernelSize = kernelSize,
                Kernel = kernel,
                Gain = gain,
                Bias = bias,
                KernelOffset = kernelOffset,
                TileMode = tileMode,
                ConvolveAlpha = convolveAlpha,
                Input = input,
                Clip = cropRect
            };
        }

        public static SKImageFilter CreateMerge(SKImageFilter[] filters, CropRect? cropRect = null)
        {
            return new MergeImageFilter
            {
                Filters = filters,
                Clip = cropRect
            };
        }

        public static SKImageFilter CreateOffset(float dx, float dy, SKImageFilter? input = null, CropRect? cropRect = null)
        {
            return new OffsetImageFilter
            {
                Dx = dx,
                Dy = dy,
                Input = input,
                Clip = cropRect
            };
        }

        public static SKImageFilter CreatePaint(SKPaint paint, CropRect? cropRect = null)
        {
            return new PaintImageFilter
            {
                Paint = paint,
                Clip = cropRect
            };
        }

        public static SKImageFilter CreatePicture(SKPicture picture, SKRect cropRect)
        {
            return new PictureImageFilter
            {
                Picture = picture,
                Clip = cropRect
            };
        }

        public static SKImageFilter CreatePointLitDiffuse(SKPoint3 location, SKColor lightColor, float surfaceScale, float kd, SKImageFilter? input = null, CropRect? cropRect = null)
        {
            return new PointLitDiffuseImageFilter
            {
                Location = location,
                LightColor = lightColor,
                SurfaceScale = surfaceScale,
                Kd = kd,
                Input = input,
                Clip = cropRect
            };
        }

        public static SKImageFilter CreatePointLitSpecular(SKPoint3 location, SKColor lightColor, float surfaceScale, float ks, float shininess, SKImageFilter? input = null, CropRect? cropRect = null)
        {
            return new PointLitSpecularImageFilter
            {
                Location = location,
                LightColor = lightColor,
                SurfaceScale = surfaceScale,
                Ks = ks,
                Shininess = shininess,
                Input = input,
                Clip = cropRect
            };
        }

        public static SKImageFilter CreateSpotLitDiffuse(SKPoint3 location, SKPoint3 target, float specularExponent, float cutoffAngle, SKColor lightColor, float surfaceScale, float kd, SKImageFilter? input = null, CropRect? cropRect = null)
        {
            return new SpotLitDiffuseImageFilter
            {
                Location = location,
                Target = target,
                SpecularExponent = specularExponent,
                CutoffAngle = cutoffAngle,
                LightColor = lightColor,
                SurfaceScale = surfaceScale,
                Kd = kd,
                Input = input,
                Clip = cropRect
            };
        }

        public static SKImageFilter CreateSpotLitSpecular(SKPoint3 location, SKPoint3 target, float specularExponent, float cutoffAngle, SKColor lightColor, float surfaceScale, float ks, float shininess, SKImageFilter? input = null, CropRect? cropRect = null)
        {
            return new SpotLitSpecularImageFilter
            {
                Location = location,
                Target = target,
                SpecularExponent = specularExponent,
                CutoffAngle = cutoffAngle,
                LightColor = lightColor,
                SurfaceScale = surfaceScale,
                Ks = ks,
                Shininess = shininess,
                Input = input,
                Clip = cropRect
            };
        }

        public static SKImageFilter CreateTile(SKRect src, SKRect dst, SKImageFilter? input)
        {
            return new TileImageFilter
            {
                Src = src,
                Dst = dst,
                Input = input
            };
        }
    }
}
