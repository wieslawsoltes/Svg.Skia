using System;
using Svg.Model.Painting.ImageFilters;
using Svg.Model.Painting.Shaders;
using Svg.Model.Primitives;

namespace Svg.Model.Painting
{
    public sealed class CropRect
    {
        public Rect Rect { get; }

        public CropRect()
        {
        }

        public CropRect(Rect rect)
        {
            Rect = rect;
        }

        public override string ToString()
        {
            return FormattableString.Invariant($"{Rect}");
        }
    }

    public abstract class ImageFilter
    {
        public static ImageFilter CreateArithmetic(float k1, float k2, float k3, float k4, bool enforcePMColor, ImageFilter background, ImageFilter? foreground = null, CropRect? cropRect = null)
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
                CropRect = cropRect
            };
        }

        public static ImageFilter CreateBlendMode(BlendMode mode, ImageFilter background, ImageFilter? foreground = null, CropRect? cropRect = null)
        {
            return new BlendModeImageFilter
            {
                Mode = mode,
                Background = background,
                Foreground = foreground,
                CropRect = cropRect
            };
        }

        public static ImageFilter CreateBlur(float sigmaX, float sigmaY, ImageFilter? input = null, CropRect? cropRect = null)
        {
            return new BlurImageFilter
            {
                SigmaX = sigmaX,
                SigmaY = sigmaY,
                Input = input,
                CropRect = cropRect
            };
        }

        public static ImageFilter CreateColorFilter(ColorFilter cf, ImageFilter? input = null, CropRect? cropRect = null)
        {
            return new ColorFilterImageFilter
            {
                ColorFilter = cf,
                Input = input,
                CropRect = cropRect
            };
        }

        public static ImageFilter CreateDilate(int radiusX, int radiusY, ImageFilter? input = null, CropRect? cropRect = null)
        {
            return new DilateImageFilter
            {
                RadiusX = radiusX,
                RadiusY = radiusY,
                Input = input,
                CropRect = cropRect
            };
        }

        public static ImageFilter CreateDisplacementMapEffect(ColorChannel xChannelSelector, ColorChannel yChannelSelector, float scale, ImageFilter displacement, ImageFilter? input = null, CropRect? cropRect = null)
        {
            return new DisplacementMapEffectImageFilter
            {
                XChannelSelector = xChannelSelector,
                YChannelSelector = yChannelSelector,
                Scale = scale,
                Displacement = displacement,
                Input = input,
                CropRect = cropRect
            };
        }

        public static ImageFilter CreateDistantLitDiffuse(Point3 direction, Color lightColor, float surfaceScale, float kd, ImageFilter? input = null, CropRect? cropRect = null)
        {
            return new DistantLitDiffuseImageFilter
            {
                Direction = direction,
                LightColor = lightColor,
                SurfaceScale = surfaceScale,
                Kd = kd,
                Input = input,
                CropRect = cropRect
            };
        }

        public static ImageFilter CreateDistantLitSpecular(Point3 direction, Color lightColor, float surfaceScale, float ks, float shininess, ImageFilter? input = null, CropRect? cropRect = null)
        {
            return new DistantLitSpecularImageFilter
            {
                Direction = direction,
                LightColor = lightColor,
                SurfaceScale = surfaceScale,
                Ks = ks,
                Shininess = shininess,
                Input = input,
                CropRect = cropRect
            };
        }

        public static ImageFilter CreateErode(int radiusX, int radiusY, ImageFilter? input = null, CropRect? cropRect = null)
        {
            return new ErodeImageFilter
            {
                RadiusX = radiusX,
                RadiusY = radiusY,
                Input = input,
                CropRect = cropRect
            };
        }

        public static ImageFilter CreateImage(Image image, Rect src, Rect dst, FilterQuality filterQuality)
        {
            return new ImageImageFilter
            {
                Image = image,
                Src = src,
                Dst = dst,
                FilterQuality = filterQuality
            };
        }

        public static ImageFilter CreateMatrixConvolution(SizeI kernelSize, float[] kernel, float gain, float bias, PointI kernelOffset, ShaderTileMode tileMode, bool convolveAlpha, ImageFilter? input = null, CropRect? cropRect = null)
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
                CropRect = cropRect
            };
        }

        public static ImageFilter CreateMerge(ImageFilter[] filters, CropRect? cropRect = null)
        {
            return new MergeImageFilter
            {
                Filters = filters,
                CropRect = cropRect
            };
        }

        public static ImageFilter CreateOffset(float dx, float dy, ImageFilter? input = null, CropRect? cropRect = null)
        {
            return new OffsetImageFilter
            {
                Dx = dx,
                Dy = dy,
                Input = input,
                CropRect = cropRect
            };
        }

        public static ImageFilter CreatePaint(Paint paint, CropRect? cropRect = null)
        {
            return new PaintImageFilter
            {
                Paint = paint,
                CropRect = cropRect
            };
        }

        public static ImageFilter CreatePicture(Picture picture, Rect cropRect)
        {
            return new PictureImageFilter
            {
                Picture = picture,
                CropRect = cropRect
            };
        }

        public static ImageFilter CreatePointLitDiffuse(Point3 location, Color lightColor, float surfaceScale, float kd, ImageFilter? input = null, CropRect? cropRect = null)
        {
            return new PointLitDiffuseImageFilter
            {
                Location = location,
                LightColor = lightColor,
                SurfaceScale = surfaceScale,
                Kd = kd,
                Input = input,
                CropRect = cropRect
            };
        }

        public static ImageFilter CreatePointLitSpecular(Point3 location, Color lightColor, float surfaceScale, float ks, float shininess, ImageFilter? input = null, CropRect? cropRect = null)
        {
            return new PointLitSpecularImageFilter
            {
                Location = location,
                LightColor = lightColor,
                SurfaceScale = surfaceScale,
                Ks = ks,
                Shininess = shininess,
                Input = input,
                CropRect = cropRect
            };
        }

        public static ImageFilter CreateSpotLitDiffuse(Point3 location, Point3 target, float specularExponent, float cutoffAngle, Color lightColor, float surfaceScale, float kd, ImageFilter? input = null, CropRect? cropRect = null)
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
                CropRect = cropRect
            };
        }

        public static ImageFilter CreateSpotLitSpecular(Point3 location, Point3 target, float specularExponent, float cutoffAngle, Color lightColor, float surfaceScale, float ks, float shininess, ImageFilter? input = null, CropRect? cropRect = null)
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
                CropRect = cropRect
            };
        }

        public static ImageFilter CreateTile(Rect src, Rect dst, ImageFilter? input)
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
