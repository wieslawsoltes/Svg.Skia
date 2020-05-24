using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Picture.Skia
{
    internal static class SkiaModelExtensions
    {
        public static SKPoint ToSKPoint(this Point point)
        {
            return new SKPoint(point.X, point.Y);
        }

        public static SKPoint3 ToSKPoint3(this Point3 point3)
        {
            return new SKPoint3(point3.X, point3.Y, point3.Z);
        }

        public static SKPoint[] ToSKPoints(this IList<Point> points)
        {
            var skPoints = new SKPoint[points.Count];

            for (int i = 0; i < points.Count; i++)
            {
                skPoints[i] = points[i].ToSKPoint();
            }

            return skPoints;
        }

        public static SKPointI ToSKPointI(this PointI pointI)
        {
            return new SKPointI(pointI.X, pointI.Y);
        }

        public static SKSize ToSKSize(this Size size)
        {
            return new SKSize(size.Width, size.Height);
        }

        public static SKSizeI ToSKSizeI(this SizeI sizeI)
        {
            return new SKSizeI(sizeI.Width, sizeI.Height);
        }

        public static SKRect ToSKRect(this Rect rect)
        {
            return new SKRect(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }

        public static SKMatrix ToSKMatrix(this Matrix matrix)
        {
            return new SKMatrix(
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

        public static SKImage ToSKImage(this Image image)
        {
            return SKImage.FromEncodedData(image.Data);
        }

        public static SKPaintStyle ToSKPaintStyle(this PaintStyle paintStyle)
        {
            switch (paintStyle)
            {
                default:
                case PaintStyle.Fill:
                    return SKPaintStyle.Fill;
                case PaintStyle.Stroke:
                    return SKPaintStyle.Stroke;
                case PaintStyle.StrokeAndFill:
                    return SKPaintStyle.StrokeAndFill;
            }
        }

        public static SKStrokeCap ToSKStrokeCap(this StrokeCap strokeCap)
        {
            switch (strokeCap)
            {
                default:
                case StrokeCap.Butt:
                    return SKStrokeCap.Butt;
                case StrokeCap.Round:
                    return SKStrokeCap.Round;
                case StrokeCap.Square:
                    return SKStrokeCap.Square;
            }
        }

        public static SKStrokeJoin ToSKStrokeJoin(this StrokeJoin strokeJoin)
        {
            switch (strokeJoin)
            {
                default:
                case StrokeJoin.Miter:
                    return SKStrokeJoin.Miter;
                case StrokeJoin.Round:
                    return SKStrokeJoin.Round;
                case StrokeJoin.Bevel:
                    return SKStrokeJoin.Bevel;
            }
        }

        public static SKTextAlign ToSKTextAlign(this TextAlign textAlign)
        {
            switch (textAlign)
            {
                default:
                case TextAlign.Left:
                    return SKTextAlign.Left;
                case TextAlign.Center:
                    return SKTextAlign.Center;
                case TextAlign.Right:
                    return SKTextAlign.Right;
            }
        }

        public static SKTextEncoding ToSKTextEncoding(this TextEncoding textEncoding)
        {
            switch (textEncoding)
            {
                default:
                case TextEncoding.Utf8:
                    return SKTextEncoding.Utf8;
                case TextEncoding.Utf16:
                    return SKTextEncoding.Utf16;
                case TextEncoding.Utf32:
                    return SKTextEncoding.Utf32;
                case TextEncoding.GlyphId:
                    return SKTextEncoding.GlyphId;
            }
        }

        public static SKFontStyleWeight ToSKFontStyleWeight(this FontStyleWeight fontStyleWeight)
        {
            switch (fontStyleWeight)
            {
                default:
                case FontStyleWeight.Invisible:
                    return SKFontStyleWeight.Invisible;
                case FontStyleWeight.Thin:
                    return SKFontStyleWeight.Thin;
                case FontStyleWeight.ExtraLight:
                    return SKFontStyleWeight.ExtraLight;
                case FontStyleWeight.Light:
                    return SKFontStyleWeight.Light;
                case FontStyleWeight.Normal:
                    return SKFontStyleWeight.Normal;
                case FontStyleWeight.Medium:
                    return SKFontStyleWeight.Medium;
                case FontStyleWeight.SemiBold:
                    return SKFontStyleWeight.SemiBold;
                case FontStyleWeight.Bold:
                    return SKFontStyleWeight.Bold;
                case FontStyleWeight.ExtraBold:
                    return SKFontStyleWeight.ExtraBold;
                case FontStyleWeight.Black:
                    return SKFontStyleWeight.Black;
                case FontStyleWeight.ExtraBlack:
                    return SKFontStyleWeight.ExtraBlack;
            }
        }

        public static SKFontStyleWidth ToSKFontStyleWidth(this FontStyleWidth fontStyleWidth)
        {
            switch (fontStyleWidth)
            {
                default:
                case FontStyleWidth.UltraCondensed:
                    return SKFontStyleWidth.UltraCondensed;
                case FontStyleWidth.ExtraCondensed:
                    return SKFontStyleWidth.ExtraCondensed;
                case FontStyleWidth.Condensed:
                    return SKFontStyleWidth.Condensed;
                case FontStyleWidth.SemiCondensed:
                    return SKFontStyleWidth.SemiCondensed;
                case FontStyleWidth.Normal:
                    return SKFontStyleWidth.Normal;
                case FontStyleWidth.SemiExpanded:
                    return SKFontStyleWidth.SemiExpanded;
                case FontStyleWidth.Expanded:
                    return SKFontStyleWidth.Expanded;
                case FontStyleWidth.ExtraExpanded:
                    return SKFontStyleWidth.ExtraExpanded;
                case FontStyleWidth.UltraExpanded:
                    return SKFontStyleWidth.UltraExpanded;
            }
        }

        public static SKFontStyleSlant ToSKFontStyleSlant(this FontStyleSlant fontStyleSlant)
        {
            switch (fontStyleSlant)
            {
                default:
                case FontStyleSlant.Upright:
                    return SKFontStyleSlant.Upright;
                case FontStyleSlant.Italic:
                    return SKFontStyleSlant.Italic;
                case FontStyleSlant.Oblique:
                    return SKFontStyleSlant.Oblique;
            }
        }

        public static SKTypeface? ToSKTypeface(this Typeface? typeface)
        {
            if (typeface == null)
            {
                return null;
            }

            var familyName = typeface.FamilyName;
            var weight = typeface.Weight.ToSKFontStyleWeight();
            var width = typeface.Width.ToSKFontStyleWidth();
            var slant = typeface.Style.ToSKFontStyleSlant();

            // TODO: SKSvgSettings.s_typefaceProviders

            return SKTypeface.FromFamilyName(familyName, weight, width, slant);
        }

        public static SKColor ToSKColor(this Color color)
        {
            return new SKColor(color.Red, color.Green, color.Blue, color.Alpha);
        }

        public static SKColor[] ToSKColors(this Color[] colors)
        {
            var skColors = new SKColor[colors.Length];

            for (int i = 0; i < colors.Length; i++)
            {
                skColors[i] = colors[i].ToSKColor();
            }

            return skColors;
        }

        public static SKShaderTileMode ToSKShaderTileMode(this ShaderTileMode shaderTileMode)
        {
            switch (shaderTileMode)
            {
                default:
                case ShaderTileMode.Clamp:
                    return SKShaderTileMode.Clamp;
                case ShaderTileMode.Repeat:
                    return SKShaderTileMode.Repeat;
                case ShaderTileMode.Mirror:
                    return SKShaderTileMode.Mirror;
            }
        }

        public static SKShader? ToSKShader(this Shader? shader)
        {
            switch (shader)
            {
                case ColorShader colorShader:
                    {
                        return SKShader.CreateColor(colorShader.Color.ToSKColor());
                    }
                case LinearGradientShader linearGradientShader:
                    {
                        if (linearGradientShader.Colors == null || linearGradientShader.ColorPos == null)
                        {
                            return null;
                        }

                        if (linearGradientShader.LocalMatrix != null)
                        {
                            return SKShader.CreateLinearGradient(
                                linearGradientShader.Start.ToSKPoint(),
                                linearGradientShader.End.ToSKPoint(),
                                linearGradientShader.Colors.ToSKColors(),
                                linearGradientShader.ColorPos,
                                linearGradientShader.Mode.ToSKShaderTileMode(),
                                linearGradientShader.LocalMatrix.Value.ToSKMatrix());
                        }
                        else
                        {
                            return SKShader.CreateLinearGradient(
                                linearGradientShader.Start.ToSKPoint(),
                                linearGradientShader.End.ToSKPoint(),
                                linearGradientShader.Colors.ToSKColors(),
                                linearGradientShader.ColorPos,
                                linearGradientShader.Mode.ToSKShaderTileMode());
                        }
                    }
                case TwoPointConicalGradientShader twoPointConicalGradientShader:
                    {
                        if (twoPointConicalGradientShader.Colors == null || twoPointConicalGradientShader.ColorPos == null)
                        {
                            return null;
                        }

                        if (twoPointConicalGradientShader.LocalMatrix != null)
                        {
                            return SKShader.CreateTwoPointConicalGradient(
                                twoPointConicalGradientShader.Start.ToSKPoint(),
                                twoPointConicalGradientShader.StartRadius,
                                twoPointConicalGradientShader.End.ToSKPoint(),
                                twoPointConicalGradientShader.EndRadius,
                                twoPointConicalGradientShader.Colors.ToSKColors(),
                                twoPointConicalGradientShader.ColorPos,
                                twoPointConicalGradientShader.Mode.ToSKShaderTileMode(),
                                twoPointConicalGradientShader.LocalMatrix.Value.ToSKMatrix());
                        }
                        else
                        {
                            return SKShader.CreateTwoPointConicalGradient(
                                twoPointConicalGradientShader.Start.ToSKPoint(),
                                twoPointConicalGradientShader.StartRadius,
                                twoPointConicalGradientShader.End.ToSKPoint(),
                                twoPointConicalGradientShader.EndRadius,
                                twoPointConicalGradientShader.Colors.ToSKColors(),
                                twoPointConicalGradientShader.ColorPos,
                                twoPointConicalGradientShader.Mode.ToSKShaderTileMode());
                        }
                    }
                case PictureShader pictureShader:
                    {
                        if (pictureShader.Src == null)
                        {
                            return null;
                        }

                        return SKShader.CreatePicture(
                            pictureShader.Src.ToSKPicture(),
                            SKShaderTileMode.Repeat,
                            SKShaderTileMode.Repeat,
                            pictureShader.LocalMatrix.ToSKMatrix(),
                            pictureShader.Tile.ToSKRect());
                    }
                case PerlinNoiseFractalNoiseShader perlinNoiseFractalNoiseShader:
                    {
                        return SKShader.CreatePerlinNoiseFractalNoise(
                            perlinNoiseFractalNoiseShader.BaseFrequencyX,
                            perlinNoiseFractalNoiseShader.BaseFrequencyY,
                            perlinNoiseFractalNoiseShader.NumOctaves,
                            perlinNoiseFractalNoiseShader.Seed,
                            perlinNoiseFractalNoiseShader.TileSize.ToSKPointI());
                    }
                case PerlinNoiseTurbulenceShader perlinNoiseTurbulenceShader:
                    {
                        return SKShader.CreatePerlinNoiseTurbulence(
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

        public static SKColorFilter? ToSKColorFilter(this ColorFilter? colorFilter)
        {
            switch (colorFilter)
            {
                case BlendModeColorFilter blendModeColorFilter:
                    {
                        return SKColorFilter.CreateBlendMode(
                            blendModeColorFilter.Color.ToSKColor(),
                            blendModeColorFilter.Mode.ToSKBlendMode());
                    }
                case ColorMatrixColorFilter colorMatrixColorFilter:
                    {
                        if (colorMatrixColorFilter.Matrix == null)
                        {
                            return null;
                        }
                        return SKColorFilter.CreateColorMatrix(colorMatrixColorFilter.Matrix);
                    }
                case LumaColorColorFilter _:
                    {
                        return SKColorFilter.CreateLumaColor();
                    }
                case TableColorFilter tableColorFilter:
                    {
                        if (tableColorFilter.TableA == null
                            || tableColorFilter.TableR == null
                            || tableColorFilter.TableG == null
                            || tableColorFilter.TableB == null)
                        {
                            return null;
                        }
                        return SKColorFilter.CreateTable(
                            tableColorFilter.TableA,
                            tableColorFilter.TableR,
                            tableColorFilter.TableG,
                            tableColorFilter.TableB);
                    }
                default:
                    return null;
            }
        }

        public static SKImageFilter.CropRect ToCropRect(this CropRect cropRect)
        {
            return new SKImageFilter.CropRect(cropRect.Rect.ToSKRect());
        }

        public static SKDisplacementMapEffectChannelSelectorType ToSKDisplacementMapEffectChannelSelectorType(this DisplacementMapEffectChannelSelectorType displacementMapEffectChannelSelectorType)
        {
            switch (displacementMapEffectChannelSelectorType)
            {
                default:
                case DisplacementMapEffectChannelSelectorType.Unknown:
                    return SKDisplacementMapEffectChannelSelectorType.Unknown;
                case DisplacementMapEffectChannelSelectorType.R:
                    return SKDisplacementMapEffectChannelSelectorType.R;
                case DisplacementMapEffectChannelSelectorType.G:
                    return SKDisplacementMapEffectChannelSelectorType.G;
                case DisplacementMapEffectChannelSelectorType.B:
                    return SKDisplacementMapEffectChannelSelectorType.B;
                case DisplacementMapEffectChannelSelectorType.A:
                    return SKDisplacementMapEffectChannelSelectorType.A;
            }
        }

        public static SKMatrixConvolutionTileMode ToSKMatrixConvolutionTileMode(this MatrixConvolutionTileMode matrixConvolutionTileMode)
        {
            switch (matrixConvolutionTileMode)
            {
                default:
                case MatrixConvolutionTileMode.Clamp:
                    return SKMatrixConvolutionTileMode.Clamp;
                case MatrixConvolutionTileMode.Repeat:
                    return SKMatrixConvolutionTileMode.Repeat;
                case MatrixConvolutionTileMode.ClampToBlack:
                    return SKMatrixConvolutionTileMode.ClampToBlack;
            }
        }

        public static SKImageFilter? ToSKImageFilter(this ImageFilter? imageFilter)
        {
            switch (imageFilter)
            {
                case ArithmeticImageFilter arithmeticImageFilter:
                    {
                        if (arithmeticImageFilter.Background == null)
                        {
                            return null;
                        }

                        return SKImageFilter.CreateArithmetic(
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
                        if (blendModeImageFilter.Background == null)
                        {
                            return null;
                        }

                        return SKImageFilter.CreateBlendMode(
                            blendModeImageFilter.Mode.ToSKBlendMode(),
                            blendModeImageFilter.Background?.ToSKImageFilter(),
                            blendModeImageFilter.Foreground?.ToSKImageFilter(),
                            blendModeImageFilter.CropRect?.ToCropRect());
                    }
                case BlurImageFilter blurImageFilter:
                    {
                        return SKImageFilter.CreateBlur(
                            blurImageFilter.SigmaX,
                            blurImageFilter.SigmaY,
                            blurImageFilter.Input?.ToSKImageFilter(),
                            blurImageFilter.CropRect?.ToCropRect());
                    }
                case ColorFilterImageFilter colorFilterImageFilter:
                    {
                        if (colorFilterImageFilter.ColorFilter == null)
                        {
                            return null;
                        }

                        return SKImageFilter.CreateColorFilter(
                            colorFilterImageFilter.ColorFilter?.ToSKColorFilter(),
                            colorFilterImageFilter.Input?.ToSKImageFilter(),
                            colorFilterImageFilter.CropRect?.ToCropRect());
                    }
                case DilateImageFilter dilateImageFilter:
                    {
                        return SKImageFilter.CreateDilate(
                            dilateImageFilter.RadiusX,
                            dilateImageFilter.RadiusY,
                            dilateImageFilter.Input?.ToSKImageFilter(),
                            dilateImageFilter.CropRect?.ToCropRect());
                    }
                case DisplacementMapEffectImageFilter displacementMapEffectImageFilter:
                    {
                        if (displacementMapEffectImageFilter.Displacement == null)
                        {
                            return null;
                        }

                        return SKImageFilter.CreateDisplacementMapEffect(
                            displacementMapEffectImageFilter.XChannelSelector.ToSKDisplacementMapEffectChannelSelectorType(),
                            displacementMapEffectImageFilter.YChannelSelector.ToSKDisplacementMapEffectChannelSelectorType(),
                            displacementMapEffectImageFilter.Scale,
                            displacementMapEffectImageFilter.Displacement?.ToSKImageFilter(),
                            displacementMapEffectImageFilter.Input?.ToSKImageFilter(),
                            displacementMapEffectImageFilter.CropRect?.ToCropRect());
                    }
                case DistantLitDiffuseImageFilter distantLitDiffuseImageFilter:
                    {
                        return SKImageFilter.CreateDistantLitDiffuse(
                            distantLitDiffuseImageFilter.Direction.ToSKPoint3(),
                            distantLitDiffuseImageFilter.LightColor.ToSKColor(),
                            distantLitDiffuseImageFilter.SurfaceScale,
                            distantLitDiffuseImageFilter.Kd,
                            distantLitDiffuseImageFilter.Input?.ToSKImageFilter(),
                            distantLitDiffuseImageFilter.CropRect?.ToCropRect());
                    }
                case DistantLitSpecularImageFilter distantLitSpecularImageFilter:
                    {
                        return SKImageFilter.CreateDistantLitSpecular(
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
                        return SKImageFilter.CreateErode(
                            erodeImageFilter.RadiusX,
                            erodeImageFilter.RadiusY,
                            erodeImageFilter.Input?.ToSKImageFilter(),
                            erodeImageFilter.CropRect?.ToCropRect());
                    }
                case ImageImageFilter imageImageFilter:
                    {
                        if (imageImageFilter.Image == null)
                        {
                            return null;
                        }

                        return SKImageFilter.CreateImage(
                            imageImageFilter.Image.ToSKImage(),
                            imageImageFilter.Src.ToSKRect(),
                            imageImageFilter.Dst.ToSKRect(),
                            SKFilterQuality.High);
                    }
                case MatrixConvolutionImageFilter matrixConvolutionImageFilter:
                    {
                        if (matrixConvolutionImageFilter.Kernel == null)
                        {
                            return null;
                        }

                        return SKImageFilter.CreateMatrixConvolution(
                            matrixConvolutionImageFilter.KernelSize.ToSKSizeI(),
                            matrixConvolutionImageFilter.Kernel,
                            matrixConvolutionImageFilter.Gain,
                            matrixConvolutionImageFilter.Bias,
                            matrixConvolutionImageFilter.KernelOffset.ToSKPointI(),
                            matrixConvolutionImageFilter.TileMode.ToSKMatrixConvolutionTileMode(),
                            matrixConvolutionImageFilter.ConvolveAlpha,
                            matrixConvolutionImageFilter.Input?.ToSKImageFilter(),
                            matrixConvolutionImageFilter.CropRect?.ToCropRect());
                    }
                case MergeImageFilter mergeImageFilter:
                    {
                        if (mergeImageFilter.Filters == null)
                        {
                            return null;
                        }

                        return SKImageFilter.CreateMerge(
                            mergeImageFilter.Filters?.ToSKImageFilters(),
                            mergeImageFilter.CropRect?.ToCropRect());
                    }
                case OffsetImageFilter offsetImageFilter:
                    {
                        return SKImageFilter.CreateOffset(
                            offsetImageFilter.Dx,
                            offsetImageFilter.Dy,
                            offsetImageFilter.Input?.ToSKImageFilter(),
                            offsetImageFilter.CropRect?.ToCropRect());
                    }
                case PaintImageFilter paintImageFilter:
                    {
                        if (paintImageFilter.Paint == null)
                        {
                            return null;
                        }

                        return SKImageFilter.CreatePaint(
                            paintImageFilter.Paint.ToSKPaint(),
                            paintImageFilter.CropRect?.ToCropRect());
                    }
                case PictureImageFilter pictureImageFilter:
                    {
                        if (pictureImageFilter.Picture == null)
                        {
                            return null;
                        }

                        return SKImageFilter.CreatePicture(
                            pictureImageFilter.Picture.ToSKPicture(),
                            pictureImageFilter.Picture.CullRect.ToSKRect());
                    }
                case PointLitDiffuseImageFilter pointLitDiffuseImageFilter:
                    {
                        return SKImageFilter.CreatePointLitDiffuse(
                            pointLitDiffuseImageFilter.Location.ToSKPoint3(),
                            pointLitDiffuseImageFilter.LightColor.ToSKColor(),
                            pointLitDiffuseImageFilter.SurfaceScale,
                            pointLitDiffuseImageFilter.Kd,
                            pointLitDiffuseImageFilter.Input?.ToSKImageFilter(),
                            pointLitDiffuseImageFilter.CropRect?.ToCropRect());
                    }
                case PointLitSpecularImageFilter pointLitSpecularImageFilter:
                    {
                        return SKImageFilter.CreatePointLitSpecular(
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
                        return SKImageFilter.CreateSpotLitDiffuse(
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
                        return SKImageFilter.CreateSpotLitSpecular(
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
                        return SKImageFilter.CreateTile(
                            tileImageFilter.Src.ToSKRect(),
                            tileImageFilter.Dst.ToSKRect(),
                            tileImageFilter.Input?.ToSKImageFilter());
                    }
                default:
                    return null;
            }
        }

        public static SKImageFilter[]? ToSKImageFilters(this ImageFilter[]? imageFilters)
        {
            if (imageFilters == null)
            {
                return null;
            }

            var skImageFilters = new SKImageFilter[imageFilters.Length];

            for (int i = 0; i < imageFilters.Length; i++)
            {
                var imageFilter = imageFilters[i];
                if (imageFilter != null)
                {
                    var skImageFilter = imageFilter.ToSKImageFilter();
                    if (skImageFilter != null)
                    {
                        skImageFilters[i] = skImageFilter;
                    }
                }
            }

            return skImageFilters;
        }

        public static SKPathEffect? ToSKPathEffect(this PathEffect? pathEffect)
        {
            switch (pathEffect)
            {
                case DashPathEffect dashPathEffect:
                    {
                        return SKPathEffect.CreateDash(
                            dashPathEffect.Intervals,
                            dashPathEffect.Phase);
                    }
                default:
                    return null;
            }
        }

        public static SKBlendMode ToSKBlendMode(this BlendMode blendMode)
        {
            switch (blendMode)
            {
                default:
                case BlendMode.Clear:
                    return SKBlendMode.Clear;
                case BlendMode.Src:
                    return SKBlendMode.Src;
                case BlendMode.Dst:
                    return SKBlendMode.Dst;
                case BlendMode.SrcOver:
                    return SKBlendMode.SrcOver;
                case BlendMode.DstOver:
                    return SKBlendMode.DstOver;
                case BlendMode.SrcIn:
                    return SKBlendMode.SrcIn;
                case BlendMode.DstIn:
                    return SKBlendMode.DstIn;
                case BlendMode.SrcOut:
                    return SKBlendMode.SrcOut;
                case BlendMode.DstOut:
                    return SKBlendMode.DstOut;
                case BlendMode.SrcATop:
                    return SKBlendMode.SrcATop;
                case BlendMode.DstATop:
                    return SKBlendMode.DstATop;
                case BlendMode.Xor:
                    return SKBlendMode.Xor;
                case BlendMode.Plus:
                    return SKBlendMode.Plus;
                case BlendMode.Modulate:
                    return SKBlendMode.Modulate;
                case BlendMode.Screen:
                    return SKBlendMode.Screen;
                case BlendMode.Overlay:
                    return SKBlendMode.Overlay;
                case BlendMode.Darken:
                    return SKBlendMode.Darken;
                case BlendMode.Lighten:
                    return SKBlendMode.Lighten;
                case BlendMode.ColorDodge:
                    return SKBlendMode.ColorDodge;
                case BlendMode.ColorBurn:
                    return SKBlendMode.ColorBurn;
                case BlendMode.HardLight:
                    return SKBlendMode.HardLight;
                case BlendMode.SoftLight:
                    return SKBlendMode.SoftLight;
                case BlendMode.Difference:
                    return SKBlendMode.Difference;
                case BlendMode.Exclusion:
                    return SKBlendMode.Exclusion;
                case BlendMode.Multiply:
                    return SKBlendMode.Multiply;
                case BlendMode.Hue:
                    return SKBlendMode.Hue;
                case BlendMode.Saturation:
                    return SKBlendMode.Saturation;
                case BlendMode.Color:
                    return SKBlendMode.Color;
                case BlendMode.Luminosity:
                    return SKBlendMode.Luminosity;
            }
        }

        public static SKFilterQuality ToSKFilterQuality(this FilterQuality filterQuality)
        {
            switch (filterQuality)
            {
                default:
                case FilterQuality.None:
                    return SKFilterQuality.None;
                case FilterQuality.Low:
                    return SKFilterQuality.Low;
                case FilterQuality.Medium:
                    return SKFilterQuality.Medium;
                case FilterQuality.High:
                    return SKFilterQuality.High;
            }
        }

        public static SKPaint ToSKPaint(this Paint paint)
        {
            var style = paint.Style.ToSKPaintStyle();
            var strokeCap = paint.StrokeCap.ToSKStrokeCap();
            var strokeJoin = paint.StrokeJoin.ToSKStrokeJoin();
            var textAlign = paint.TextAlign.ToSKTextAlign();
            var typeface = paint.Typeface?.ToSKTypeface();
            var textEncoding = paint.TextEncoding.ToSKTextEncoding();
            var color = paint.Color == null ? SKColor.Empty : ToSKColor(paint.Color.Value);
            var shader = paint.Shader?.ToSKShader();
            var colorFilter = paint.ColorFilter?.ToSKColorFilter();
            var imageFilter = paint.ImageFilter?.ToSKImageFilter();
            var pathEffect = paint.PathEffect?.ToSKPathEffect();
            var blendMode = paint.BlendMode.ToSKBlendMode();
            var filterQuality = paint.FilterQuality.ToSKFilterQuality();

            return new SKPaint()
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

        public static SKClipOperation ToSKClipOperation(this ClipOperation clipOperation)
        {
            switch (clipOperation)
            {
                default:
                case ClipOperation.Difference:
                    return SKClipOperation.Difference;
                case ClipOperation.Intersect:
                    return SKClipOperation.Intersect;
            }
        }

        public static SKPathFillType ToSKPathFillType(this PathFillType pathFillType)
        {
            switch (pathFillType)
            {
                default:
                case PathFillType.Winding:
                    return SKPathFillType.Winding;
                case PathFillType.EvenOdd:
                    return SKPathFillType.EvenOdd;
            }
        }

        public static SKPathArcSize ToSKPathArcSize(this PathArcSize pathArcSize)
        {
            switch (pathArcSize)
            {
                default:
                case PathArcSize.Small:
                    return SKPathArcSize.Small;
                case PathArcSize.Large:
                    return SKPathArcSize.Large;
            }
        }

        public static SKPathDirection ToSKPathDirection(this PathDirection pathDirection)
        {
            switch (pathDirection)
            {
                default:
                case PathDirection.Clockwise:
                    return SKPathDirection.Clockwise;
                case PathDirection.CounterClockwise:
                    return SKPathDirection.CounterClockwise;
            }
        }

        public static SKPathOp ToSKPathOp(this PathOp pathOp)
        {
            switch (pathOp)
            {
                default:
                case PathOp.Difference:
                    return SKPathOp.Difference;
                case PathOp.Intersect:
                    return SKPathOp.Intersect;
                case PathOp.Union:
                    return SKPathOp.Union;
                case PathOp.Xor:
                    return SKPathOp.Xor;
                case PathOp.ReverseDifference:
                    return SKPathOp.ReverseDifference;
            }
        }

        public static void ToSKPath(this PathCommand pathCommand, SKPath skPath)
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
                        if (addPolyPathCommand.Points != null)
                        {
                            var points = addPolyPathCommand.Points.ToSKPoints();
                            var close = addPolyPathCommand.Close;
                            skPath.AddPoly(points, close);
                        }
                    }
                    break;
                default:
                    break;
            }
        }

        public static SKPath ToSKPath(this Path path)
        {
            var skPath = new SKPath()
            {
                FillType = path.FillType.ToSKPathFillType()
            };

            if (path.Commands == null)
            {
                return skPath;
            }

            foreach (var pathCommand in path.Commands)
            {
                pathCommand.ToSKPath(skPath);
            }

            return skPath;
        }

        public static SKPath? ToSKPath(this ClipPath clipPath)
        {
            if (clipPath.Clips == null)
            {
                return null;
            }

            var skPathResult = default(SKPath);

            foreach (var clip in clipPath.Clips)
            {
                if (clip.Path == null)
                {
                    return null;
                }

                var skPath = clip.Path.ToSKPath();
                if (skPath != null)
                {
                    if (clip.Clip != null)
                    {
                        var skPathClip = clip.Clip.ToSKPath();
                        if (skPathClip != null)
                        {
                            skPath = skPath.Op(skPathClip, SKPathOp.Intersect);
                        }
                    }

                    if (clip.Transform != null)
                    {
                        var skMatrix = clip.Transform.Value.ToSKMatrix();
                        skPath.Transform(skMatrix);
                    }

                    if (skPathResult == null)
                    {
                        skPathResult = skPath;
                    }
                    else
                    {
                        var result = skPathResult.Op(skPath, SKPathOp.Union);
                        skPathResult = result;
                    }
                }
            }

            if (skPathResult != null)
            {
                if (clipPath.Clip != null && clipPath.Clip.Clips != null)
                {
                    var skPathClip = clipPath.Clip.ToSKPath();
                    if (skPathClip != null)
                    {
                        skPathResult = skPathResult.Op(skPathClip, SKPathOp.Intersect);
                    }
                }

                if (clipPath.Transform != null)
                {
                    var skMatrix = clipPath.Transform.Value.ToSKMatrix();
                    skPathResult.Transform(skMatrix);
                }
            }

            return skPathResult;
        }

        public static SKPicture? ToSKPicture(this Picture? picture)
        {
            if (picture == null)
            {
                return null;
            }

            var skRect = picture.CullRect.ToSKRect();
            using var skPictureRecorder = new SKPictureRecorder();
            using var skCanvas = skPictureRecorder.BeginRecording(skRect);

            picture.Draw(skCanvas);

            return skPictureRecorder.EndRecording();
        }

        public static void Draw(this CanvasCommand canvasCommand, SKCanvas skCanvas)
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
                        if (saveLayerCanvasCommand.Paint != null)
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
                        if (drawImageCanvasCommand.Image != null)
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
                        if (drawPathCanvasCommand.Path != null && drawPathCanvasCommand.Paint != null)
                        {
                            var path = drawPathCanvasCommand.Path.ToSKPath();
                            var paint = drawPathCanvasCommand.Paint.ToSKPaint();
                            skCanvas.DrawPath(path, paint);
                        }
                    }
                    break;
                case DrawPositionedTextCanvasCommand drawPositionedTextCanvasCommand:
                    {
                        if (drawPositionedTextCanvasCommand.Points != null && drawPositionedTextCanvasCommand.Paint != null)
                        {
                            var text = drawPositionedTextCanvasCommand.Text;
                            var points = drawPositionedTextCanvasCommand.Points.ToSKPoints();
                            var paint = drawPositionedTextCanvasCommand.Paint.ToSKPaint();
                            skCanvas.DrawPositionedText(text, points, paint);
                        }
                    }
                    break;
                case DrawTextCanvasCommand drawTextCanvasCommand:
                    {
                        if (drawTextCanvasCommand.Paint != null)
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
                        if (drawTextOnPathCanvasCommand.Path != null && drawTextOnPathCanvasCommand.Paint != null)
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
                default:
                    break;
            }
        }

        public static void Draw(this Picture picture, SKCanvas skCanvas)
        {
            if (picture.Commands == null)
            {
                return;
            }

            foreach (var canvasCommand in picture.Commands)
            {
                canvasCommand.Draw(skCanvas);
            }
        }
    }
}
