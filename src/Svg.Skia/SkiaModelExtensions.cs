using System.Collections.Generic;
using ShimSkiaSharp.Painting;
using ShimSkiaSharp.Painting.ColorFilters;
using ShimSkiaSharp.Painting.ImageFilters;
using ShimSkiaSharp.Painting.PathEffects;
using ShimSkiaSharp.Painting.Shaders;
using ShimSkiaSharp.Primitives;
using ShimSkiaSharp.Primitives.CanvasCommands;
using ShimSkiaSharp.Primitives.PathCommands;

namespace Svg.Skia
{
    public static class SkiaModelExtensions
    {
        public static SkiaSharp.SKPoint ToSKPoint(this SKPoint point)
        {
            return new(point.X, point.Y);
        }

        public static SkiaSharp.SKPoint3 ToSKPoint3(this SKPoint3 point3)
        {
            return new(point3.X, point3.Y, point3.Z);
        }

        public static SkiaSharp.SKPoint[] ToSKPoints(this IList<SKPoint> points)
        {
            var skPoints = new SkiaSharp.SKPoint[points.Count];

            for (var i = 0; i < points.Count; i++) skPoints[i] = points[i].ToSKPoint();

            return skPoints;
        }

        public static SkiaSharp.SKPointI ToSKPointI(this SKPointI pointI)
        {
            return new(pointI.X, pointI.Y);
        }

        public static SkiaSharp.SKSize ToSKSize(this SKSize size)
        {
            return new(size.Width, size.Height);
        }

        public static SkiaSharp.SKSizeI ToSKSizeI(this SKSizeI sizeI)
        {
            return new(sizeI.Width, sizeI.Height);
        }

        public static SkiaSharp.SKRect ToSKRect(this SKRect rect)
        {
            return new(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }

        public static SkiaSharp.SKMatrix ToSKMatrix(this SKMatrix matrix)
        {
            return new(
                matrix.ScaleX,
                matrix.SkewX,
                matrix.TransX,
                matrix.SkewY,
                matrix.ScaleY,
                matrix.TransY,
                matrix.Persp0,
                matrix.Persp1,
                matrix.Persp2);
        }

        public static SkiaSharp.SKImage ToSKImage(this SKImage image)
        {
            return SkiaSharp.SKImage.FromEncodedData(image.Data);
        }

        public static SkiaSharp.SKPaintStyle ToSKPaintStyle(this SKPaintStyle paintStyle)
        {
            return paintStyle switch
            {
                SKPaintStyle.Fill => SkiaSharp.SKPaintStyle.Fill,
                SKPaintStyle.Stroke => SkiaSharp.SKPaintStyle.Stroke,
                SKPaintStyle.StrokeAndFill => SkiaSharp.SKPaintStyle.StrokeAndFill,
                _ => SkiaSharp.SKPaintStyle.Fill
            };
        }

        public static SkiaSharp.SKStrokeCap ToSKStrokeCap(this SKStrokeCap strokeCap)
        {
            return strokeCap switch
            {
                SKStrokeCap.Butt => SkiaSharp.SKStrokeCap.Butt,
                SKStrokeCap.Round => SkiaSharp.SKStrokeCap.Round,
                SKStrokeCap.Square => SkiaSharp.SKStrokeCap.Square,
                _ => SkiaSharp.SKStrokeCap.Butt
            };
        }

        public static SkiaSharp.SKStrokeJoin ToSKStrokeJoin(this SKStrokeJoin strokeJoin)
        {
            return strokeJoin switch
            {
                SKStrokeJoin.Miter => SkiaSharp.SKStrokeJoin.Miter,
                SKStrokeJoin.Round => SkiaSharp.SKStrokeJoin.Round,
                SKStrokeJoin.Bevel => SkiaSharp.SKStrokeJoin.Bevel,
                _ => SkiaSharp.SKStrokeJoin.Miter
            };
        }

        public static SkiaSharp.SKTextAlign ToSKTextAlign(this SKTextAlign textAlign)
        {
            return textAlign switch
            {
                SKTextAlign.Left => SkiaSharp.SKTextAlign.Left,
                SKTextAlign.Center => SkiaSharp.SKTextAlign.Center,
                SKTextAlign.Right => SkiaSharp.SKTextAlign.Right,
                _ => SkiaSharp.SKTextAlign.Left
            };
        }

        public static SkiaSharp.SKTextEncoding ToSKTextEncoding(this SKTextEncoding textEncoding)
        {
            return textEncoding switch
            {
                SKTextEncoding.Utf8 => SkiaSharp.SKTextEncoding.Utf8,
                SKTextEncoding.Utf16 => SkiaSharp.SKTextEncoding.Utf16,
                SKTextEncoding.Utf32 => SkiaSharp.SKTextEncoding.Utf32,
                SKTextEncoding.GlyphId => SkiaSharp.SKTextEncoding.GlyphId,
                _ => SkiaSharp.SKTextEncoding.Utf8
            };
        }

        public static SkiaSharp.SKFontStyleWeight ToSKFontStyleWeight(this SKFontStyleWeight fontStyleWeight)
        {
            return fontStyleWeight switch
            {
                SKFontStyleWeight.Invisible => SkiaSharp.SKFontStyleWeight.Invisible,
                SKFontStyleWeight.Thin => SkiaSharp.SKFontStyleWeight.Thin,
                SKFontStyleWeight.ExtraLight => SkiaSharp.SKFontStyleWeight.ExtraLight,
                SKFontStyleWeight.Light => SkiaSharp.SKFontStyleWeight.Light,
                SKFontStyleWeight.Normal => SkiaSharp.SKFontStyleWeight.Normal,
                SKFontStyleWeight.Medium => SkiaSharp.SKFontStyleWeight.Medium,
                SKFontStyleWeight.SemiBold => SkiaSharp.SKFontStyleWeight.SemiBold,
                SKFontStyleWeight.Bold => SkiaSharp.SKFontStyleWeight.Bold,
                SKFontStyleWeight.ExtraBold => SkiaSharp.SKFontStyleWeight.ExtraBold,
                SKFontStyleWeight.Black => SkiaSharp.SKFontStyleWeight.Black,
                SKFontStyleWeight.ExtraBlack => SkiaSharp.SKFontStyleWeight.ExtraBlack,
                _ => SkiaSharp.SKFontStyleWeight.Invisible
            };
        }

        public static SkiaSharp.SKFontStyleWidth ToSKFontStyleWidth(this SKFontStyleWidth fontStyleWidth)
        {
            return fontStyleWidth switch
            {
                SKFontStyleWidth.UltraCondensed => SkiaSharp.SKFontStyleWidth.UltraCondensed,
                SKFontStyleWidth.ExtraCondensed => SkiaSharp.SKFontStyleWidth.ExtraCondensed,
                SKFontStyleWidth.Condensed => SkiaSharp.SKFontStyleWidth.Condensed,
                SKFontStyleWidth.SemiCondensed => SkiaSharp.SKFontStyleWidth.SemiCondensed,
                SKFontStyleWidth.Normal => SkiaSharp.SKFontStyleWidth.Normal,
                SKFontStyleWidth.SemiExpanded => SkiaSharp.SKFontStyleWidth.SemiExpanded,
                SKFontStyleWidth.Expanded => SkiaSharp.SKFontStyleWidth.Expanded,
                SKFontStyleWidth.ExtraExpanded => SkiaSharp.SKFontStyleWidth.ExtraExpanded,
                SKFontStyleWidth.UltraExpanded => SkiaSharp.SKFontStyleWidth.UltraExpanded,
                _ => SkiaSharp.SKFontStyleWidth.UltraCondensed
            };
        }

        public static SkiaSharp.SKFontStyleSlant ToSKFontStyleSlant(this SKFontStyleSlant fontStyleSlant)
        {
            return fontStyleSlant switch
            {
                SKFontStyleSlant.Upright => SkiaSharp.SKFontStyleSlant.Upright,
                SKFontStyleSlant.Italic => SkiaSharp.SKFontStyleSlant.Italic,
                SKFontStyleSlant.Oblique => SkiaSharp.SKFontStyleSlant.Oblique,
                _ => SkiaSharp.SKFontStyleSlant.Upright
            };
        }

        public static SkiaSharp.SKTypeface? ToSKTypeface(this SKTypeface? typeface)
        {
            if (typeface is null || typeface.FamilyName is null) return SkiaSharp.SKTypeface.Default;

            var fontFamily = typeface.FamilyName;
            var fontWeight = typeface.FontWeight.ToSKFontStyleWeight();
            var fontWidth = typeface.FontWidth.ToSKFontStyleWidth();
            var fontStyle = typeface.Style.ToSKFontStyleSlant();

            if (SKSvgSettings.s_typefaceProviders is { } && SKSvgSettings.s_typefaceProviders.Count > 0)
                foreach (var typefaceProviders in SKSvgSettings.s_typefaceProviders)
                {
                    var skTypeface = typefaceProviders.FromFamilyName(fontFamily, fontWeight, fontWidth, fontStyle);
                    if (skTypeface is { }) return skTypeface;
                }

            return SkiaSharp.SKTypeface.FromFamilyName(fontFamily, fontWeight, fontWidth, fontStyle);
        }

        public static SkiaSharp.SKColor ToSKColor(this SKColor color)
        {
            return new(color.Red, color.Green, color.Blue, color.Alpha);
        }

        public static SkiaSharp.SKColor[] ToSKColors(this SKColor[] colors)
        {
            var skColors = new SkiaSharp.SKColor[colors.Length];

            for (var i = 0; i < colors.Length; i++) skColors[i] = colors[i].ToSKColor();

            return skColors;
        }

        public static SkiaSharp.SKColorF ToSKColor(this SKColorF color)
        {
            return new(color.Red, color.Green, color.Blue, color.Alpha);
        }

        public static SkiaSharp.SKColorF[] ToSKColors(this SKColorF[] colors)
        {
            var skColors = new SkiaSharp.SKColorF[colors.Length];

            for (var i = 0; i < colors.Length; i++) skColors[i] = colors[i].ToSKColor();

            return skColors;
        }

        public static SkiaSharp.SKShaderTileMode ToSKShaderTileMode(this SKShaderTileMode shaderTileMode)
        {
            return shaderTileMode switch
            {
                SKShaderTileMode.Clamp => SkiaSharp.SKShaderTileMode.Clamp,
                SKShaderTileMode.Repeat => SkiaSharp.SKShaderTileMode.Repeat,
                SKShaderTileMode.Mirror => SkiaSharp.SKShaderTileMode.Mirror,
                SKShaderTileMode.Decal => SkiaSharp.SKShaderTileMode.Decal,
                _ => SkiaSharp.SKShaderTileMode.Clamp
            };
        }

        public static SkiaSharp.SKShader? ToSKShader(this SKShader? shader)
        {
            switch (shader)
            {
                case ColorShader colorShader:
                    {
                        return SkiaSharp.SKShader.CreateColor(
                            colorShader.Color.ToSKColor(),
                            colorShader.ColorSpace == SKColorSpace.Srgb ? SKSvgSettings.s_srgb : SKSvgSettings.s_srgbLinear);
                    }
                case LinearGradientShader linearGradientShader:
                    {
                        if (linearGradientShader.Colors is null || linearGradientShader.ColorPos is null) return null;

                        if (linearGradientShader.LocalMatrix is { })
                            return SkiaSharp.SKShader.CreateLinearGradient(
                                linearGradientShader.Start.ToSKPoint(),
                                linearGradientShader.End.ToSKPoint(),
                                linearGradientShader.Colors.ToSKColors(),
                                linearGradientShader.ColorSpace == SKColorSpace.Srgb ? SKSvgSettings.s_srgb : SKSvgSettings.s_srgbLinear,
                                linearGradientShader.ColorPos,
                                linearGradientShader.Mode.ToSKShaderTileMode(),
                                linearGradientShader.LocalMatrix.Value.ToSKMatrix());

                        return SkiaSharp.SKShader.CreateLinearGradient(
                            linearGradientShader.Start.ToSKPoint(),
                            linearGradientShader.End.ToSKPoint(),
                            linearGradientShader.Colors.ToSKColors(),
                            linearGradientShader.ColorSpace == SKColorSpace.Srgb ? SKSvgSettings.s_srgb : SKSvgSettings.s_srgbLinear,
                            linearGradientShader.ColorPos,
                            linearGradientShader.Mode.ToSKShaderTileMode());
                    }
                case TwoPointConicalGradientShader twoPointConicalGradientShader:
                    {
                        if (twoPointConicalGradientShader.Colors is null || twoPointConicalGradientShader.ColorPos is null) return null;

                        if (twoPointConicalGradientShader.LocalMatrix is { })
                            return SkiaSharp.SKShader.CreateTwoPointConicalGradient(
                                twoPointConicalGradientShader.Start.ToSKPoint(),
                                twoPointConicalGradientShader.StartRadius,
                                twoPointConicalGradientShader.End.ToSKPoint(),
                                twoPointConicalGradientShader.EndRadius,
                                twoPointConicalGradientShader.Colors.ToSKColors(),
                                twoPointConicalGradientShader.ColorSpace == SKColorSpace.Srgb ? SKSvgSettings.s_srgb : SKSvgSettings.s_srgbLinear,
                                twoPointConicalGradientShader.ColorPos,
                                twoPointConicalGradientShader.Mode.ToSKShaderTileMode(),
                                twoPointConicalGradientShader.LocalMatrix.Value.ToSKMatrix());

                        return SkiaSharp.SKShader.CreateTwoPointConicalGradient(
                            twoPointConicalGradientShader.Start.ToSKPoint(),
                            twoPointConicalGradientShader.StartRadius,
                            twoPointConicalGradientShader.End.ToSKPoint(),
                            twoPointConicalGradientShader.EndRadius,
                            twoPointConicalGradientShader.Colors.ToSKColors(),
                            twoPointConicalGradientShader.ColorSpace == SKColorSpace.Srgb ? SKSvgSettings.s_srgb : SKSvgSettings.s_srgbLinear,
                            twoPointConicalGradientShader.ColorPos,
                            twoPointConicalGradientShader.Mode.ToSKShaderTileMode());
                    }
                case PictureShader pictureShader:
                    {
                        if (pictureShader.Src is null) return null;

                        return SkiaSharp.SKShader.CreatePicture(
                            pictureShader.Src.ToSKPicture(),
                            SkiaSharp.SKShaderTileMode.Repeat,
                            SkiaSharp.SKShaderTileMode.Repeat,
                            pictureShader.LocalMatrix.ToSKMatrix(),
                            pictureShader.Tile.ToSKRect());
                    }
                case PerlinNoiseFractalNoiseShader perlinNoiseFractalNoiseShader:
                    {
                        return SkiaSharp.SKShader.CreatePerlinNoiseFractalNoise(
                            perlinNoiseFractalNoiseShader.BaseFrequencyX,
                            perlinNoiseFractalNoiseShader.BaseFrequencyY,
                            perlinNoiseFractalNoiseShader.NumOctaves,
                            perlinNoiseFractalNoiseShader.Seed,
                            perlinNoiseFractalNoiseShader.TileSize.ToSKPointI());
                    }
                case PerlinNoiseTurbulenceShader perlinNoiseTurbulenceShader:
                    {
                        return SkiaSharp.SKShader.CreatePerlinNoiseTurbulence(
                            perlinNoiseTurbulenceShader.BaseFrequencyX,
                            perlinNoiseTurbulenceShader.BaseFrequencyY,
                            perlinNoiseTurbulenceShader.NumOctaves,
                            perlinNoiseTurbulenceShader.Seed,
                            perlinNoiseTurbulenceShader.TileSize.ToSKPointI());
                    }
                default:
                    return null;
            }
        }

        public static SkiaSharp.SKColorFilter? ToSKColorFilter(this SKColorFilter? colorFilter)
        {
            switch (colorFilter)
            {
                case BlendModeColorFilter blendModeColorFilter:
                    {
                        return SkiaSharp.SKColorFilter.CreateBlendMode(
                            blendModeColorFilter.Color.ToSKColor(),
                            blendModeColorFilter.Mode.ToSKBlendMode());
                    }
                case ColorMatrixColorFilter colorMatrixColorFilter:
                    {
                        if (colorMatrixColorFilter.Matrix is null) return null;
                        return SkiaSharp.SKColorFilter.CreateColorMatrix(colorMatrixColorFilter.Matrix);
                    }
                case LumaColorColorFilter _:
                    {
                        return SkiaSharp.SKColorFilter.CreateLumaColor();
                    }
                case TableColorFilter tableColorFilter:
                    {
                        if (tableColorFilter.TableA is null
                            || tableColorFilter.TableR is null
                            || tableColorFilter.TableG is null
                            || tableColorFilter.TableB is null)
                            return null;
                        return SkiaSharp.SKColorFilter.CreateTable(
                            tableColorFilter.TableA,
                            tableColorFilter.TableR,
                            tableColorFilter.TableG,
                            tableColorFilter.TableB);
                    }
                default:
                    return null;
            }
        }

        public static SkiaSharp.SKImageFilter.CropRect ToCropRect(this SKImageFilter.CropRect cropRect)
        {
            return new(cropRect.Rect.ToSKRect());
        }

        public static SkiaSharp.SKColorChannel ToSKColorChannel(this SKColorChannel colorChannel)
        {
            return colorChannel switch
            {
                SKColorChannel.R => SkiaSharp.SKColorChannel.R,
                SKColorChannel.G => SkiaSharp.SKColorChannel.G,
                SKColorChannel.B => SkiaSharp.SKColorChannel.B,
                SKColorChannel.A => SkiaSharp.SKColorChannel.A,
                _ => SkiaSharp.SKColorChannel.R
            };
        }

        public static SkiaSharp.SKImageFilter? ToSKImageFilter(this SKImageFilter? imageFilter)
        {
            switch (imageFilter)
            {
                case ArithmeticImageFilter arithmeticImageFilter:
                    {
                        if (arithmeticImageFilter.Background is null) return null;

                        return SkiaSharp.SKImageFilter.CreateArithmetic(
                            arithmeticImageFilter.K1,
                            arithmeticImageFilter.K2,
                            arithmeticImageFilter.K3,
                            arithmeticImageFilter.K4,
                            arithmeticImageFilter.EforcePMColor,
                            arithmeticImageFilter.Background?.ToSKImageFilter(),
                            arithmeticImageFilter.Foreground?.ToSKImageFilter(),
                            arithmeticImageFilter.CropRect?.ToCropRect());
                    }
                case BlendModeImageFilter blendModeImageFilter:
                    {
                        if (blendModeImageFilter.Background is null) return null;

                        return SkiaSharp.SKImageFilter.CreateBlendMode(
                            blendModeImageFilter.Mode.ToSKBlendMode(),
                            blendModeImageFilter.Background?.ToSKImageFilter(),
                            blendModeImageFilter.Foreground?.ToSKImageFilter(),
                            blendModeImageFilter.CropRect?.ToCropRect());
                    }
                case BlurImageFilter blurImageFilter:
                    {
                        return SkiaSharp.SKImageFilter.CreateBlur(
                            blurImageFilter.SigmaX,
                            blurImageFilter.SigmaY,
                            blurImageFilter.Input?.ToSKImageFilter(),
                            blurImageFilter.CropRect?.ToCropRect());
                    }
                case ColorFilterImageFilter colorFilterImageFilter:
                    {
                        if (colorFilterImageFilter.ColorFilter is null) return null;

                        return SkiaSharp.SKImageFilter.CreateColorFilter(
                            colorFilterImageFilter.ColorFilter?.ToSKColorFilter(),
                            colorFilterImageFilter.Input?.ToSKImageFilter(),
                            colorFilterImageFilter.CropRect?.ToCropRect());
                    }
                case DilateImageFilter dilateImageFilter:
                    {
                        return SkiaSharp.SKImageFilter.CreateDilate(
                            dilateImageFilter.RadiusX,
                            dilateImageFilter.RadiusY,
                            dilateImageFilter.Input?.ToSKImageFilter(),
                            dilateImageFilter.CropRect?.ToCropRect());
                    }
                case DisplacementMapEffectImageFilter displacementMapEffectImageFilter:
                    {
                        if (displacementMapEffectImageFilter.Displacement is null) return null;

                        return SkiaSharp.SKImageFilter.CreateDisplacementMapEffect(
                            displacementMapEffectImageFilter.XChannelSelector.ToSKColorChannel(),
                            displacementMapEffectImageFilter.YChannelSelector.ToSKColorChannel(),
                            displacementMapEffectImageFilter.Scale,
                            displacementMapEffectImageFilter.Displacement?.ToSKImageFilter(),
                            displacementMapEffectImageFilter.Input?.ToSKImageFilter(),
                            displacementMapEffectImageFilter.CropRect?.ToCropRect());
                    }
                case DistantLitDiffuseImageFilter distantLitDiffuseImageFilter:
                    {
                        return SkiaSharp.SKImageFilter.CreateDistantLitDiffuse(
                            distantLitDiffuseImageFilter.Direction.ToSKPoint3(),
                            distantLitDiffuseImageFilter.LightColor.ToSKColor(),
                            distantLitDiffuseImageFilter.SurfaceScale,
                            distantLitDiffuseImageFilter.Kd,
                            distantLitDiffuseImageFilter.Input?.ToSKImageFilter(),
                            distantLitDiffuseImageFilter.CropRect?.ToCropRect());
                    }
                case DistantLitSpecularImageFilter distantLitSpecularImageFilter:
                    {
                        return SkiaSharp.SKImageFilter.CreateDistantLitSpecular(
                            distantLitSpecularImageFilter.Direction.ToSKPoint3(),
                            distantLitSpecularImageFilter.LightColor.ToSKColor(),
                            distantLitSpecularImageFilter.SurfaceScale,
                            distantLitSpecularImageFilter.Ks,
                            distantLitSpecularImageFilter.Shininess,
                            distantLitSpecularImageFilter.Input?.ToSKImageFilter(),
                            distantLitSpecularImageFilter.CropRect?.ToCropRect());
                    }
                case ErodeImageFilter erodeImageFilter:
                    {
                        return SkiaSharp.SKImageFilter.CreateErode(
                            erodeImageFilter.RadiusX,
                            erodeImageFilter.RadiusY,
                            erodeImageFilter.Input?.ToSKImageFilter(),
                            erodeImageFilter.CropRect?.ToCropRect());
                    }
                case ImageImageFilter imageImageFilter:
                    {
                        if (imageImageFilter.Image is null) return null;

                        return SkiaSharp.SKImageFilter.CreateImage(
                            imageImageFilter.Image.ToSKImage(),
                            imageImageFilter.Src.ToSKRect(),
                            imageImageFilter.Dst.ToSKRect(),
                            SkiaSharp.SKFilterQuality.High);
                    }
                case MatrixConvolutionImageFilter matrixConvolutionImageFilter:
                    {
                        if (matrixConvolutionImageFilter.Kernel is null) return null;

                        return SkiaSharp.SKImageFilter.CreateMatrixConvolution(
                            matrixConvolutionImageFilter.KernelSize.ToSKSizeI(),
                            matrixConvolutionImageFilter.Kernel,
                            matrixConvolutionImageFilter.Gain,
                            matrixConvolutionImageFilter.Bias,
                            matrixConvolutionImageFilter.KernelOffset.ToSKPointI(),
                            matrixConvolutionImageFilter.TileMode.ToSKShaderTileMode(),
                            matrixConvolutionImageFilter.ConvolveAlpha,
                            matrixConvolutionImageFilter.Input?.ToSKImageFilter(),
                            matrixConvolutionImageFilter.CropRect?.ToCropRect());
                    }
                case MergeImageFilter mergeImageFilter:
                    {
                        if (mergeImageFilter.Filters is null) return null;

                        return SkiaSharp.SKImageFilter.CreateMerge(
                            mergeImageFilter.Filters?.ToSKImageFilters(),
                            mergeImageFilter.CropRect?.ToCropRect());
                    }
                case OffsetImageFilter offsetImageFilter:
                    {
                        return SkiaSharp.SKImageFilter.CreateOffset(
                            offsetImageFilter.Dx,
                            offsetImageFilter.Dy,
                            offsetImageFilter.Input?.ToSKImageFilter(),
                            offsetImageFilter.CropRect?.ToCropRect());
                    }
                case PaintImageFilter paintImageFilter:
                    {
                        if (paintImageFilter.Paint is null) return null;

                        return SkiaSharp.SKImageFilter.CreatePaint(
                            paintImageFilter.Paint.ToSKPaint(),
                            paintImageFilter.CropRect?.ToCropRect());
                    }
                case PictureImageFilter pictureImageFilter:
                    {
                        if (pictureImageFilter.Picture is null) return null;

                        return SkiaSharp.SKImageFilter.CreatePicture(
                            pictureImageFilter.Picture.ToSKPicture(),
                            pictureImageFilter.Picture.CullRect.ToSKRect());
                    }
                case PointLitDiffuseImageFilter pointLitDiffuseImageFilter:
                    {
                        return SkiaSharp.SKImageFilter.CreatePointLitDiffuse(
                            pointLitDiffuseImageFilter.Location.ToSKPoint3(),
                            pointLitDiffuseImageFilter.LightColor.ToSKColor(),
                            pointLitDiffuseImageFilter.SurfaceScale,
                            pointLitDiffuseImageFilter.Kd,
                            pointLitDiffuseImageFilter.Input?.ToSKImageFilter(),
                            pointLitDiffuseImageFilter.CropRect?.ToCropRect());
                    }
                case PointLitSpecularImageFilter pointLitSpecularImageFilter:
                    {
                        return SkiaSharp.SKImageFilter.CreatePointLitSpecular(
                            pointLitSpecularImageFilter.Location.ToSKPoint3(),
                            pointLitSpecularImageFilter.LightColor.ToSKColor(),
                            pointLitSpecularImageFilter.SurfaceScale,
                            pointLitSpecularImageFilter.Ks,
                            pointLitSpecularImageFilter.Shininess,
                            pointLitSpecularImageFilter.Input?.ToSKImageFilter(),
                            pointLitSpecularImageFilter.CropRect?.ToCropRect());
                    }
                case SpotLitDiffuseImageFilter spotLitDiffuseImageFilter:
                    {
                        return SkiaSharp.SKImageFilter.CreateSpotLitDiffuse(
                            spotLitDiffuseImageFilter.Location.ToSKPoint3(),
                            spotLitDiffuseImageFilter.Target.ToSKPoint3(),
                            spotLitDiffuseImageFilter.SpecularExponent,
                            spotLitDiffuseImageFilter.CutoffAngle,
                            spotLitDiffuseImageFilter.LightColor.ToSKColor(),
                            spotLitDiffuseImageFilter.SurfaceScale,
                            spotLitDiffuseImageFilter.Kd,
                            spotLitDiffuseImageFilter.Input?.ToSKImageFilter(),
                            spotLitDiffuseImageFilter.CropRect?.ToCropRect());
                    }
                case SpotLitSpecularImageFilter spotLitSpecularImageFilter:
                    {
                        return SkiaSharp.SKImageFilter.CreateSpotLitSpecular(
                            spotLitSpecularImageFilter.Location.ToSKPoint3(),
                            spotLitSpecularImageFilter.Target.ToSKPoint3(),
                            spotLitSpecularImageFilter.SpecularExponent,
                            spotLitSpecularImageFilter.CutoffAngle,
                            spotLitSpecularImageFilter.LightColor.ToSKColor(),
                            spotLitSpecularImageFilter.SurfaceScale,
                            spotLitSpecularImageFilter.Ks,
                            spotLitSpecularImageFilter.SpecularExponent,
                            spotLitSpecularImageFilter.Input?.ToSKImageFilter(),
                            spotLitSpecularImageFilter.CropRect?.ToCropRect());
                    }
                case TileImageFilter tileImageFilter:
                    {
                        return SkiaSharp.SKImageFilter.CreateTile(
                            tileImageFilter.Src.ToSKRect(),
                            tileImageFilter.Dst.ToSKRect(),
                            tileImageFilter.Input?.ToSKImageFilter());
                    }
                default:
                    return null;
            }
        }

        public static SkiaSharp.SKImageFilter[]? ToSKImageFilters(this SKImageFilter[]? imageFilters)
        {
            if (imageFilters is null) return null;

            var skImageFilters = new SkiaSharp.SKImageFilter[imageFilters.Length];

            for (var i = 0; i < imageFilters.Length; i++)
            {
                var imageFilter = imageFilters[i];
                var skImageFilter = imageFilter.ToSKImageFilter();
                if (skImageFilter is { }) skImageFilters[i] = skImageFilter;
            }

            return skImageFilters;
        }

        public static SkiaSharp.SKPathEffect? ToSKPathEffect(this SKPathEffect? pathEffect)
        {
            switch (pathEffect)
            {
                case DashPathEffect dashPathEffect:
                    {
                        return SkiaSharp.SKPathEffect.CreateDash(
                            dashPathEffect.Intervals,
                            dashPathEffect.Phase);
                    }
                default:
                    return null;
            }
        }

        public static SkiaSharp.SKBlendMode ToSKBlendMode(this SKBlendMode blendMode)
        {
            return blendMode switch
            {
                SKBlendMode.Clear => SkiaSharp.SKBlendMode.Clear,
                SKBlendMode.Src => SkiaSharp.SKBlendMode.Src,
                SKBlendMode.Dst => SkiaSharp.SKBlendMode.Dst,
                SKBlendMode.SrcOver => SkiaSharp.SKBlendMode.SrcOver,
                SKBlendMode.DstOver => SkiaSharp.SKBlendMode.DstOver,
                SKBlendMode.SrcIn => SkiaSharp.SKBlendMode.SrcIn,
                SKBlendMode.DstIn => SkiaSharp.SKBlendMode.DstIn,
                SKBlendMode.SrcOut => SkiaSharp.SKBlendMode.SrcOut,
                SKBlendMode.DstOut => SkiaSharp.SKBlendMode.DstOut,
                SKBlendMode.SrcATop => SkiaSharp.SKBlendMode.SrcATop,
                SKBlendMode.DstATop => SkiaSharp.SKBlendMode.DstATop,
                SKBlendMode.Xor => SkiaSharp.SKBlendMode.Xor,
                SKBlendMode.Plus => SkiaSharp.SKBlendMode.Plus,
                SKBlendMode.Modulate => SkiaSharp.SKBlendMode.Modulate,
                SKBlendMode.Screen => SkiaSharp.SKBlendMode.Screen,
                SKBlendMode.Overlay => SkiaSharp.SKBlendMode.Overlay,
                SKBlendMode.Darken => SkiaSharp.SKBlendMode.Darken,
                SKBlendMode.Lighten => SkiaSharp.SKBlendMode.Lighten,
                SKBlendMode.ColorDodge => SkiaSharp.SKBlendMode.ColorDodge,
                SKBlendMode.ColorBurn => SkiaSharp.SKBlendMode.ColorBurn,
                SKBlendMode.HardLight => SkiaSharp.SKBlendMode.HardLight,
                SKBlendMode.SoftLight => SkiaSharp.SKBlendMode.SoftLight,
                SKBlendMode.Difference => SkiaSharp.SKBlendMode.Difference,
                SKBlendMode.Exclusion => SkiaSharp.SKBlendMode.Exclusion,
                SKBlendMode.Multiply => SkiaSharp.SKBlendMode.Multiply,
                SKBlendMode.Hue => SkiaSharp.SKBlendMode.Hue,
                SKBlendMode.Saturation => SkiaSharp.SKBlendMode.Saturation,
                SKBlendMode.Color => SkiaSharp.SKBlendMode.Color,
                SKBlendMode.Luminosity => SkiaSharp.SKBlendMode.Luminosity,
                _ => SkiaSharp.SKBlendMode.Clear
            };
        }

        public static SkiaSharp.SKFilterQuality ToSKFilterQuality(this SKFilterQuality filterQuality)
        {
            return filterQuality switch
            {
                SKFilterQuality.None => SkiaSharp.SKFilterQuality.None,
                SKFilterQuality.Low => SkiaSharp.SKFilterQuality.Low,
                SKFilterQuality.Medium => SkiaSharp.SKFilterQuality.Medium,
                SKFilterQuality.High => SkiaSharp.SKFilterQuality.High,
                _ => SkiaSharp.SKFilterQuality.None
            };
        }

        public static SkiaSharp.SKPaint ToSKPaint(this SKPaint paint)
        {
            var style = paint.Style.ToSKPaintStyle();
            var strokeCap = paint.StrokeCap.ToSKStrokeCap();
            var strokeJoin = paint.StrokeJoin.ToSKStrokeJoin();
            var textAlign = paint.TextAlign.ToSKTextAlign();
            var typeface = paint.Typeface?.ToSKTypeface();
            var textEncoding = paint.TextEncoding.ToSKTextEncoding();
            var color = paint.Color is null ? SkiaSharp.SKColor.Empty : ToSKColor(paint.Color.Value);
            var shader = paint.Shader?.ToSKShader();
            var colorFilter = paint.ColorFilter?.ToSKColorFilter();
            var imageFilter = paint.ImageFilter?.ToSKImageFilter();
            var pathEffect = paint.PathEffect?.ToSKPathEffect();
            var blendMode = paint.BlendMode.ToSKBlendMode();
            var filterQuality = paint.FilterQuality.ToSKFilterQuality();
            return new SkiaSharp.SKPaint
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
        }

        public static SkiaSharp.SKClipOperation ToSKClipOperation(this SKClipOperation clipOperation)
        {
            return clipOperation switch
            {
                SKClipOperation.Difference => SkiaSharp.SKClipOperation.Difference,
                SKClipOperation.Intersect => SkiaSharp.SKClipOperation.Intersect,
                _ => SkiaSharp.SKClipOperation.Difference
            };
        }

        public static SkiaSharp.SKPathFillType ToSKPathFillType(this SKPathFillType pathFillType)
        {
            return pathFillType switch
            {
                SKPathFillType.Winding => SkiaSharp.SKPathFillType.Winding,
                SKPathFillType.EvenOdd => SkiaSharp.SKPathFillType.EvenOdd,
                _ => SkiaSharp.SKPathFillType.Winding
            };
        }

        public static SkiaSharp.SKPathArcSize ToSKPathArcSize(this SKPathArcSize pathArcSize)
        {
            return pathArcSize switch
            {
                SKPathArcSize.Small => SkiaSharp.SKPathArcSize.Small,
                SKPathArcSize.Large => SkiaSharp.SKPathArcSize.Large,
                _ => SkiaSharp.SKPathArcSize.Small
            };
        }

        public static SkiaSharp.SKPathDirection ToSKPathDirection(this SKPathDirection pathDirection)
        {
            return pathDirection switch
            {
                SKPathDirection.Clockwise => SkiaSharp.SKPathDirection.Clockwise,
                SKPathDirection.CounterClockwise => SkiaSharp.SKPathDirection.CounterClockwise,
                _ => SkiaSharp.SKPathDirection.Clockwise
            };
        }

        public static void ToSKPath(this PathCommand pathCommand, SkiaSharp.SKPath skPath)
        {
            switch (pathCommand)
            {
                case MoveToPathCommand moveToPathCommand:
                    {
                        var x = moveToPathCommand.X;
                        var y = moveToPathCommand.Y;
                        skPath.MoveTo(x, y);
                    }
                    break;
                case LineToPathCommand lineToPathCommand:
                    {
                        var x = lineToPathCommand.X;
                        var y = lineToPathCommand.Y;
                        skPath.LineTo(x, y);
                    }
                    break;
                case ArcToPathCommand arcToPathCommand:
                    {
                        var rx = arcToPathCommand.Rx;
                        var ry = arcToPathCommand.Ry;
                        var xAxisRotate = arcToPathCommand.XAxisRotate;
                        var largeArc = arcToPathCommand.LargeArc.ToSKPathArcSize();
                        var sweep = arcToPathCommand.Sweep.ToSKPathDirection();
                        var x = arcToPathCommand.X;
                        var y = arcToPathCommand.Y;
                        skPath.ArcTo(rx, ry, xAxisRotate, largeArc, sweep, x, y);
                    }
                    break;
                case QuadToPathCommand quadToPathCommand:
                    {
                        var x0 = quadToPathCommand.X0;
                        var y0 = quadToPathCommand.Y0;
                        var x1 = quadToPathCommand.X1;
                        var y1 = quadToPathCommand.Y1;
                        skPath.QuadTo(x0, y0, x1, y1);
                    }
                    break;
                case CubicToPathCommand cubicToPathCommand:
                    {
                        var x0 = cubicToPathCommand.X0;
                        var y0 = cubicToPathCommand.Y0;
                        var x1 = cubicToPathCommand.X1;
                        var y1 = cubicToPathCommand.Y1;
                        var x2 = cubicToPathCommand.X2;
                        var y2 = cubicToPathCommand.Y2;
                        skPath.CubicTo(x0, y0, x1, y1, x2, y2);
                    }
                    break;
                case ClosePathCommand _:
                    {
                        skPath.Close();
                    }
                    break;
                case AddRectPathCommand addRectPathCommand:
                    {
                        var rect = addRectPathCommand.Rect.ToSKRect();
                        skPath.AddRect(rect);
                    }
                    break;
                case AddRoundRectPathCommand addRoundRectPathCommand:
                    {
                        var rect = addRoundRectPathCommand.Rect.ToSKRect();
                        var rx = addRoundRectPathCommand.Rx;
                        var ry = addRoundRectPathCommand.Ry;
                        skPath.AddRoundRect(rect, rx, ry);
                    }
                    break;
                case AddOvalPathCommand addOvalPathCommand:
                    {
                        var rect = addOvalPathCommand.Rect.ToSKRect();
                        skPath.AddOval(rect);
                    }
                    break;
                case AddCirclePathCommand addCirclePathCommand:
                    {
                        var x = addCirclePathCommand.X;
                        var y = addCirclePathCommand.Y;
                        var radius = addCirclePathCommand.Radius;
                        skPath.AddCircle(x, y, radius);
                    }
                    break;
                case AddPolyPathCommand addPolyPathCommand:
                    {
                        if (addPolyPathCommand.Points is { })
                        {
                            var points = addPolyPathCommand.Points.ToSKPoints();
                            var close = addPolyPathCommand.Close;
                            skPath.AddPoly(points, close);
                        }
                    }
                    break;
            }
        }

        public static SkiaSharp.SKPath ToSKPath(this SKPath path)
        {
            var skPath = new SkiaSharp.SKPath
            {
                FillType = path.FillType.ToSKPathFillType()
            };

            if (path.Commands is null) return skPath;

            foreach (var pathCommand in path.Commands) pathCommand.ToSKPath(skPath);

            return skPath;
        }

        public static SkiaSharp.SKPath? ToSKPath(this ClipPath clipPath)
        {
            if (clipPath.Clips is null) return null;

            var skPathResult = default(SkiaSharp.SKPath);

            foreach (var clip in clipPath.Clips)
            {
                if (clip.Path is null) return null;

                var skPath = clip.Path.ToSKPath();
                var skPathClip = clip.Clip?.ToSKPath();
                if (skPathClip is { }) skPath = skPath.Op(skPathClip, SkiaSharp.SKPathOp.Intersect);

                if (clip.Transform is { })
                {
                    var skMatrix = clip.Transform.Value.ToSKMatrix();
                    skPath.Transform(skMatrix);
                }

                if (skPathResult is null)
                {
                    skPathResult = skPath;
                }
                else
                {
                    var result = skPathResult.Op(skPath, SkiaSharp.SKPathOp.Union);
                    skPathResult = result;
                }
            }

            if (skPathResult is { })
            {
                if (clipPath.Clip?.Clips is { })
                {
                    var skPathClip = clipPath.Clip.ToSKPath();
                    if (skPathClip is { }) skPathResult = skPathResult.Op(skPathClip, SkiaSharp.SKPathOp.Intersect);
                }

                if (clipPath.Transform is { })
                {
                    var skMatrix = clipPath.Transform.Value.ToSKMatrix();
                    skPathResult.Transform(skMatrix);
                }
            }

            return skPathResult;
        }

        public static SkiaSharp.SKPicture? ToSKPicture(this SKPicture? picture)
        {
            if (picture is null) return null;

            var skRect = picture.CullRect.ToSKRect();
            using var skPictureRecorder = new SkiaSharp.SKPictureRecorder();
            using var skCanvas = skPictureRecorder.BeginRecording(skRect);

            picture.Draw(skCanvas);

            return skPictureRecorder.EndRecording();
        }

        public static void Draw(this CanvasCommand canvasCommand, SkiaSharp.SKCanvas skCanvas)
        {
            switch (canvasCommand)
            {
                case ClipPathCanvasCommand clipPathCanvasCommand:
                    {
                        var path = clipPathCanvasCommand.ClipPath.ToSKPath();
                        var operation = clipPathCanvasCommand.Operation.ToSKClipOperation();
                        var antialias = clipPathCanvasCommand.Antialias;
                        skCanvas.ClipPath(path, operation, antialias);
                    }
                    break;
                case ClipRectCanvasCommand clipRectCanvasCommand:
                    {
                        var rect = clipRectCanvasCommand.Rect.ToSKRect();
                        var operation = clipRectCanvasCommand.Operation.ToSKClipOperation();
                        var antialias = clipRectCanvasCommand.Antialias;
                        skCanvas.ClipRect(rect, operation, antialias);
                    }
                    break;
                case SaveCanvasCommand _:
                    {
                        skCanvas.Save();
                    }
                    break;
                case RestoreCanvasCommand _:
                    {
                        skCanvas.Restore();
                    }
                    break;
                case SetMatrixCanvasCommand setMatrixCanvasCommand:
                    {
                        var matrix = setMatrixCanvasCommand.Matrix.ToSKMatrix();
                        skCanvas.SetMatrix(matrix);
                    }
                    break;
                case SaveLayerCanvasCommand saveLayerCanvasCommand:
                    {
                        if (saveLayerCanvasCommand.Paint is { })
                        {
                            var paint = saveLayerCanvasCommand.Paint.ToSKPaint();
                            skCanvas.SaveLayer(paint);
                        }
                        else
                        {
                            skCanvas.SaveLayer();
                        }
                    }
                    break;
                case DrawImageCanvasCommand drawImageCanvasCommand:
                    {
                        if (drawImageCanvasCommand.Image is { })
                        {
                            var image = drawImageCanvasCommand.Image.ToSKImage();
                            var source = drawImageCanvasCommand.Source.ToSKRect();
                            var dest = drawImageCanvasCommand.Dest.ToSKRect();
                            var paint = drawImageCanvasCommand.Paint?.ToSKPaint();
                            skCanvas.DrawImage(image, source, dest, paint);
                        }
                    }
                    break;
                case DrawPathCanvasCommand drawPathCanvasCommand:
                    {
                        if (drawPathCanvasCommand.Path is { } && drawPathCanvasCommand.Paint is { })
                        {
                            var path = drawPathCanvasCommand.Path.ToSKPath();
                            var paint = drawPathCanvasCommand.Paint.ToSKPaint();
                            skCanvas.DrawPath(path, paint);
                        }
                    }
                    break;
                case DrawTextBlobCanvasCommand drawPositionedTextCanvasCommand:
                    {
                        if (drawPositionedTextCanvasCommand.TextBlob?.Points is { } && drawPositionedTextCanvasCommand.Paint is { })
                        {
                            var text = drawPositionedTextCanvasCommand.TextBlob.Text;
                            var points = drawPositionedTextCanvasCommand.TextBlob.Points.ToSKPoints();
                            var paint = drawPositionedTextCanvasCommand.Paint.ToSKPaint();
                            var font = paint.ToFont();
                            var textBlob = SkiaSharp.SKTextBlob.CreatePositioned(text, font, points);
                            skCanvas.DrawText(textBlob, 0, 0, paint);
                        }
                    }
                    break;
                case DrawTextCanvasCommand drawTextCanvasCommand:
                    {
                        if (drawTextCanvasCommand.Paint is { })
                        {
                            var text = drawTextCanvasCommand.Text;
                            var x = drawTextCanvasCommand.X;
                            var y = drawTextCanvasCommand.Y;
                            var paint = drawTextCanvasCommand.Paint.ToSKPaint();
                            skCanvas.DrawText(text, x, y, paint);
                        }
                    }
                    break;
                case DrawTextOnPathCanvasCommand drawTextOnPathCanvasCommand:
                    {
                        if (drawTextOnPathCanvasCommand.Path is { } && drawTextOnPathCanvasCommand.Paint is { })
                        {
                            var text = drawTextOnPathCanvasCommand.Text;
                            var path = drawTextOnPathCanvasCommand.Path.ToSKPath();
                            var hOffset = drawTextOnPathCanvasCommand.HOffset;
                            var vOffset = drawTextOnPathCanvasCommand.VOffset;
                            var paint = drawTextOnPathCanvasCommand.Paint.ToSKPaint();
                            skCanvas.DrawTextOnPath(text, path, hOffset, vOffset, paint);
                        }
                    }
                    break;
            }
        }

        public static void Draw(this SKPicture picture, SkiaSharp.SKCanvas skCanvas)
        {
            if (picture.Commands is null) return;

            foreach (var canvasCommand in picture.Commands) canvasCommand.Draw(skCanvas);
        }
    }
}
