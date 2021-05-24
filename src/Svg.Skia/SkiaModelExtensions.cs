using System.Collections.Generic;
using SkiaSharp;
using Svg.Model.Painting;
using Svg.Model.Painting.ColorFilters;
using Svg.Model.Painting.ImageFilters;
using Svg.Model.Painting.PathEffects;
using Svg.Model.Painting.Shaders;
using Svg.Model.Primitives;
using Svg.Model.Primitives.CanvasCommands;
using Svg.Model.Primitives.PathCommands;

namespace Svg.Skia
{
    public static class SkiaModelExtensions
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
            return paintStyle switch
            {
                PaintStyle.Fill => SKPaintStyle.Fill,
                PaintStyle.Stroke => SKPaintStyle.Stroke,
                PaintStyle.StrokeAndFill => SKPaintStyle.StrokeAndFill,
                _ => SKPaintStyle.Fill
            };
        }

        public static SKStrokeCap ToSKStrokeCap(this StrokeCap strokeCap)
        {
            return strokeCap switch
            {
                StrokeCap.Butt => SKStrokeCap.Butt,
                StrokeCap.Round => SKStrokeCap.Round,
                StrokeCap.Square => SKStrokeCap.Square,
                _ => SKStrokeCap.Butt
            };
        }

        public static SKStrokeJoin ToSKStrokeJoin(this StrokeJoin strokeJoin)
        {
            return strokeJoin switch
            {
                StrokeJoin.Miter => SKStrokeJoin.Miter,
                StrokeJoin.Round => SKStrokeJoin.Round,
                StrokeJoin.Bevel => SKStrokeJoin.Bevel,
                _ => SKStrokeJoin.Miter
            };
        }

        public static SKTextAlign ToSKTextAlign(this TextAlign textAlign)
        {
            return textAlign switch
            {
                TextAlign.Left => SKTextAlign.Left,
                TextAlign.Center => SKTextAlign.Center,
                TextAlign.Right => SKTextAlign.Right,
                _ => SKTextAlign.Left
            };
        }

        public static SKTextEncoding ToSKTextEncoding(this TextEncoding textEncoding)
        {
            return textEncoding switch
            {
                TextEncoding.Utf8 => SKTextEncoding.Utf8,
                TextEncoding.Utf16 => SKTextEncoding.Utf16,
                TextEncoding.Utf32 => SKTextEncoding.Utf32,
                TextEncoding.GlyphId => SKTextEncoding.GlyphId,
                _ => SKTextEncoding.Utf8
            };
        }

        public static SKFontStyleWeight ToSKFontStyleWeight(this FontStyleWeight fontStyleWeight)
        {
            return fontStyleWeight switch
            {
                FontStyleWeight.Invisible => SKFontStyleWeight.Invisible,
                FontStyleWeight.Thin => SKFontStyleWeight.Thin,
                FontStyleWeight.ExtraLight => SKFontStyleWeight.ExtraLight,
                FontStyleWeight.Light => SKFontStyleWeight.Light,
                FontStyleWeight.Normal => SKFontStyleWeight.Normal,
                FontStyleWeight.Medium => SKFontStyleWeight.Medium,
                FontStyleWeight.SemiBold => SKFontStyleWeight.SemiBold,
                FontStyleWeight.Bold => SKFontStyleWeight.Bold,
                FontStyleWeight.ExtraBold => SKFontStyleWeight.ExtraBold,
                FontStyleWeight.Black => SKFontStyleWeight.Black,
                FontStyleWeight.ExtraBlack => SKFontStyleWeight.ExtraBlack,
                _ => SKFontStyleWeight.Invisible
            };
        }

        public static SKFontStyleWidth ToSKFontStyleWidth(this FontStyleWidth fontStyleWidth)
        {
            return fontStyleWidth switch
            {
                FontStyleWidth.UltraCondensed => SKFontStyleWidth.UltraCondensed,
                FontStyleWidth.ExtraCondensed => SKFontStyleWidth.ExtraCondensed,
                FontStyleWidth.Condensed => SKFontStyleWidth.Condensed,
                FontStyleWidth.SemiCondensed => SKFontStyleWidth.SemiCondensed,
                FontStyleWidth.Normal => SKFontStyleWidth.Normal,
                FontStyleWidth.SemiExpanded => SKFontStyleWidth.SemiExpanded,
                FontStyleWidth.Expanded => SKFontStyleWidth.Expanded,
                FontStyleWidth.ExtraExpanded => SKFontStyleWidth.ExtraExpanded,
                FontStyleWidth.UltraExpanded => SKFontStyleWidth.UltraExpanded,
                _ => SKFontStyleWidth.UltraCondensed
            };
        }

        public static SKFontStyleSlant ToSKFontStyleSlant(this FontStyleSlant fontStyleSlant)
        {
            return fontStyleSlant switch
            {
                FontStyleSlant.Upright => SKFontStyleSlant.Upright,
                FontStyleSlant.Italic => SKFontStyleSlant.Italic,
                FontStyleSlant.Oblique => SKFontStyleSlant.Oblique,
                _ => SKFontStyleSlant.Upright
            };
        }

        public static SKTypeface? ToSKTypeface(this Typeface? typeface)
        {
            if (typeface is null || typeface.FamilyName is null)
            {
                return SKTypeface.Default;
            }

            var fontFamily = typeface.FamilyName;
            var fontWeight = typeface.Weight.ToSKFontStyleWeight();
            var fontWidth = typeface.Width.ToSKFontStyleWidth();
            var fontStyle = typeface.Style.ToSKFontStyleSlant();

            if (SKSvgSettings.s_typefaceProviders is { } && SKSvgSettings.s_typefaceProviders.Count > 0)
            {
                foreach (var typefaceProviders in SKSvgSettings.s_typefaceProviders)
                {
                    var skTypeface = typefaceProviders.FromFamilyName(fontFamily, fontWeight, fontWidth, fontStyle);
                    if (skTypeface is { })
                    {
                        return skTypeface;
                    }
                }
            }

            return SKTypeface.FromFamilyName(fontFamily, fontWeight, fontWidth, fontStyle);
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

        public static SKColorF ToSKColor(this ColorF color)
        {
            return new SKColorF(color.Red, color.Green, color.Blue, color.Alpha);
        }

        public static SKColorF[] ToSKColors(this ColorF[] colors)
        {
            var skColors = new SKColorF[colors.Length];

            for (int i = 0; i < colors.Length; i++)
            {
                skColors[i] = colors[i].ToSKColor();
            }

            return skColors;
        }

        public static SKShaderTileMode ToSKShaderTileMode(this ShaderTileMode shaderTileMode)
        {
            return shaderTileMode switch
            {
                ShaderTileMode.Clamp => SKShaderTileMode.Clamp,
                ShaderTileMode.Repeat => SKShaderTileMode.Repeat,
                ShaderTileMode.Mirror => SKShaderTileMode.Mirror,
                ShaderTileMode.Decal => SKShaderTileMode.Decal,
                _ => SKShaderTileMode.Clamp
            };
        }

        public static SKShader? ToSKShader(this Shader? shader)
        {
            switch (shader)
            {
                case ColorShader colorShader:
                    {
                        return SKShader.CreateColor(
                            colorShader.Color.ToSKColor(),
                            colorShader.ColorSpace == ColorSpace.Srgb ? SKSvgSettings.s_srgb : SKSvgSettings.s_srgbLinear);
                    }
                case LinearGradientShader linearGradientShader:
                    {
                        if (linearGradientShader.Colors is null || linearGradientShader.ColorPos is null)
                        {
                            return null;
                        }

                        if (linearGradientShader.LocalMatrix is { })
                        {
                            return SKShader.CreateLinearGradient(
                                linearGradientShader.Start.ToSKPoint(),
                                linearGradientShader.End.ToSKPoint(),
                                linearGradientShader.Colors.ToSKColors(),
                                linearGradientShader.ColorSpace == ColorSpace.Srgb ? SKSvgSettings.s_srgb : SKSvgSettings.s_srgbLinear,
                                linearGradientShader.ColorPos,
                                linearGradientShader.Mode.ToSKShaderTileMode(),
                                linearGradientShader.LocalMatrix.Value.ToSKMatrix());
                        }

                        return SKShader.CreateLinearGradient(
                            linearGradientShader.Start.ToSKPoint(),
                            linearGradientShader.End.ToSKPoint(),
                            linearGradientShader.Colors.ToSKColors(),
                            linearGradientShader.ColorSpace == ColorSpace.Srgb ? SKSvgSettings.s_srgb : SKSvgSettings.s_srgbLinear,
                            linearGradientShader.ColorPos,
                            linearGradientShader.Mode.ToSKShaderTileMode());
                    }
                case TwoPointConicalGradientShader twoPointConicalGradientShader:
                    {
                        if (twoPointConicalGradientShader.Colors is null || twoPointConicalGradientShader.ColorPos is null)
                        {
                            return null;
                        }

                        if (twoPointConicalGradientShader.LocalMatrix is { })
                        {
                            return SKShader.CreateTwoPointConicalGradient(
                                twoPointConicalGradientShader.Start.ToSKPoint(),
                                twoPointConicalGradientShader.StartRadius,
                                twoPointConicalGradientShader.End.ToSKPoint(),
                                twoPointConicalGradientShader.EndRadius,
                                twoPointConicalGradientShader.Colors.ToSKColors(),
                                twoPointConicalGradientShader.ColorSpace == ColorSpace.Srgb ? SKSvgSettings.s_srgb : SKSvgSettings.s_srgbLinear,
                                twoPointConicalGradientShader.ColorPos,
                                twoPointConicalGradientShader.Mode.ToSKShaderTileMode(),
                                twoPointConicalGradientShader.LocalMatrix.Value.ToSKMatrix());
                        }

                        return SKShader.CreateTwoPointConicalGradient(
                            twoPointConicalGradientShader.Start.ToSKPoint(),
                            twoPointConicalGradientShader.StartRadius,
                            twoPointConicalGradientShader.End.ToSKPoint(),
                            twoPointConicalGradientShader.EndRadius,
                            twoPointConicalGradientShader.Colors.ToSKColors(),
                            twoPointConicalGradientShader.ColorSpace == ColorSpace.Srgb ? SKSvgSettings.s_srgb : SKSvgSettings.s_srgbLinear,
                            twoPointConicalGradientShader.ColorPos,
                            twoPointConicalGradientShader.Mode.ToSKShaderTileMode());
                    }
                case PictureShader pictureShader:
                    {
                        if (pictureShader.Src is null)
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
                        if (colorMatrixColorFilter.Matrix is null)
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
                        if (tableColorFilter.TableA is null
                            || tableColorFilter.TableR is null
                            || tableColorFilter.TableG is null
                            || tableColorFilter.TableB is null)
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

        public static SKColorChannel ToSKColorChannel(this ColorChannel colorChannel)
        {
            return colorChannel switch
            {
                ColorChannel.R => SKColorChannel.R,
                ColorChannel.G => SKColorChannel.G,
                ColorChannel.B => SKColorChannel.B,
                ColorChannel.A => SKColorChannel.A,
                _ => SKColorChannel.R
            };
        }

        public static SKImageFilter? ToSKImageFilter(this ImageFilter? imageFilter)
        {
            switch (imageFilter)
            {
                case ArithmeticImageFilter arithmeticImageFilter:
                    {
                        if (arithmeticImageFilter.Background is null)
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
                        if (blendModeImageFilter.Background is null)
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
                        if (colorFilterImageFilter.ColorFilter is null)
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
                        if (displacementMapEffectImageFilter.Displacement is null)
                        {
                            return null;
                        }

                        return SKImageFilter.CreateDisplacementMapEffect(
                            displacementMapEffectImageFilter.XChannelSelector.ToSKColorChannel(),
                            displacementMapEffectImageFilter.YChannelSelector.ToSKColorChannel(),
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
                        if (imageImageFilter.Image is null)
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
                        if (matrixConvolutionImageFilter.Kernel is null)
                        {
                            return null;
                        }

                        return SKImageFilter.CreateMatrixConvolution(
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
                        if (mergeImageFilter.Filters is null)
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
                        if (paintImageFilter.Paint is null)
                        {
                            return null;
                        }

                        return SKImageFilter.CreatePaint(
                            paintImageFilter.Paint.ToSKPaint(),
                            paintImageFilter.CropRect?.ToCropRect());
                    }
                case PictureImageFilter pictureImageFilter:
                    {
                        if (pictureImageFilter.Picture is null)
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
            if (imageFilters is null)
            {
                return null;
            }

            var skImageFilters = new SKImageFilter[imageFilters.Length];

            for (int i = 0; i < imageFilters.Length; i++)
            {
                var imageFilter = imageFilters[i];
                var skImageFilter = imageFilter.ToSKImageFilter();
                if (skImageFilter is { })
                {
                    skImageFilters[i] = skImageFilter;
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
            return blendMode switch
            {
                BlendMode.Clear => SKBlendMode.Clear,
                BlendMode.Src => SKBlendMode.Src,
                BlendMode.Dst => SKBlendMode.Dst,
                BlendMode.SrcOver => SKBlendMode.SrcOver,
                BlendMode.DstOver => SKBlendMode.DstOver,
                BlendMode.SrcIn => SKBlendMode.SrcIn,
                BlendMode.DstIn => SKBlendMode.DstIn,
                BlendMode.SrcOut => SKBlendMode.SrcOut,
                BlendMode.DstOut => SKBlendMode.DstOut,
                BlendMode.SrcATop => SKBlendMode.SrcATop,
                BlendMode.DstATop => SKBlendMode.DstATop,
                BlendMode.Xor => SKBlendMode.Xor,
                BlendMode.Plus => SKBlendMode.Plus,
                BlendMode.Modulate => SKBlendMode.Modulate,
                BlendMode.Screen => SKBlendMode.Screen,
                BlendMode.Overlay => SKBlendMode.Overlay,
                BlendMode.Darken => SKBlendMode.Darken,
                BlendMode.Lighten => SKBlendMode.Lighten,
                BlendMode.ColorDodge => SKBlendMode.ColorDodge,
                BlendMode.ColorBurn => SKBlendMode.ColorBurn,
                BlendMode.HardLight => SKBlendMode.HardLight,
                BlendMode.SoftLight => SKBlendMode.SoftLight,
                BlendMode.Difference => SKBlendMode.Difference,
                BlendMode.Exclusion => SKBlendMode.Exclusion,
                BlendMode.Multiply => SKBlendMode.Multiply,
                BlendMode.Hue => SKBlendMode.Hue,
                BlendMode.Saturation => SKBlendMode.Saturation,
                BlendMode.Color => SKBlendMode.Color,
                BlendMode.Luminosity => SKBlendMode.Luminosity,
                _ => SKBlendMode.Clear
            };
        }

        public static SKFilterQuality ToSKFilterQuality(this FilterQuality filterQuality)
        {
            return filterQuality switch
            {
                FilterQuality.None => SKFilterQuality.None,
                FilterQuality.Low => SKFilterQuality.Low,
                FilterQuality.Medium => SKFilterQuality.Medium,
                FilterQuality.High => SKFilterQuality.High,
                _ => SKFilterQuality.None
            };
        }

        public static SKPaint ToSKPaint(this Paint paint)
        {
            var style = paint.Style.ToSKPaintStyle();
            var strokeCap = paint.StrokeCap.ToSKStrokeCap();
            var strokeJoin = paint.StrokeJoin.ToSKStrokeJoin();
            var textAlign = paint.TextAlign.ToSKTextAlign();
            var typeface = paint.Typeface?.ToSKTypeface();
            var textEncoding = paint.TextEncoding.ToSKTextEncoding();
            var color = paint.Color is null ? SKColor.Empty : ToSKColor(paint.Color.Value);
            var shader = paint.Shader?.ToSKShader();
            var colorFilter = paint.ColorFilter?.ToSKColorFilter();
            var imageFilter = paint.ImageFilter?.ToSKImageFilter();
            var pathEffect = paint.PathEffect?.ToSKPathEffect();
            var blendMode = paint.BlendMode.ToSKBlendMode();
            var filterQuality = paint.FilterQuality.ToSKFilterQuality();
            return new SKPaint
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
            return clipOperation switch
            {
                ClipOperation.Difference => SKClipOperation.Difference,
                ClipOperation.Intersect => SKClipOperation.Intersect,
                _ => SKClipOperation.Difference
            };
        }

        public static SKPathFillType ToSKPathFillType(this PathFillType pathFillType)
        {
            return pathFillType switch
            {
                PathFillType.Winding => SKPathFillType.Winding,
                PathFillType.EvenOdd => SKPathFillType.EvenOdd,
                _ => SKPathFillType.Winding
            };
        }

        public static SKPathArcSize ToSKPathArcSize(this PathArcSize pathArcSize)
        {
            return pathArcSize switch
            {
                PathArcSize.Small => SKPathArcSize.Small,
                PathArcSize.Large => SKPathArcSize.Large,
                _ => SKPathArcSize.Small
            };
        }

        public static SKPathDirection ToSKPathDirection(this PathDirection pathDirection)
        {
            return pathDirection switch
            {
                PathDirection.Clockwise => SKPathDirection.Clockwise,
                PathDirection.CounterClockwise => SKPathDirection.CounterClockwise,
                _ => SKPathDirection.Clockwise
            };
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

        public static SKPath ToSKPath(this Path path)
        {
            var skPath = new SKPath
            {
                FillType = path.FillType.ToSKPathFillType()
            };

            if (path.Commands is null)
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
            if (clipPath.Clips is null)
            {
                return null;
            }

            var skPathResult = default(SKPath);

            foreach (var clip in clipPath.Clips)
            {
                if (clip.Path is null)
                {
                    return null;
                }

                var skPath = clip.Path.ToSKPath();
                var skPathClip = clip.Clip?.ToSKPath();
                if (skPathClip is { })
                {
                    skPath = skPath.Op(skPathClip, SKPathOp.Intersect);
                }

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
                    var result = skPathResult.Op(skPath, SKPathOp.Union);
                    skPathResult = result;
                }
            }

            if (skPathResult is { })
            {
                if (clipPath.Clip?.Clips is { })
                {
                    var skPathClip = clipPath.Clip.ToSKPath();
                    if (skPathClip is { })
                    {
                        skPathResult = skPathResult.Op(skPathClip, SKPathOp.Intersect);
                    }
                }

                if (clipPath.Transform is { })
                {
                    var skMatrix = clipPath.Transform.Value.ToSKMatrix();
                    skPathResult.Transform(skMatrix);
                }
            }

            return skPathResult;
        }

        public static SKPicture? ToSKPicture(this Picture? picture)
        {
            if (picture is null)
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
                            var textBlob = SKTextBlob.CreatePositioned(text, font, points);
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

        public static void Draw(this Picture picture, SKCanvas skCanvas)
        {
            if (picture.Commands is null)
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
