//#define USE_DISPOSE_TYPEFACE
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ShimSkiaSharp;

namespace Svg.CodeGen.Skia;

public static class SkiaCSharpModelExtensions
{
    private static string s_srgbLinear = "SKColorSpace.CreateRgb(SKColorSpaceTransferFn.Linear, SKColorSpaceXyz.Srgb)";

    private static string s_srgb = "SKColorSpace.CreateRgb(SKColorSpaceTransferFn.Srgb, SKColorSpaceXyz.Srgb)";

    private static readonly CultureInfo s_ci = CultureInfo.InvariantCulture;

    private static readonly char[] s_fontFamilyTrim = ['\''];

    public static string ToBoolString(this bool value) => value ? "true" : "false";

    public static string ToByteString(this byte value)
    {
        return value.ToString(s_ci);
    }

    public static string ToIntString(this int value)
    {
        return value.ToString(s_ci);
    }

    public static string ToFloatString(this float value)
    {
        if (float.IsNaN(value) || float.IsNegativeInfinity(value) || float.IsPositiveInfinity(value))
        {
            return string.Concat("float.", value.ToString(s_ci));
        }
        return string.Concat(value.ToString(s_ci), "f");
    }

    private static string? EspaceString(string? text)
    {
        return text?.Replace("\"", "\\\"");
    }

    public static StringBuilder ToByteArray(this byte[] array)
    {
        // Each byte is 1 to 3 chars. Add trailing comma and space, then 5 char is expected for each byte in the array.
        // Around the bytes are 15 constant chars, plus an unknown array size - let's do 7 for good measure.
        var sb = new StringBuilder("new byte[", array.Length * 5 + 22).AppendFormat(s_ci, "{0}] {{ ", array.Length);

        for (int i = 0; i < array.Length; i++)
        {
            sb.AppendFormat(s_ci, "{0}, ", array[i]); // C# allows trailing , on end element
        }

        sb.Append(" }");

        return sb;
    }

    public static StringBuilder ToFloatArray(this float[] array)
    {
        var sb = new StringBuilder("new float[").AppendFormat(s_ci, "{0}] {{ ", array.Length);

        for (int i = 0; i < array.Length; i++)
        {
            sb.AppendFormat(s_ci, "{0:g}f, ", array[i]); // C# allows trailing , on end element
        }

        sb.Append(" }");

        return sb;
    }

    public static StringBuilder ToStringArray(this string[] array)
    {
        var sb = new StringBuilder("new string[").AppendFormat(s_ci, "{0}] {{ ", array.Length);

        for (int i = 0; i < array.Length; i++)
        {
            sb.AppendFormat(s_ci, "@\"{0}\", ", array[i]); // C# allows trailing , on end element
        }

        sb.Append(" }");

        return sb;
    }

    public static string ToSKPoint(this SKPoint point)
    {
        return $"new SKPoint({point.X.ToFloatString()}, {point.Y.ToFloatString()})";
    }

    public static string ToSKPoint3(this SKPoint3 point3)
    {
        return $"new SKPoint3({point3.X.ToFloatString()}, {point3.Y.ToFloatString()}, {point3.Z.ToFloatString()})";
    }

    public static string ToSKPoints(this IList<SKPoint> points)
    {
        var result = $"new SKPoint[{points.Count}] {{ ";

        for (int i = 0; i < points.Count; i++)
        {
            result += points[i].ToSKPoint();

            if (points.Count > 0 && i < points.Count - 1)
            {
                result += $", ";
            }
        }

        result += $" }}";

        return result;
    }

    public static string ToSKPointI(this SKPointI pointI)
    {
        return $"new SKPointI({pointI.X.ToIntString()}, {pointI.Y.ToIntString()})";
    }

    public static string ToSKSize(this SKSize size)
    {
        return $"new SKSize({size.Width.ToFloatString()}, {size.Height.ToFloatString()})";
    }

    public static string ToSKSizeI(this SKSizeI sizeI)
    {
        return $"new SKSizeI({sizeI.Width.ToIntString()}, {sizeI.Height.ToIntString()})";
    }

    public static string ToSKRect(this SKRect rect)
    {
        return $"new SKRect({rect.Left.ToFloatString()}, {rect.Top.ToFloatString()}, {rect.Right.ToFloatString()}, {rect.Bottom.ToFloatString()})";
    }

    public static string ToSKMatrix(this SKMatrix matrix)
    {
        return $"new SKMatrix({matrix.ScaleX.ToFloatString()}, {matrix.SkewX.ToFloatString()}, {matrix.TransX.ToFloatString()}, {matrix.SkewY.ToFloatString()}, {matrix.ScaleY.ToFloatString()}, {matrix.TransY.ToFloatString()}, {matrix.Persp0.ToFloatString()}, {matrix.Persp1.ToFloatString()}, {matrix.Persp2.ToFloatString()})";
    }

    public static void ToSKImage(this SKImage image, SkiaCSharpCodeGenCounter counter, StringBuilder sb, string indent)
    {
        var counterImage = counter.Image;

        if (image.Data is null)
        {
            sb.AppendLine($"{indent}var {counter.ImageVarName}{counterImage} = default(SKImage);");
            return;
        }

        sb.Append($"{indent}var {counter.ImageVarName}{counterImage} = ");
        sb.AppendLine($"SKImage.FromEncodedData({image.Data.ToByteArray()});");
    }

    public static string ToSKPaintStyle(this SKPaintStyle paintStyle)
    {
        switch (paintStyle)
        {
            default:
            case SKPaintStyle.Fill:
                return "SKPaintStyle.Fill";
            case SKPaintStyle.Stroke:
                return "SKPaintStyle.Stroke";
            case SKPaintStyle.StrokeAndFill:
                return "SKPaintStyle.StrokeAndFill";
        }
    }

    public static string ToSKStrokeCap(this SKStrokeCap strokeCap)
    {
        switch (strokeCap)
        {
            default:
            case SKStrokeCap.Butt:
                return "SKStrokeCap.Butt";
            case SKStrokeCap.Round:
                return "SKStrokeCap.Round";
            case SKStrokeCap.Square:
                return "SKStrokeCap.Square";
        }
    }

    public static string ToSKStrokeJoin(this SKStrokeJoin strokeJoin)
    {
        switch (strokeJoin)
        {
            default:
            case SKStrokeJoin.Miter:
                return "SKStrokeJoin.Miter";
            case SKStrokeJoin.Round:
                return "SKStrokeJoin.Round";
            case SKStrokeJoin.Bevel:
                return "SKStrokeJoin.Bevel";
        }
    }

    public static string ToSKTextAlign(this SKTextAlign textAlign)
    {
        switch (textAlign)
        {
            default:
            case SKTextAlign.Left:
                return "SKTextAlign.Left";
            case SKTextAlign.Center:
                return "SKTextAlign.Center";
            case SKTextAlign.Right:
                return "SKTextAlign.Right";
        }
    }

    public static string ToSKTextEncoding(this SKTextEncoding textEncoding)
    {
        switch (textEncoding)
        {
            default:
            case SKTextEncoding.Utf8:
                return "SKTextEncoding.Utf8";
            case SKTextEncoding.Utf16:
                return "SKTextEncoding.Utf16";
            case SKTextEncoding.Utf32:
                return "SKTextEncoding.Utf32";
            case SKTextEncoding.GlyphId:
                return "SKTextEncoding.GlyphId";
        }
    }

    public static string ToSKFontStyleWeight(this SKFontStyleWeight fontStyleWeight)
    {
        switch (fontStyleWeight)
        {
            default:
            case SKFontStyleWeight.Invisible:
                return "SKFontStyleWeight.Invisible";
            case SKFontStyleWeight.Thin:
                return "SKFontStyleWeight.Thin";
            case SKFontStyleWeight.ExtraLight:
                return "SKFontStyleWeight.ExtraLight";
            case SKFontStyleWeight.Light:
                return "SKFontStyleWeight.Light";
            case SKFontStyleWeight.Normal:
                return "SKFontStyleWeight.Normal";
            case SKFontStyleWeight.Medium:
                return "SKFontStyleWeight.Medium";
            case SKFontStyleWeight.SemiBold:
                return "SKFontStyleWeight.SemiBold";
            case SKFontStyleWeight.Bold:
                return "SKFontStyleWeight.Bold";
            case SKFontStyleWeight.ExtraBold:
                return "SKFontStyleWeight.ExtraBold";
            case SKFontStyleWeight.Black:
                return "SKFontStyleWeight.Black";
            case SKFontStyleWeight.ExtraBlack:
                return "SKFontStyleWeight.ExtraBlack";
        }
    }

    public static string ToSKFontStyleWidth(this SKFontStyleWidth fontStyleWidth)
    {
        switch (fontStyleWidth)
        {
            default:
            case SKFontStyleWidth.UltraCondensed:
                return "SKFontStyleWidth.UltraCondensed";
            case SKFontStyleWidth.ExtraCondensed:
                return "SKFontStyleWidth.ExtraCondensed";
            case SKFontStyleWidth.Condensed:
                return "SKFontStyleWidth.Condensed";
            case SKFontStyleWidth.SemiCondensed:
                return "SKFontStyleWidth.SemiCondensed";
            case SKFontStyleWidth.Normal:
                return "SKFontStyleWidth.Normal";
            case SKFontStyleWidth.SemiExpanded:
                return "SKFontStyleWidth.SemiExpanded";
            case SKFontStyleWidth.Expanded:
                return "SKFontStyleWidth.Expanded";
            case SKFontStyleWidth.ExtraExpanded:
                return "SKFontStyleWidth.ExtraExpanded";
            case SKFontStyleWidth.UltraExpanded:
                return "SKFontStyleWidth.UltraExpanded";
        }
    }

    public static string ToSKFontStyleSlant(this SKFontStyleSlant fontStyleSlant)
    {
        switch (fontStyleSlant)
        {
            default:
            case SKFontStyleSlant.Upright:
                return "SKFontStyleSlant.Upright";
            case SKFontStyleSlant.Italic:
                return "SKFontStyleSlant.Italic";
            case SKFontStyleSlant.Oblique:
                return "SKFontStyleSlant.Oblique";
        }
    }

    public static void ToSKTypeface(this SKTypeface? typeface, SkiaCSharpCodeGenCounter counter, StringBuilder sb, string indent)
    {
        var counterTypeface = counter.Typeface;

        if (typeface is null || typeface.FamilyName is null)
        {
            sb.AppendLine($"{indent}var {counter.TypefaceVarName}{counterTypeface} = SKTypeface.Default;");
            return;
        }

        var fontFamily = typeface.FamilyName;
        var fontWeight = typeface.FontWeight.ToSKFontStyleWeight();
        var fontWidth = typeface.FontWidth.ToSKFontStyleWidth();
        var fontStyle = typeface.FontSlant.ToSKFontStyleSlant();

        var fontFamilyNames = fontFamily?.Split(',')?.Select(x => x.Trim().Trim(s_fontFamilyTrim))?.ToArray();
        if (fontFamilyNames is null || fontFamilyNames.Length == 0)
        {
            sb.AppendLine($"{indent}var {counter.TypefaceVarName}{counterTypeface} = SKTypeface.Default;");
            return;
        }

        sb.AppendLine($"{indent}var {counter.TypefaceVarName}{counterTypeface} = default(SKTypeface);");
        sb.AppendLine($"{indent}var fontFamilyNames{counterTypeface} = {fontFamilyNames?.ToStringArray()};");
        sb.AppendLine($"{indent}var defaultName{counterTypeface} = SKTypeface.Default.FamilyName;");
        sb.AppendLine($"{indent}var {counter.FontManagerVarName}{counterTypeface} = SKFontManager.Default;");
        sb.AppendLine($"{indent}var {counter.FontStyleVarName}{counterTypeface} = new SKFontStyle({fontWeight}, {fontWidth}, {fontStyle});");
        sb.AppendLine($"{indent}foreach (var fontFamilyName{counterTypeface} in fontFamilyNames{counterTypeface})");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    var {counter.FontStyleSetVarName}{counterTypeface} = {counter.FontManagerVarName}{counterTypeface}.GetFontStyles(fontFamilyName{counterTypeface});");
        sb.AppendLine($"{indent}    if ({counter.FontStyleSetVarName}{counterTypeface}.Count > 0)");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        {counter.TypefaceVarName}{counterTypeface} = {counter.FontManagerVarName}{counterTypeface}.MatchFamily(fontFamilyName{counterTypeface}, {counter.FontStyleVarName}{counterTypeface});");
        sb.AppendLine($"{indent}        if ({counter.TypefaceVarName}{counterTypeface} is {{ }})");
        sb.AppendLine($"{indent}        {{");
        sb.AppendLine($"{indent}            if (!defaultName{counterTypeface}.Equals(fontFamilyName{counterTypeface}, StringComparison.Ordinal)");
        sb.AppendLine($"{indent}                && defaultName{counterTypeface}.Equals({counter.TypefaceVarName}{counterTypeface}.FamilyName, StringComparison.Ordinal))");
        sb.AppendLine($"{indent}            {{");
#if USE_DISPOSE_TYPEFACE
            sb.AppendLine($"{indent}                {counter.TypefaceVarName}{counterTypeface}?.Dispose();");  
#endif
        sb.AppendLine($"{indent}                {counter.TypefaceVarName}{counterTypeface} = null;");
        sb.AppendLine($"{indent}                continue;");
        sb.AppendLine($"{indent}            }}");
        sb.AppendLine($"{indent}            break;");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}}");
    }

    public static string ToSKColor(this SKColor color)
    {
        return $"new SKColor({color.Red}, {color.Green}, {color.Blue}, {color.Alpha})";
    }

    public static string ToSKColor(this SKColorF color)
    {
        return $"new SKColorF({color.Red.ToFloatString()}, {color.Green.ToFloatString()}, {color.Blue.ToFloatString()}, {color.Alpha.ToFloatString()})";
    }

    public static string ToSKColors(this SKColor[] colors)
    {
        var skColors = $"new SKColor[{colors.Length}] {{ ";

        for (int i = 0; i < colors.Length; i++)
        {
            skColors += colors[i].ToSKColor();

            if (colors.Length > 0 && i < colors.Length - 1)
            {
                skColors += $", ";
            }
        }

        skColors += $" }}";

        return skColors;
    }

    public static string ToSKColorF(this SKColorF color)
    {
        return $"new SKColorF({color.Red.ToFloatString()}, {color.Green.ToFloatString()}, {color.Blue.ToFloatString()}, {color.Alpha.ToFloatString()})";
    }

    public static string ToSKColors(this SKColorF[] colors)
    {
        var skColors = $"new SKColorF[{colors.Length}] {{ ";

        for (int i = 0; i < colors.Length; i++)
        {
            skColors += colors[i].ToSKColorF();

            if (colors.Length > 0 && i < colors.Length - 1)
            {
                skColors += $", ";
            }
        }

        skColors += $" }}";

        return skColors;
    }

    public static string ToSKShaderTileMode(this SKShaderTileMode shaderTileMode)
    {
        switch (shaderTileMode)
        {
            default:
            case SKShaderTileMode.Clamp:
                return "SKShaderTileMode.Clamp";
            case SKShaderTileMode.Repeat:
                return "SKShaderTileMode.Repeat";
            case SKShaderTileMode.Mirror:
                return "SKShaderTileMode.Mirror";
            case SKShaderTileMode.Decal:
                return "SKShaderTileMode.Decal";
        }
    }

    public static void ToSKShader(this SKShader? shader, SkiaCSharpCodeGenCounter counter, StringBuilder sb, string indent)
    {
        var counterShader = counter.Shader;

        switch (shader)
        {
            case ColorShader colorShader:
            {
                sb.Append($"{indent}var {counter.ShaderVarName}{counterShader} = ");
                sb.AppendLine($"SKShader.CreateColor(");
                sb.AppendLine($"{indent}    {colorShader.Color.ToSKColor()},");
                sb.AppendLine($"{indent}    {(colorShader.ColorSpace == SKColorSpace.Srgb ? s_srgb : s_srgbLinear)});");
                return;
            }
            case LinearGradientShader linearGradientShader:
            {
                if (linearGradientShader.Colors is null || linearGradientShader.ColorPos is null)
                {
                    sb.AppendLine($"{indent}var {counter.ShaderVarName}{counterShader} = default(SKShader);");
                    return;
                }

                if (linearGradientShader.LocalMatrix is { })
                {
                    sb.Append($"{indent}var {counter.ShaderVarName}{counterShader} = ");
                    sb.AppendLine($"SKShader.CreateLinearGradient(");
                    sb.AppendLine($"{indent}    {linearGradientShader.Start.ToSKPoint()},");
                    sb.AppendLine($"{indent}    {linearGradientShader.End.ToSKPoint()},");
                    sb.AppendLine($"{indent}    {linearGradientShader.Colors.ToSKColors()},");
                    sb.AppendLine($"{indent}    {(linearGradientShader.ColorSpace == SKColorSpace.Srgb ? s_srgb : s_srgbLinear)},");
                    sb.AppendLine($"{indent}    {linearGradientShader.ColorPos.ToFloatArray()},");
                    sb.AppendLine($"{indent}    {linearGradientShader.Mode.ToSKShaderTileMode()},");
                    sb.AppendLine($"{indent}    {linearGradientShader.LocalMatrix.Value.ToSKMatrix()});");
                    return;
                }
                else
                {
                    sb.Append($"{indent}var {counter.ShaderVarName}{counterShader} = ");
                    sb.AppendLine($"SKShader.CreateLinearGradient(");
                    sb.AppendLine($"{indent}    {linearGradientShader.Start.ToSKPoint()},");
                    sb.AppendLine($"{indent}    {linearGradientShader.End.ToSKPoint()},");
                    sb.AppendLine($"{indent}    {linearGradientShader.Colors.ToSKColors()},");
                    sb.AppendLine($"{indent}    {(linearGradientShader.ColorSpace == SKColorSpace.Srgb ? s_srgb : s_srgbLinear)},");
                    sb.AppendLine($"{indent}    {linearGradientShader.ColorPos.ToFloatArray()},");
                    sb.AppendLine($"{indent}    {linearGradientShader.Mode.ToSKShaderTileMode()});");
                    return;
                }
            }
            case RadialGradientShader radialGradientShader:
            {
                if (radialGradientShader.Colors is null || radialGradientShader.ColorPos is null)
                {
                    sb.AppendLine($"{indent}var {counter.ShaderVarName}{counterShader} = default(SKShader);");
                    return;
                }

                if (radialGradientShader.LocalMatrix is { })
                {
                    sb.Append($"{indent}var {counter.ShaderVarName}{counterShader} = ");
                    sb.AppendLine($"SKShader.CreateRadialGradient(");
                    sb.AppendLine($"{indent}    {radialGradientShader.Center.ToSKPoint()},");
                    sb.AppendLine($"{indent}    {radialGradientShader.Radius.ToFloatString()},");
                    sb.AppendLine($"{indent}    {radialGradientShader.Colors.ToSKColors()},");
                    sb.AppendLine($"{indent}    {(radialGradientShader.ColorSpace == SKColorSpace.Srgb ? s_srgb : s_srgbLinear)},");
                    sb.AppendLine($"{indent}    {radialGradientShader.ColorPos.ToFloatArray()},");
                    sb.AppendLine($"{indent}    {radialGradientShader.Mode.ToSKShaderTileMode()},");
                    sb.AppendLine($"{indent}    {radialGradientShader.LocalMatrix.Value.ToSKMatrix()});");
                    return;
                }
                else
                {
                    sb.Append($"{indent}var {counter.ShaderVarName}{counterShader} = ");
                    sb.AppendLine($"SKShader.CreateRadialGradient(");
                    sb.AppendLine($"{indent}    {radialGradientShader.Center.ToSKPoint()},");
                    sb.AppendLine($"{indent}    {radialGradientShader.Radius.ToFloatString()},");
                    sb.AppendLine($"{indent}    {radialGradientShader.Colors.ToSKColors()},");
                    sb.AppendLine($"{indent}    {(radialGradientShader.ColorSpace == SKColorSpace.Srgb ? s_srgb : s_srgbLinear)},");
                    sb.AppendLine($"{indent}    {radialGradientShader.ColorPos.ToFloatArray()},");
                    sb.AppendLine($"{indent}    {radialGradientShader.Mode.ToSKShaderTileMode()});");
                    return;
                }
            }
            case TwoPointConicalGradientShader twoPointConicalGradientShader:
            {
                if (twoPointConicalGradientShader.Colors is null || twoPointConicalGradientShader.ColorPos is null)
                {
                    sb.AppendLine($"{indent}var {counter.ShaderVarName}{counterShader} = default(SKShader);");
                    return;
                }

                if (twoPointConicalGradientShader.LocalMatrix is { })
                {
                    sb.Append($"{indent}var {counter.ShaderVarName}{counterShader} = ");
                    sb.AppendLine($"SKShader.CreateTwoPointConicalGradient(");
                    sb.AppendLine($"{indent}    {twoPointConicalGradientShader.Start.ToSKPoint()},");
                    sb.AppendLine($"{indent}    {twoPointConicalGradientShader.StartRadius.ToFloatString()},");
                    sb.AppendLine($"{indent}    {twoPointConicalGradientShader.End.ToSKPoint()},");
                    sb.AppendLine($"{indent}    {twoPointConicalGradientShader.EndRadius.ToFloatString()},");
                    sb.AppendLine($"{indent}    {twoPointConicalGradientShader.Colors.ToSKColors()},");
                    sb.AppendLine($"{indent}    {(twoPointConicalGradientShader.ColorSpace == SKColorSpace.Srgb ? s_srgb : s_srgbLinear)},");
                    sb.AppendLine($"{indent}    {twoPointConicalGradientShader.ColorPos.ToFloatArray()},");
                    sb.AppendLine($"{indent}    {twoPointConicalGradientShader.Mode.ToSKShaderTileMode()},");
                    sb.AppendLine($"{indent}    {twoPointConicalGradientShader.LocalMatrix.Value.ToSKMatrix()});");
                    return;
                }
                else
                {
                    sb.Append($"{indent}var {counter.ShaderVarName}{counterShader} = ");
                    sb.AppendLine($"SKShader.CreateTwoPointConicalGradient(");
                    sb.AppendLine($"{indent}    {twoPointConicalGradientShader.Start.ToSKPoint()},");
                    sb.AppendLine($"{indent}    {twoPointConicalGradientShader.StartRadius.ToFloatString()},");
                    sb.AppendLine($"{indent}    {twoPointConicalGradientShader.End.ToSKPoint()},");
                    sb.AppendLine($"{indent}    {twoPointConicalGradientShader.EndRadius.ToFloatString()},");
                    sb.AppendLine($"{indent}    {twoPointConicalGradientShader.Colors.ToSKColors()},");
                    sb.AppendLine($"{indent}    {(twoPointConicalGradientShader.ColorSpace == SKColorSpace.Srgb ? s_srgb : s_srgbLinear)},");
                    sb.AppendLine($"{indent}    {twoPointConicalGradientShader.ColorPos.ToFloatArray()},");
                    sb.AppendLine($"{indent}    {twoPointConicalGradientShader.Mode.ToSKShaderTileMode()});");
                    return;
                }
            }
            case PictureShader pictureShader:
            {
                if (pictureShader.Src is null)
                {
                    sb.AppendLine($"{indent}var {counter.ShaderVarName}{counterShader} = default(SKShader);");
                    return;
                }

                var counterPicture = ++counter.Picture;
                pictureShader.Src.ToSKPicture(counter, sb, indent);

                sb.Append($"{indent}var {counter.ShaderVarName}{counterShader} = ");
                sb.AppendLine($"SKShader.CreatePicture(");
                sb.AppendLine($"{indent}    {counter.PictureVarName}{counterPicture},");
                sb.AppendLine($"{indent}    SKShaderTileMode.Repeat,");
                sb.AppendLine($"{indent}    SKShaderTileMode.Repeat,");
                sb.AppendLine($"{indent}    {pictureShader.LocalMatrix.ToSKMatrix()},");
                sb.AppendLine($"{indent}    {pictureShader.Tile.ToSKRect()});");
                sb.AppendLine($"{indent}{counter.PictureVarName}{counterPicture}?.Dispose();");
                return;
            }
            case PerlinNoiseFractalNoiseShader perlinNoiseFractalNoiseShader:
            {
                sb.Append($"{indent}var {counter.ShaderVarName}{counterShader} = ");
                sb.AppendLine($"SKShader.CreatePerlinNoiseFractalNoise(");
                sb.AppendLine($"{indent}    {perlinNoiseFractalNoiseShader.BaseFrequencyX.ToFloatString()},");
                sb.AppendLine($"{indent}    {perlinNoiseFractalNoiseShader.BaseFrequencyY.ToFloatString()},");
                sb.AppendLine($"{indent}    {perlinNoiseFractalNoiseShader.NumOctaves.ToIntString()},");
                sb.AppendLine($"{indent}    {perlinNoiseFractalNoiseShader.Seed.ToFloatString()},");
                sb.AppendLine($"{indent}    {perlinNoiseFractalNoiseShader.TileSize.ToSKPointI()});");
                return;
            }
            case PerlinNoiseTurbulenceShader perlinNoiseTurbulenceShader:
            {
                sb.Append($"{indent}var {counter.ShaderVarName}{counterShader} = ");
                sb.AppendLine($"SKShader.CreatePerlinNoiseTurbulence(");
                sb.AppendLine($"{indent}    {perlinNoiseTurbulenceShader.BaseFrequencyX.ToFloatString()},");
                sb.AppendLine($"{indent}    {perlinNoiseTurbulenceShader.BaseFrequencyY.ToFloatString()},");
                sb.AppendLine($"{indent}    {perlinNoiseTurbulenceShader.NumOctaves.ToIntString()},");
                sb.AppendLine($"{indent}    {perlinNoiseTurbulenceShader.Seed.ToFloatString()},");
                sb.AppendLine($"{indent}    {perlinNoiseTurbulenceShader.TileSize.ToSKPointI()});");
                return;
            }
            default:
            {
                sb.AppendLine($"{indent}var {counter.ShaderVarName}{counterShader} = default(SKShader);");
                return;
            }
        }
    }

    public static void ToSKColorFilter(this SKColorFilter? colorFilter, SkiaCSharpCodeGenCounter counter, StringBuilder sb, string indent)
    {
        var counterColorFilter = counter.ColorFilter;

        switch (colorFilter)
        {
            case BlendModeColorFilter blendModeColorFilter:
            {
                sb.Append($"{indent}var {counter.ColorFilterVarName}{counterColorFilter} = ");
                sb.AppendLine($"SKColorFilter.CreateBlendMode(");
                sb.AppendLine($"{indent}    {blendModeColorFilter.Color.ToSKColor()},");
                sb.AppendLine($"{indent}    {blendModeColorFilter.Mode.ToSKBlendMode()});");
                return;
            }
            case ColorMatrixColorFilter colorMatrixColorFilter:
            {
                if (colorMatrixColorFilter.Matrix is null)
                {
                    sb.AppendLine($"{indent}var {counter.ColorFilterVarName}{counterColorFilter} = default(SKColorFilter);");
                    return;
                }

                sb.Append($"{indent}var {counter.ColorFilterVarName}{counterColorFilter} = ");
                sb.AppendLine($"SKColorFilter.CreateColorMatrix(");
                sb.AppendLine($"{indent}    {colorMatrixColorFilter.Matrix.ToFloatArray()});");
                return;
            }
            case LumaColorColorFilter _:
            {
                sb.Append($"{indent}var {counter.ColorFilterVarName}{counterColorFilter} = ");
                sb.AppendLine($"SKColorFilter.CreateLumaColor();");
                return;
            }
            case TableColorFilter tableColorFilter:
            {
                if (tableColorFilter.TableA is null
                    || tableColorFilter.TableR is null
                    || tableColorFilter.TableG is null
                    || tableColorFilter.TableB is null)
                {
                    sb.AppendLine($"{indent}var {counter.ColorFilterVarName}{counterColorFilter} = default(SKColorFilter);");
                    return;
                }

                sb.Append($"{indent}var {counter.ColorFilterVarName}{counterColorFilter} = ");
                sb.AppendLine($"SKColorFilter.CreateTable(");
                sb.AppendLine($"{indent}    {tableColorFilter.TableA.ToByteArray()},");
                sb.AppendLine($"{indent}    {tableColorFilter.TableR.ToByteArray()},");
                sb.AppendLine($"{indent}    {tableColorFilter.TableG.ToByteArray()},");
                sb.AppendLine($"{indent}    {tableColorFilter.TableB.ToByteArray()});");
                return;
            }
            default:
            {
                sb.AppendLine($"{indent}var {counter.ColorFilterVarName}{counterColorFilter} = default(SKColorFilter);");
                return;
            }
        }
    }

    public static string ToCropRect(this SKImageFilter.CropRect cropRect)
    {
        return $"new SKImageFilter.CropRect({cropRect.Rect.ToSKRect()})";
    }

    public static string ToSKColorChannel(this SKColorChannel colorChannel)
    {
        switch (colorChannel)
        {
            default:
            case SKColorChannel.R:
                return "SKColorChannel.R";
            case SKColorChannel.G:
                return "SKColorChannel.G";
            case SKColorChannel.B:
                return "SKColorChannel.B";
            case SKColorChannel.A:
                return "SKColorChannel.A";
        }
    }

    public static void ToSKImageFilter(this SKImageFilter? imageFilter, SkiaCSharpCodeGenCounter counter, StringBuilder sb, string indent)
    {
        var counterImageFilter = counter.ImageFilter;

        switch (imageFilter)
        {
            case ArithmeticImageFilter arithmeticImageFilter:
            {
                if (arithmeticImageFilter.Background is null)
                {
                    sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = default(SKImageFilter);");
                    return;
                }

                var counterImageFilterBackground = ++counter.ImageFilter;
                if (arithmeticImageFilter.Background is null)
                {
                    sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilterBackground} = default(SKImageFilter);");
                }
                else
                {
                    arithmeticImageFilter.Background.ToSKImageFilter(counter, sb, indent);
                }

                var counterImageFilterForeground = ++counter.ImageFilter;
                if (arithmeticImageFilter.Foreground is null)
                {
                    sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilterBackground} = default(SKImageFilter);");
                }
                else
                {
                    arithmeticImageFilter.Foreground.ToSKImageFilter(counter, sb, indent);
                }

                sb.Append($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = ");
                sb.AppendLine($"SKImageFilter.CreateArithmetic(");
                sb.AppendLine($"{indent}    {arithmeticImageFilter.K1.ToFloatString()},");
                sb.AppendLine($"{indent}    {arithmeticImageFilter.K2.ToFloatString()},");
                sb.AppendLine($"{indent}    {arithmeticImageFilter.K3.ToFloatString()},");
                sb.AppendLine($"{indent}    {arithmeticImageFilter.K4.ToFloatString()},");
                sb.AppendLine($"{indent}    {arithmeticImageFilter.EforcePMColor.ToBoolString()},");
                sb.AppendLine($"{indent}    {counter.ImageFilterVarName}{counterImageFilterBackground},");
                sb.AppendLine($"{indent}    {counter.ImageFilterVarName}{counterImageFilterForeground},");
                sb.AppendLine($"{indent}    {arithmeticImageFilter.Clip?.ToCropRect() ?? "null"});");
                return;
            }
            case BlendModeImageFilter blendModeImageFilter:
            {
                if (blendModeImageFilter.Background is null)
                {
                    sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = default(SKImageFilter);");
                    return;
                }

                var counterImageFilterBackground = ++counter.ImageFilter;
                if (blendModeImageFilter.Background is null)
                {
                    sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilterBackground} = default(SKImageFilter);");
                }
                else
                {
                    blendModeImageFilter.Background.ToSKImageFilter(counter, sb, indent);
                }

                var counterImageFilterForeground = ++counter.ImageFilter;
                if (blendModeImageFilter.Foreground is null)
                {
                    sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilterBackground} = default(SKImageFilter);");
                }
                else
                {
                    blendModeImageFilter.Foreground.ToSKImageFilter(counter, sb, indent);
                }

                sb.Append($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = ");
                sb.AppendLine($"SKImageFilter.CreateBlendMode(");
                sb.AppendLine($"{indent}    {blendModeImageFilter.Mode.ToSKBlendMode()},");
                sb.AppendLine($"{indent}    {counter.ImageFilterVarName}{counterImageFilterBackground},");
                sb.AppendLine($"{indent}    {counter.ImageFilterVarName}{counterImageFilterForeground},");
                sb.AppendLine($"{indent}    {blendModeImageFilter.Clip?.ToCropRect() ?? "null"});");
                return;
            }
            case BlurImageFilter blurImageFilter:
            {
                var counterImageFilterInput = ++counter.ImageFilter;
                if (blurImageFilter.Input is null)
                {
                    sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilterInput} = default(SKImageFilter);");
                }
                else
                {
                    blurImageFilter.Input.ToSKImageFilter(counter, sb, indent);
                }

                sb.Append($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = ");
                sb.AppendLine($"SKImageFilter.CreateBlur(");
                sb.AppendLine($"{indent}    {blurImageFilter.SigmaX.ToFloatString()},");
                sb.AppendLine($"{indent}    {blurImageFilter.SigmaY.ToFloatString()},");
                sb.AppendLine($"{indent}    {counter.ImageFilterVarName}{counterImageFilterInput},");
                sb.AppendLine($"{indent}    {blurImageFilter.Clip?.ToCropRect() ?? "null"});");
                return;
            }
            case ColorFilterImageFilter colorFilterImageFilter:
            {
                if (colorFilterImageFilter.ColorFilter is null)
                {
                    sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = default(SKImageFilter);");
                    return;
                }

                var counterColorFilter = ++counter.ColorFilter;
                colorFilterImageFilter.ColorFilter.ToSKColorFilter(counter, sb, indent);

                var counterImageFilterInput = ++counter.ImageFilter;
                if (colorFilterImageFilter.Input is null)
                {
                    sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilterInput} = default(SKImageFilter);");
                }
                else
                {
                    colorFilterImageFilter.Input.ToSKImageFilter(counter, sb, indent);
                }

                sb.Append($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = ");
                sb.AppendLine($"SKImageFilter.CreateColorFilter(");
                sb.AppendLine($"{indent}    {counter.ColorFilterVarName}{counterColorFilter},");
                sb.AppendLine($"{indent}    {counter.ImageFilterVarName}{counterImageFilterInput},");
                sb.AppendLine($"{indent}    {colorFilterImageFilter.Clip?.ToCropRect() ?? "null"});");
                return;
            }
            case DilateImageFilter dilateImageFilter:
            {
                var counterImageFilterInput = ++counter.ImageFilter;
                if (dilateImageFilter.Input is null)
                {
                    sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilterInput} = default(SKImageFilter);");
                }
                else
                {
                    dilateImageFilter.Input.ToSKImageFilter(counter, sb, indent);
                }

                sb.Append($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = ");
                sb.AppendLine($"SKImageFilter.CreateDilate(");
                sb.AppendLine($"{indent}    {dilateImageFilter.RadiusX.ToIntString()},");
                sb.AppendLine($"{indent}    {dilateImageFilter.RadiusY.ToIntString()},");
                sb.AppendLine($"{indent}    {counter.ImageFilterVarName}{counterImageFilterInput},");
                sb.AppendLine($"{indent}    {dilateImageFilter.Clip?.ToCropRect() ?? "null"});");
                return;
            }
            case DisplacementMapEffectImageFilter displacementMapEffectImageFilter:
            {
                if (displacementMapEffectImageFilter.Displacement is null)
                {
                    sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = default(SKImageFilter);");
                    return;
                }

                var counterImageFilterDisplacement = ++counter.ImageFilter;
                displacementMapEffectImageFilter.Displacement.ToSKImageFilter(counter, sb, indent);

                var counterImageFilterInput = ++counter.ImageFilter;
                if (displacementMapEffectImageFilter.Input is null)
                {
                    sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilterInput} = default(SKImageFilter);");
                }
                else
                {
                    displacementMapEffectImageFilter.Input.ToSKImageFilter(counter, sb, indent);
                }

                sb.Append($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = ");
                sb.AppendLine($"SKImageFilter.CreateDisplacementMapEffect(");
                sb.AppendLine($"{indent}    {displacementMapEffectImageFilter.XChannelSelector.ToSKColorChannel()},");
                sb.AppendLine($"{indent}    {displacementMapEffectImageFilter.YChannelSelector.ToSKColorChannel()},");
                sb.AppendLine($"{indent}    {displacementMapEffectImageFilter.Scale.ToFloatString()},");
                sb.AppendLine($"{indent}    {counter.ImageFilterVarName}{counterImageFilterDisplacement},");
                sb.AppendLine($"{indent}    {counter.ImageFilterVarName}{counterImageFilterInput},");
                sb.AppendLine($"{indent}    {displacementMapEffectImageFilter.Clip?.ToCropRect() ?? "null"});");
                return;
            }
            case DistantLitDiffuseImageFilter distantLitDiffuseImageFilter:
            {
                var counterImageFilterInput = ++counter.ImageFilter;
                if (distantLitDiffuseImageFilter.Input is null)
                {
                    sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilterInput} = default(SKImageFilter);");
                }
                else
                {
                    distantLitDiffuseImageFilter.Input.ToSKImageFilter(counter, sb, indent);
                }

                sb.Append($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = ");
                sb.AppendLine($"SKImageFilter.CreateDistantLitDiffuse(");
                sb.AppendLine($"{indent}    {distantLitDiffuseImageFilter.Direction.ToSKPoint3()},");
                sb.AppendLine($"{indent}    {distantLitDiffuseImageFilter.LightColor.ToSKColor()},");
                sb.AppendLine($"{indent}    {distantLitDiffuseImageFilter.SurfaceScale.ToFloatString()},");
                sb.AppendLine($"{indent}    {distantLitDiffuseImageFilter.Kd.ToFloatString()},");
                sb.AppendLine($"{indent}    {counter.ImageFilterVarName}{counterImageFilterInput},");
                sb.AppendLine($"{indent}    {distantLitDiffuseImageFilter.Clip?.ToCropRect() ?? "null"});");
                return;
            }
            case DistantLitSpecularImageFilter distantLitSpecularImageFilter:
            {
                var counterImageFilterInput = ++counter.ImageFilter;
                if (distantLitSpecularImageFilter.Input is null)
                {
                    sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilterInput} = default(SKImageFilter);");
                }
                else
                {
                    distantLitSpecularImageFilter.Input.ToSKImageFilter(counter, sb, indent);
                }

                sb.Append($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = ");
                sb.AppendLine($"SKImageFilter.CreateDistantLitSpecular(");
                sb.AppendLine($"{indent}    {distantLitSpecularImageFilter.Direction.ToSKPoint3()},");
                sb.AppendLine($"{indent}    {distantLitSpecularImageFilter.LightColor.ToSKColor()},");
                sb.AppendLine($"{indent}    {distantLitSpecularImageFilter.SurfaceScale.ToFloatString()},");
                sb.AppendLine($"{indent}    {distantLitSpecularImageFilter.Ks.ToFloatString()},");
                sb.AppendLine($"{indent}    {distantLitSpecularImageFilter.Shininess.ToFloatString()},");
                sb.AppendLine($"{indent}    {counter.ImageFilterVarName}{counterImageFilterInput},");
                sb.AppendLine($"{indent}    {distantLitSpecularImageFilter.Clip?.ToCropRect() ?? "null"});");
                return;
            }
            case ErodeImageFilter erodeImageFilter:
            {
                var counterImageFilterInput = ++counter.ImageFilter;
                if (erodeImageFilter.Input is null)
                {
                    sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilterInput} = default(SKImageFilter);");
                }
                else
                {
                    erodeImageFilter.Input.ToSKImageFilter(counter, sb, indent);
                }

                sb.Append($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = ");
                sb.AppendLine($"SKImageFilter.CreateErode(");
                sb.AppendLine($"{indent}    {erodeImageFilter.RadiusX.ToIntString()},");
                sb.AppendLine($"{indent}    {erodeImageFilter.RadiusY.ToIntString()},");
                sb.AppendLine($"{indent}    {counter.ImageFilterVarName}{counterImageFilterInput},");
                sb.AppendLine($"{indent}    {erodeImageFilter.Clip?.ToCropRect() ?? "null"});");
                return;
            }
            case ImageImageFilter imageImageFilter:
            {
                if (imageImageFilter.Image is null)
                {
                    sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = default(SKImageFilter);");
                    return;
                }

                var counterImage = ++counter.Image;
                imageImageFilter.Image.ToSKImage(counter, sb, indent);

                sb.Append($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = ");
                sb.AppendLine($"SKImageFilter.CreateImage(");
                sb.AppendLine($"{indent}    {counter.ImageVarName}{counterImage},");
                sb.AppendLine($"{indent}    {imageImageFilter.Src.ToSKRect()},");
                sb.AppendLine($"{indent}    {imageImageFilter.Dst.ToSKRect()},");
                sb.AppendLine($"{indent}    SKFilterQuality.High);");
                return;
            }
            case MatrixConvolutionImageFilter matrixConvolutionImageFilter:
            {
                if (matrixConvolutionImageFilter.Kernel is null)
                {
                    sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = default(SKImageFilter);");
                    return;
                }

                var counterImageFilterInput = ++counter.ImageFilter;
                if (matrixConvolutionImageFilter.Input is null)
                {
                    sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilterInput} = default(SKImageFilter);");
                }
                else
                {
                    matrixConvolutionImageFilter.Input.ToSKImageFilter(counter, sb, indent);
                }

                sb.Append($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = ");
                sb.AppendLine($"SKImageFilter.CreateMatrixConvolution(");
                sb.AppendLine($"{indent}    {matrixConvolutionImageFilter.KernelSize.ToSKSizeI()},");
                sb.AppendLine($"{indent}    {matrixConvolutionImageFilter.Kernel.ToFloatArray()},");
                sb.AppendLine($"{indent}    {matrixConvolutionImageFilter.Gain.ToFloatString()},");
                sb.AppendLine($"{indent}    {matrixConvolutionImageFilter.Bias.ToFloatString()},");
                sb.AppendLine($"{indent}    {matrixConvolutionImageFilter.KernelOffset.ToSKPointI()},");
                sb.AppendLine($"{indent}    {matrixConvolutionImageFilter.TileMode.ToSKShaderTileMode()},");
                sb.AppendLine($"{indent}    {matrixConvolutionImageFilter.ConvolveAlpha.ToBoolString()},");
                sb.AppendLine($"{indent}    {counter.ImageFilterVarName}{counterImageFilterInput},");
                sb.AppendLine($"{indent}    {matrixConvolutionImageFilter.Clip?.ToCropRect() ?? "null"});");
                return;
            }
            case MergeImageFilter mergeImageFilter:
            {
                if (mergeImageFilter.Filters is null)
                {
                    sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = default(SKImageFilter);");
                    return;
                }

                var imageFilters = mergeImageFilter.Filters;

                sb.AppendLine($"{indent}var {counter.ImageFilterVarName}s{counterImageFilter} = new SKImageFilter[{imageFilters.Length}];");

                for (int i = 0; i < imageFilters.Length; i++)
                {
                    var imageFilterItem = imageFilters[i];
                    var counterImageFilterItem = ++counter.ImageFilter;
                    if (imageFilterItem is null)
                    {
                        sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilterItem} = default(SKImageFilter);");
                    }
                    else
                    {
                        imageFilterItem.ToSKImageFilter(counter, sb, indent);
                    }
                    sb.AppendLine($"{indent}{counter.ImageFilterVarName}s{counterImageFilter}[{i}] = {counter.ImageFilterVarName}{counterImageFilterItem};");
                }

                sb.Append($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = ");
                sb.AppendLine($"SKImageFilter.CreateMerge(");
                sb.AppendLine($"{indent}    {counter.ImageFilterVarName}s{counterImageFilter},");
                sb.AppendLine($"{indent}    {mergeImageFilter.Clip?.ToCropRect() ?? "null"});");
                return;
            }
            case OffsetImageFilter offsetImageFilter:
            {
                var counterImageFilterInput = ++counter.ImageFilter;
                if (offsetImageFilter.Input is null)
                {
                    sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilterInput} = default(SKImageFilter);");
                }
                else
                {
                    offsetImageFilter.Input.ToSKImageFilter(counter, sb, indent);
                }

                sb.Append($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = ");
                sb.AppendLine($"SKImageFilter.CreateOffset(");
                sb.AppendLine($"{indent}    {offsetImageFilter.Dx.ToFloatString()},");
                sb.AppendLine($"{indent}    {offsetImageFilter.Dy.ToFloatString()},");
                sb.AppendLine($"{indent}    {counter.ImageFilterVarName}{counterImageFilterInput},");
                sb.AppendLine($"{indent}    {offsetImageFilter.Clip?.ToCropRect() ?? "null"});");
                return;
            }
            case PaintImageFilter paintImageFilter:
            {
                if (paintImageFilter.Paint is null)
                {
                    sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = default(SKImageFilter);");
                    return;
                }

                var counterPaint = ++counter.Paint;
                paintImageFilter.Paint.ToSKPaint(counter, sb, indent);

                sb.Append($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = ");
                sb.AppendLine($"SKImageFilter.CreatePaint(");
                sb.AppendLine($"{indent}    {counter.PaintVarName}{counterPaint},");
                sb.AppendLine($"{indent}    {paintImageFilter.Clip?.ToCropRect() ?? "null"});");

                // NOTE: Do not dispose created SKTypeface by font manager.
#if USE_DISPOSE_TYPEFACE
                        if (paintImageFilter.Paint.Typeface is { })
                        {
                            sb.AppendLine($"{indent}if ({counter.PaintVarName}{counterPaint}.Typeface != SKTypeface.Default)");
                            sb.AppendLine($"{indent}{{");
                            sb.AppendLine($"{indent}    {counter.PaintVarName}{counterPaint}.Typeface?.Dispose();");
                            sb.AppendLine($"{indent}}}");
                        } 
#endif
                if (paintImageFilter.Paint.Shader is { })
                {
                    sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.Shader?.Dispose();");
                }
                if (paintImageFilter.Paint.ColorFilter is { })
                {
                    sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.ColorFilter.Dispose()");
                }
                if (paintImageFilter.Paint.ImageFilter is { })
                {
                    sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.ImageFilter?.Dispose();");
                }
                if (paintImageFilter.Paint.PathEffect is { })
                {
                    sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.PathEffect?.Dispose();");
                }

                sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.Dispose();");
                return;
            }
            case PictureImageFilter pictureImageFilter:
            {
                if (pictureImageFilter.Picture is null)
                {
                    sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = default(SKImageFilter);");
                    return;
                }

                var counterPicture = ++counter.Picture;
                pictureImageFilter.Picture.ToSKPicture(counter, sb, indent);

                sb.Append($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = ");
                sb.AppendLine($"SKImageFilter.CreatePicture(");
                sb.AppendLine($"{indent}    {counter.PictureVarName}{counterPicture},");
                sb.AppendLine($"{indent}    {pictureImageFilter.Picture.CullRect.ToSKRect()});");
                return;
            }
            case PointLitDiffuseImageFilter pointLitDiffuseImageFilter:
            {
                var counterImageFilterInput = ++counter.ImageFilter;
                if (pointLitDiffuseImageFilter.Input is null)
                {
                    sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilterInput} = default(SKImageFilter);");
                }
                else
                {
                    pointLitDiffuseImageFilter.Input.ToSKImageFilter(counter, sb, indent);
                }

                sb.Append($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = ");
                sb.AppendLine($"SKImageFilter.CreatePointLitDiffuse(");
                sb.AppendLine($"{indent}    {pointLitDiffuseImageFilter.Location.ToSKPoint3()},");
                sb.AppendLine($"{indent}    {pointLitDiffuseImageFilter.LightColor.ToSKColor()},");
                sb.AppendLine($"{indent}    {pointLitDiffuseImageFilter.SurfaceScale.ToFloatString()},");
                sb.AppendLine($"{indent}    {pointLitDiffuseImageFilter.Kd.ToFloatString()},");
                sb.AppendLine($"{indent}    {counter.ImageFilterVarName}{counterImageFilterInput},");
                sb.AppendLine($"{indent}    {pointLitDiffuseImageFilter.Clip?.ToCropRect() ?? "null"});");
                return;
            }
            case PointLitSpecularImageFilter pointLitSpecularImageFilter:
            {
                var counterImageFilterInput = ++counter.ImageFilter;
                if (pointLitSpecularImageFilter.Input is null)
                {
                    sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilterInput} = default(SKImageFilter);");
                }
                else
                {
                    pointLitSpecularImageFilter.Input.ToSKImageFilter(counter, sb, indent);
                }

                sb.Append($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = ");
                sb.AppendLine($"SKImageFilter.CreatePointLitSpecular(");
                sb.AppendLine($"{indent}    {pointLitSpecularImageFilter.Location.ToSKPoint3()},");
                sb.AppendLine($"{indent}    {pointLitSpecularImageFilter.LightColor.ToSKColor()},");
                sb.AppendLine($"{indent}    {pointLitSpecularImageFilter.SurfaceScale.ToFloatString()},");
                sb.AppendLine($"{indent}    {pointLitSpecularImageFilter.Ks.ToFloatString()},");
                sb.AppendLine($"{indent}    {pointLitSpecularImageFilter.Shininess.ToFloatString()},");
                sb.AppendLine($"{indent}    {counter.ImageFilterVarName}{counterImageFilterInput},");
                sb.AppendLine($"{indent}    {pointLitSpecularImageFilter.Clip?.ToCropRect() ?? "null"});");
                return;
            }
            case SpotLitDiffuseImageFilter spotLitDiffuseImageFilter:
            {
                var counterImageFilterInput = ++counter.ImageFilter;
                if (spotLitDiffuseImageFilter.Input is null)
                {
                    sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilterInput} = default(SKImageFilter);");
                }
                else
                {
                    spotLitDiffuseImageFilter.Input.ToSKImageFilter(counter, sb, indent);
                }

                sb.Append($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = ");
                sb.AppendLine($"SKImageFilter.CreateSpotLitDiffuse(");
                sb.AppendLine($"{indent}    {spotLitDiffuseImageFilter.Location.ToSKPoint3()},");
                sb.AppendLine($"{indent}    {spotLitDiffuseImageFilter.Target.ToSKPoint3()},");
                sb.AppendLine($"{indent}    {spotLitDiffuseImageFilter.SpecularExponent.ToFloatString()},");
                sb.AppendLine($"{indent}    {spotLitDiffuseImageFilter.CutoffAngle.ToFloatString()},");
                sb.AppendLine($"{indent}    {spotLitDiffuseImageFilter.LightColor.ToSKColor()},");
                sb.AppendLine($"{indent}    {spotLitDiffuseImageFilter.SurfaceScale.ToFloatString()},");
                sb.AppendLine($"{indent}    {spotLitDiffuseImageFilter.Kd.ToFloatString()},");
                sb.AppendLine($"{indent}    {counter.ImageFilterVarName}{counterImageFilterInput},");
                sb.AppendLine($"{indent}    {spotLitDiffuseImageFilter.Clip?.ToCropRect() ?? "null"});");
                return;
            }
            case SpotLitSpecularImageFilter spotLitSpecularImageFilter:
            {
                var counterImageFilterInput = ++counter.ImageFilter;
                if (spotLitSpecularImageFilter.Input is null)
                {
                    sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilterInput} = default(SKImageFilter);");
                }
                else
                {
                    spotLitSpecularImageFilter.Input.ToSKImageFilter(counter, sb, indent);
                }

                sb.Append($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = ");
                sb.AppendLine($"SKImageFilter.CreateSpotLitSpecular(");
                sb.AppendLine($"{indent}    {spotLitSpecularImageFilter.Location.ToSKPoint3()},");
                sb.AppendLine($"{indent}    {spotLitSpecularImageFilter.Target.ToSKPoint3()},");
                sb.AppendLine($"{indent}    {spotLitSpecularImageFilter.SpecularExponent.ToFloatString()},");
                sb.AppendLine($"{indent}    {spotLitSpecularImageFilter.CutoffAngle.ToFloatString()},");
                sb.AppendLine($"{indent}    {spotLitSpecularImageFilter.LightColor.ToSKColor()},");
                sb.AppendLine($"{indent}    {spotLitSpecularImageFilter.SurfaceScale.ToFloatString()},");
                sb.AppendLine($"{indent}    {spotLitSpecularImageFilter.Ks.ToFloatString()},");
                sb.AppendLine($"{indent}    {spotLitSpecularImageFilter.SpecularExponent.ToFloatString()},");
                sb.AppendLine($"{indent}    {counter.ImageFilterVarName}{counterImageFilterInput},");
                sb.AppendLine($"{indent}    {spotLitSpecularImageFilter.Clip?.ToCropRect() ?? "null"});");
                return;
            }
            case TileImageFilter tileImageFilter:
            {
                var counterImageFilterInput = ++counter.ImageFilter;
                if (tileImageFilter.Input is null)
                {
                    sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilterInput} = default(SKImageFilter);");
                }
                else
                {
                    tileImageFilter.Input.ToSKImageFilter(counter, sb, indent);
                }

                sb.Append($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = ");
                sb.AppendLine($"SKImageFilter.CreateTile(");
                sb.AppendLine($"{indent}    {tileImageFilter.Src.ToSKRect()},");
                sb.AppendLine($"{indent}    {tileImageFilter.Dst.ToSKRect()},");
                sb.AppendLine($"{indent}    {counter.ImageFilterVarName}{counterImageFilterInput});");
                return;
            }
            default:
            {
                sb.AppendLine($"{indent}var {counter.ImageFilterVarName}{counterImageFilter} = default(SKImageFilter);");
                return;
            }
        }
    }

    public static void ToSKPathEffect(this SKPathEffect? pathEffect, SkiaCSharpCodeGenCounter counter, StringBuilder sb, string indent)
    {
        var counterPathEffect = counter.PathEffect;

        switch (pathEffect)
        {
            case DashPathEffect dashPathEffect:
            {
                if (dashPathEffect.Intervals is null)
                {
                    sb.AppendLine($"{indent}var {counter.PathEffectVarName}{counterPathEffect} = default(SKPathEffect);");
                    return;
                }

                sb.Append($"{indent}var {counter.PathEffectVarName}{counterPathEffect} = ");
                sb.AppendLine($"SKPathEffect.CreateDash(");
                sb.AppendLine($"{indent}    {dashPathEffect.Intervals.ToFloatArray()},");
                sb.AppendLine($"{indent}    {dashPathEffect.Phase.ToFloatString()});");
                return;
            }
            default:
            {
                sb.AppendLine($"{indent}var {counter.PathEffectVarName}{counterPathEffect} = default(SKPathEffect);");
                return;
            }
        }
    }

    public static string ToSKBlendMode(this SKBlendMode blendMode)
    {
        switch (blendMode)
        {
            default:
            case SKBlendMode.Clear:
                return "SKBlendMode.Clear";
            case SKBlendMode.Src:
                return "SKBlendMode.Src";
            case SKBlendMode.Dst:
                return "SKBlendMode.Dst";
            case SKBlendMode.SrcOver:
                return "SKBlendMode.SrcOver";
            case SKBlendMode.DstOver:
                return "SKBlendMode.DstOver";
            case SKBlendMode.SrcIn:
                return "SKBlendMode.SrcIn";
            case SKBlendMode.DstIn:
                return "SKBlendMode.DstIn";
            case SKBlendMode.SrcOut:
                return "SKBlendMode.SrcOut";
            case SKBlendMode.DstOut:
                return "SKBlendMode.DstOut";
            case SKBlendMode.SrcATop:
                return "SKBlendMode.SrcATop";
            case SKBlendMode.DstATop:
                return "SKBlendMode.DstATop";
            case SKBlendMode.Xor:
                return "SKBlendMode.Xor";
            case SKBlendMode.Plus:
                return "SKBlendMode.Plus";
            case SKBlendMode.Modulate:
                return "SKBlendMode.Modulate";
            case SKBlendMode.Screen:
                return "SKBlendMode.Screen";
            case SKBlendMode.Overlay:
                return "SKBlendMode.Overlay";
            case SKBlendMode.Darken:
                return "SKBlendMode.Darken";
            case SKBlendMode.Lighten:
                return "SKBlendMode.Lighten";
            case SKBlendMode.ColorDodge:
                return "SKBlendMode.ColorDodge";
            case SKBlendMode.ColorBurn:
                return "SKBlendMode.ColorBurn";
            case SKBlendMode.HardLight:
                return "SKBlendMode.HardLight";
            case SKBlendMode.SoftLight:
                return "SKBlendMode.SoftLight";
            case SKBlendMode.Difference:
                return "SKBlendMode.Difference";
            case SKBlendMode.Exclusion:
                return "SKBlendMode.Exclusion";
            case SKBlendMode.Multiply:
                return "SKBlendMode.Multiply";
            case SKBlendMode.Hue:
                return "SKBlendMode.Hue";
            case SKBlendMode.Saturation:
                return "SKBlendMode.Saturation";
            case SKBlendMode.Color:
                return "SKBlendMode.Color";
            case SKBlendMode.Luminosity:
                return "SKBlendMode.Luminosity";
        }
    }

    public static string ToSKFilterQuality(this SKFilterQuality filterQuality)
    {
        switch (filterQuality)
        {
            default:
            case SKFilterQuality.None:
                return "SKFilterQuality.None";
            case SKFilterQuality.Low:
                return "SKFilterQuality.Low";
            case SKFilterQuality.Medium:
                return "SKFilterQuality.Medium";
            case SKFilterQuality.High:
                return "SKFilterQuality.High";
        }
    }

    public static void ToSKPaint(this SKPaint paint, SkiaCSharpCodeGenCounter counter, StringBuilder sb, string indent)
    {
        var counterPaint = counter.Paint;

        sb.AppendLine($"{indent}var {counter.PaintVarName}{counterPaint} = new SKPaint();");

        // SKPaint defaults:
        // Style=Fill
        // IsAntialias=false
        // StrokeWidth=0
        // StrokeCap=Butt
        // StrokeJoin=Miter
        // StrokeMiter=4
        // TextSize=12
        // TextAlign=Left
        // LcdRenderText=false
        // SubpixelText=false
        // TextEncoding=Utf8
        // Color=#ff000000
        // BlendMode=SrcOver
        // FilterQuality=None

        if (paint.Style != SKPaintStyle.Fill)
        {
            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}.Style = {paint.Style.ToSKPaintStyle()};");
        }

        if (paint.IsAntialias != false)
        {
            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}.IsAntialias = {paint.IsAntialias.ToBoolString()};");
        }

        if (paint.StrokeWidth != 0f)
        {
            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}.StrokeWidth = {paint.StrokeWidth.ToFloatString()};");
        }

        if (paint.StrokeCap != SKStrokeCap.Butt)
        {
            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}.StrokeCap = {paint.StrokeCap.ToSKStrokeCap()};");
        }

        if (paint.StrokeJoin != SKStrokeJoin.Miter)
        {
            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}.StrokeJoin = {paint.StrokeJoin.ToSKStrokeJoin()};");
        }

        if (paint.StrokeMiter != 4f)
        {
            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}.StrokeMiter = {paint.StrokeMiter.ToFloatString()};");
        }

        if (paint.TextSize != 12f)
        {
            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}.TextSize = {paint.TextSize.ToFloatString()};");
        }

        if (paint.TextAlign != SKTextAlign.Left)
        {
            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}.TextAlign = {paint.TextAlign.ToSKTextAlign()};");
        }

        if (paint.Typeface is { })
        {
            var counterTypeface = ++counter.Typeface;
            paint.Typeface?.ToSKTypeface(counter, sb, indent);
            sb.AppendLine($"{indent}if ({counter.TypefaceVarName}{counterTypeface} is null)");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    {counter.TypefaceVarName}{counterTypeface} = SKTypeface.Default;");
            sb.AppendLine($"{indent}}}");
            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}.Typeface = {counter.TypefaceVarName}{counterTypeface};");
        }

        if (paint.LcdRenderText != false)
        {
            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}.LcdRenderText = {paint.LcdRenderText.ToBoolString()};");
        }

        if (paint.SubpixelText != false)
        {
            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}.SubpixelText = {paint.SubpixelText.ToBoolString()};");
        }

        if (paint.TextEncoding != SKTextEncoding.Utf8)
        {
            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}.TextEncoding = {paint.TextEncoding.ToSKTextEncoding()};");
        }

        if (paint.Color is { } && paint.Color.Value.Alpha != 255 && paint.Color.Value.Red != 0 && paint.Color.Value.Green != 0 && paint.Color.Value.Blue != 0)
        {
            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}.Color = {(paint.Color is null ? "SKColor.Empty" : ToSKColor(paint.Color.Value))};");
        }

        if (paint.Shader is { })
        {
            var counterShader = ++counter.Shader;
            paint.Shader.ToSKShader(counter, sb, indent);
            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}.Shader = {counter.ShaderVarName}{counterShader};");
        }

        if (paint.ColorFilter is { })
        {
            var counterColorFilter = ++counter.ColorFilter;
            paint.ColorFilter.ToSKColorFilter(counter, sb, indent);
            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}.ColorFilter = {counter.ColorFilterVarName}{counterColorFilter};");
        }

        if (paint.ImageFilter is { })
        {
            var counterImageFilter = ++counter.ImageFilter;
            paint.ImageFilter.ToSKImageFilter(counter, sb, indent);
            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}.ImageFilter = {counter.ImageFilterVarName}{counterImageFilter};");
        }

        if (paint.PathEffect is { })
        {
            var counterPathEffect = ++counter.PathEffect;
            paint.PathEffect.ToSKPathEffect(counter, sb, indent);
            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}.PathEffect = {counter.PathEffectVarName}{counterPathEffect};");
        }

        if (paint.BlendMode != SKBlendMode.SrcOver)
        {
            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}.BlendMode = {paint.BlendMode.ToSKBlendMode()};");
        }

        if (paint.FilterQuality != SKFilterQuality.None)
        {
            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}.FilterQuality = {paint.FilterQuality.ToSKFilterQuality()};");
        }
    }

    public static string ToSKClipOperation(this SKClipOperation clipOperation)
    {
        switch (clipOperation)
        {
            default:
            case SKClipOperation.Difference:
                return "SKClipOperation.Difference";
            case SKClipOperation.Intersect:
                return "SKClipOperation.Intersect";
        }
    }

    public static string ToSKPathFillType(this SKPathFillType pathFillType)
    {
        switch (pathFillType)
        {
            default:
            case SKPathFillType.Winding:
                return "SKPathFillType.Winding";
            case SKPathFillType.EvenOdd:
                return "SKPathFillType.EvenOdd";
        }
    }

    public static string ToSKPathArcSize(this SKPathArcSize pathArcSize)
    {
        switch (pathArcSize)
        {
            default:
            case SKPathArcSize.Small:
                return "SKPathArcSize.Small";
            case SKPathArcSize.Large:
                return "SKPathArcSize.Large";
        }
    }

    public static string ToSKPathDirection(this SKPathDirection pathDirection)
    {
        switch (pathDirection)
        {
            default:
            case SKPathDirection.Clockwise:
                return "SKPathDirection.Clockwise";
            case SKPathDirection.CounterClockwise:
                return "SKPathDirection.CounterClockwise";
        }
    }

    public static string ToSKPathOp(this SKPathOp pathOp)
    {
        switch (pathOp)
        {
            default:
            case SKPathOp.Difference:
                return "SKPathOp.Difference";
            case SKPathOp.Intersect:
                return "SKPathOp.Intersect";
            case SKPathOp.Union:
                return "SKPathOp.Union";
            case SKPathOp.Xor:
                return "SKPathOp.Xor";
            case SKPathOp.ReverseDifference:
                return "SKPathOp.ReverseDifference";
        }
    }

    public static void ToSKPath(this SKPath path, SkiaCSharpCodeGenCounter counter, StringBuilder sb, string indent)
    {
        var counterPath = counter.Path;

        sb.AppendLine($"{indent}var {counter.PathVarName}{counterPath} = new SKPath();");
        if (path.FillType != SKPathFillType.Winding)
        {
            sb.AppendLine($"{indent}{counter.PathVarName}{counterPath}.FillType = {path.FillType.ToSKPathFillType()};");
        }

        if (path.Commands is null)
        {
            return;
        }

        foreach (var pathCommand in path.Commands)
        {
            switch (pathCommand)
            {
                case MoveToPathCommand moveToPathCommand:
                {
                    var x = moveToPathCommand.X;
                    var y = moveToPathCommand.Y;
                    sb.AppendLine($"{indent}{counter.PathVarName}{counterPath}.MoveTo({x.ToFloatString()}, {y.ToFloatString()});");
                }
                    break;
                case LineToPathCommand lineToPathCommand:
                {
                    var x = lineToPathCommand.X;
                    var y = lineToPathCommand.Y;
                    sb.AppendLine($"{indent}{counter.PathVarName}{counterPath}.LineTo({x.ToFloatString()}, {y.ToFloatString()});");
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
                    sb.AppendLine($"{indent}{counter.PathVarName}{counterPath}.ArcTo({rx.ToFloatString()}, {ry.ToFloatString()}, {xAxisRotate.ToFloatString()}, {largeArc}, {sweep}, {x.ToFloatString()}, {y.ToFloatString()});");
                }
                    break;
                case QuadToPathCommand quadToPathCommand:
                {
                    var x0 = quadToPathCommand.X0;
                    var y0 = quadToPathCommand.Y0;
                    var x1 = quadToPathCommand.X1;
                    var y1 = quadToPathCommand.Y1;
                    sb.AppendLine($"{indent}{counter.PathVarName}{counterPath}.QuadTo({x0.ToFloatString()}, {y0.ToFloatString()}, {x1.ToFloatString()}, {y1.ToFloatString()});");
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
                    sb.AppendLine($"{indent}{counter.PathVarName}{counterPath}.CubicTo({x0.ToFloatString()}, {y0.ToFloatString()}, {x1.ToFloatString()}, {y1.ToFloatString()}, {x2.ToFloatString()}, {y2.ToFloatString()});");
                }
                    break;
                case ClosePathCommand _:
                {
                    sb.AppendLine($"{indent}{counter.PathVarName}{counterPath}.Close();");
                }
                    break;
                case AddRectPathCommand addRectPathCommand:
                {
                    var rect = addRectPathCommand.Rect.ToSKRect();
                    sb.AppendLine($"{indent}{counter.PathVarName}{counterPath}.AddRect({rect});");
                }
                    break;
                case AddRoundRectPathCommand addRoundRectPathCommand:
                {
                    var rect = addRoundRectPathCommand.Rect.ToSKRect();
                    var rx = addRoundRectPathCommand.Rx;
                    var ry = addRoundRectPathCommand.Ry;
                    sb.AppendLine($"{indent}{counter.PathVarName}{counterPath}.AddRoundRect({rect}, {rx.ToFloatString()}, {ry.ToFloatString()});");
                }
                    break;
                case AddOvalPathCommand addOvalPathCommand:
                {
                    var rect = addOvalPathCommand.Rect.ToSKRect();
                    sb.AppendLine($"{indent}{counter.PathVarName}{counterPath}.AddOval({rect});");
                }
                    break;
                case AddCirclePathCommand addCirclePathCommand:
                {
                    var x = addCirclePathCommand.X;
                    var y = addCirclePathCommand.Y;
                    var radius = addCirclePathCommand.Radius;
                    sb.AppendLine($"{indent}{counter.PathVarName}{counterPath}.AddCircle({x.ToFloatString()}, {y.ToFloatString()}, {radius.ToFloatString()});");
                }
                    break;
                case AddPolyPathCommand addPolyPathCommand:
                {
                    if (addPolyPathCommand.Points is { })
                    {
                        var points = addPolyPathCommand.Points.ToSKPoints();
                        var close = addPolyPathCommand.Close.ToBoolString();
                        sb.AppendLine($"{indent}{counter.PathVarName}{counterPath}.AddPoly({points}, {close});");
                    }
                }
                    break;
                default:
                    break;
            }
        }
    }

    public static void ToSKPath(this ClipPath clipPath, SkiaCSharpCodeGenCounter counter, StringBuilder sb, string indent, out bool isDefault)
    {
        var counterPathResult = counter.Path;
        var isDefaultPathResult = true;

        if (clipPath.Clips is null)
        {
            isDefault = isDefaultPathResult;
            return;
        }

        foreach (var clip in clipPath.Clips)
        {
            var counterPath = ++counter.Path;

            if (clip.Path is null)
            {
                isDefault = true;
                return;
            }

            clip.Path.ToSKPath(counter, sb, indent);

            if (clip.Clip is { })
            {
                var counterPathClip = ++counter.Path;

                clip.Clip.ToSKPath(counter, sb, indent, out var isDefaultPathClip);

                if (!isDefaultPathClip)
                {
                    sb.AppendLine($"{indent}{counter.PathVarName}{counterPath} = {counter.PathVarName}{counterPath}.Op({counter.PathVarName}{counterPathClip}, SKPathOp.Intersect);");
                }
            }

            if (clip.Transform is { })
            {
                sb.AppendLine($"{indent}{counter.PathVarName}{counterPath}.Transform({clip.Transform.Value.ToSKMatrix()});");
            }

            if (isDefaultPathResult)
            {
                sb.AppendLine($"{indent}var {counter.PathVarName}{counterPathResult} = {counter.PathVarName}{counterPath};");
                isDefaultPathResult = false;
            }
            else
            {
                sb.AppendLine($"{indent}{counter.PathVarName}{counterPathResult} = {counter.PathVarName}{counterPathResult}.Op({counter.PathVarName}{counterPath}, SKPathOp.Union);");
            }
        }

        if (clipPath.Clip is { } && clipPath.Clip.Clips is { })
        {
            var counterPathClip = ++counter.Path;

            clipPath.Clip.ToSKPath(counter, sb, indent, out var isDefaultPathClip);

            if (!isDefaultPathClip)
            {
                sb.AppendLine($"{indent}{counter.PathVarName}{counterPathResult} = {counter.PathVarName}{counterPathResult}.Op({counter.PathVarName}{counterPathClip}, SKPathOp.Intersect);");
            }
        }

        if (!isDefaultPathResult && clipPath.Transform is { })
        {
            sb.AppendLine($"{indent}{counter.PathVarName}{counterPathResult}.Transform({clipPath.Transform.Value.ToSKMatrix()});");
        }

        isDefault = isDefaultPathResult;
    }

    public static void ToSKPicture(this SKPicture? picture, SkiaCSharpCodeGenCounter counter, StringBuilder sb, string indent)
    {
        var counterPicture = counter.Picture;

        if (picture is null)
        {
            sb.AppendLine($"{indent}var {counter.PictureVarName}{counterPicture} = default(SKPicture);");
            return;
        }

        var counterPictureRecorder = ++counter.PictureRecorder;
        var counterCanvas = ++counter.Canvas;

        sb.AppendLine($"{indent}var {counter.PictureRecorderVarName}{counterPictureRecorder} = new SKPictureRecorder();");
        sb.AppendLine($"{indent}var {counter.CanvasVarName}{counterCanvas} = {counter.PictureRecorderVarName}{counterPictureRecorder}.BeginRecording({picture.CullRect.ToSKRect()});");

        if (picture.Commands is null)
        {
            sb.AppendLine($"{indent}var {counter.PictureVarName}{counterPicture} = {counter.PictureRecorderVarName}{counterPictureRecorder}.EndRecording();");
            sb.AppendLine($"{indent}{counter.PictureRecorderVarName}{counterPictureRecorder}?.Dispose();");
            sb.AppendLine($"{indent}{counter.CanvasVarName}{counterCanvas}?.Dispose();");
            return;
        }

        foreach (var canvasCommand in picture.Commands)
        {
            switch (canvasCommand)
            {
                case ClipPathCanvasCommand clipPathCanvasCommand:
                {
                    if (clipPathCanvasCommand.ClipPath is { })
                    {
                        var counterPath = ++counter.Path;
                        clipPathCanvasCommand.ClipPath.ToSKPath(counter, sb, indent, out var isDefault);
                        if (!isDefault)
                        {
                            var operation = clipPathCanvasCommand.Operation.ToSKClipOperation();
                            var antialias = clipPathCanvasCommand.Antialias.ToBoolString();
                            sb.AppendLine(
                                $"{indent}{counter.CanvasVarName}{counterCanvas}.ClipPath({counter.PathVarName}{counterPath}, {operation}, {antialias});");
                        }
                    }
                    break;
                }
                case ClipRectCanvasCommand clipRectCanvasCommand:
                {
                    var rect = clipRectCanvasCommand.Rect.ToSKRect();
                    var operation = clipRectCanvasCommand.Operation.ToSKClipOperation();
                    var antialias = clipRectCanvasCommand.Antialias.ToBoolString();
                    sb.AppendLine($"{indent}{counter.CanvasVarName}{counterCanvas}.ClipRect({rect}, {operation}, {antialias});");
                    break;
                }
                case SaveCanvasCommand _:
                {
                    sb.AppendLine($"{indent}{counter.CanvasVarName}{counterCanvas}.Save();");
                    break;
                }
                case RestoreCanvasCommand _:
                {
                    sb.AppendLine($"{indent}{counter.CanvasVarName}{counterCanvas}.Restore();");
                    break;
                }
                case SetMatrixCanvasCommand setMatrixCanvasCommand:
                {
                    sb.AppendLine($"{indent}{counter.CanvasVarName}{counterCanvas}.SetMatrix({setMatrixCanvasCommand.Matrix.ToSKMatrix()});");
                    break;
                }
                case SaveLayerCanvasCommand saveLayerCanvasCommand:
                {
                    if (saveLayerCanvasCommand.Paint is { })
                    {
                        var counterPaint = ++counter.Paint;
                        saveLayerCanvasCommand.Paint.ToSKPaint(counter, sb, indent);
                        sb.AppendLine($"{indent}{counter.CanvasVarName}{counterCanvas}.SaveLayer({counter.PaintVarName}{counterPaint});");

                        // NOTE: Do not dispose created SKTypeface by font manager.
#if USE_DISPOSE_TYPEFACE
                        if (saveLayerCanvasCommand.Paint.Typeface is { })
                        {
                            sb.AppendLine($"{indent}if ({counter.PaintVarName}{counterPaint}.Typeface != SKTypeface.Default)");
                            sb.AppendLine($"{indent}{{");
                            sb.AppendLine($"{indent}    {counter.PaintVarName}{counterPaint}.Typeface?.Dispose();");
                            sb.AppendLine($"{indent}}}");
                        } 
#endif
                        if (saveLayerCanvasCommand.Paint.Shader is { })
                        {
                            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.Shader?.Dispose();");
                        }
                        if (saveLayerCanvasCommand.Paint.ColorFilter is { })
                        {
                            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.ColorFilter?.Dispose();");
                        }
                        if (saveLayerCanvasCommand.Paint.ImageFilter is { })
                        {
                            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.ImageFilter?.Dispose();");
                        }
                        if (saveLayerCanvasCommand.Paint.PathEffect is { })
                        {
                            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.PathEffect?.Dispose();");
                        }

                        sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.Dispose();");
                    }
                    else
                    {
                        sb.AppendLine($"{indent}{counter.CanvasVarName}{counterCanvas}.SaveLayer();");
                    }
                    break;
                }
                case DrawImageCanvasCommand drawImageCanvasCommand:
                {
                    if (drawImageCanvasCommand.Image is { })
                    {
                        var counterImage = ++counter.Image;
                        drawImageCanvasCommand.Image.ToSKImage(counter, sb, indent);
                        var source = drawImageCanvasCommand.Source.ToSKRect();
                        var dest = drawImageCanvasCommand.Dest.ToSKRect();
                        var counterPaint = ++counter.Paint;
                        drawImageCanvasCommand.Paint?.ToSKPaint(counter, sb, indent);
                        sb.AppendLine($"{indent}{counter.CanvasVarName}{counterCanvas}.DrawImage({counter.ImageVarName}{counterImage}, {source}, {dest}, {counter.PaintVarName}{counterPaint});");
                        sb.AppendLine($"{indent}{counter.ImageVarName}{counterImage}?.Dispose();");

                        // NOTE: Do not dispose created SKTypeface by font manager.
#if USE_DISPOSE_TYPEFACE
                        if (drawImageCanvasCommand.Paint?.Typeface is { })
                        {
                            sb.AppendLine($"{indent}if ({counter.PaintVarName}{counterPaint}.Typeface != SKTypeface.Default)");
                            sb.AppendLine($"{indent}{{");
                            sb.AppendLine($"{indent}    {counter.PaintVarName}{counterPaint}.Typeface?.Dispose();");
                            sb.AppendLine($"{indent}}}");
                        } 
#endif
                        if (drawImageCanvasCommand.Paint?.Shader is { })
                        {
                            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.Shader?.Dispose();");
                        }
                        if (drawImageCanvasCommand.Paint?.ColorFilter is { })
                        {
                            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.ColorFilter?.Dispose();");
                        }
                        if (drawImageCanvasCommand.Paint?.ImageFilter is { })
                        {
                            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.ImageFilter?.Dispose();");
                        }
                        if (drawImageCanvasCommand.Paint?.PathEffect is { })
                        {
                            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.PathEffect?.Dispose();");
                        }

                        sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.Dispose();");
                    }
                    break;
                }
                case DrawPathCanvasCommand drawPathCanvasCommand:
                {
                    if (drawPathCanvasCommand.Path is { } && drawPathCanvasCommand.Paint is { })
                    {
                        var counterPath = ++counter.Path;
                        drawPathCanvasCommand.Path.ToSKPath(counter, sb, indent);
                        var counterPaint = ++counter.Paint;
                        drawPathCanvasCommand.Paint.ToSKPaint(counter, sb, indent);
                        sb.AppendLine($"{indent}{counter.CanvasVarName}{counterCanvas}.DrawPath({counter.PathVarName}{counterPath}, {counter.PaintVarName}{counterPaint});");

                        // NOTE: Do not dispose created SKTypeface by font manager.
#if USE_DISPOSE_TYPEFACE
                        if (drawPathCanvasCommand.Paint.Typeface is { })
                        {
                            sb.AppendLine($"{indent}if ({counter.PaintVarName}{counterPaint}.Typeface != SKTypeface.Default)");
                            sb.AppendLine($"{indent}{{");
                            sb.AppendLine($"{indent}    {counter.PaintVarName}{counterPaint}.Typeface?.Dispose();");
                            sb.AppendLine($"{indent}}}");
                        } 
#endif
                        if (drawPathCanvasCommand.Paint.Shader is { })
                        {
                            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.Shader?.Dispose();");
                        }
                        if (drawPathCanvasCommand.Paint.ColorFilter is { })
                        {
                            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.ColorFilter?.Dispose();");
                        }
                        if (drawPathCanvasCommand.Paint.ImageFilter is { })
                        {
                            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.ImageFilter?.Dispose();");
                        }
                        if (drawPathCanvasCommand.Paint.PathEffect is { })
                        {
                            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.PathEffect?.Dispose();");
                        }

                        sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.Dispose();");
                        sb.AppendLine($"{indent}{counter.PathVarName}{counterPath}?.Dispose();");
                    }
                    break;
                }
                case DrawTextBlobCanvasCommand drawPositionedTextCanvasCommand:
                {
                    if (drawPositionedTextCanvasCommand.TextBlob is { } && drawPositionedTextCanvasCommand.TextBlob.Points is { } && drawPositionedTextCanvasCommand.Paint is { })
                    {
                        var text = EspaceString(drawPositionedTextCanvasCommand.TextBlob.Text);
                        var points = drawPositionedTextCanvasCommand.TextBlob.Points.ToSKPoints();
                        var counterPaint = ++counter.Paint;
                        drawPositionedTextCanvasCommand.Paint.ToSKPaint(counter, sb, indent);
                        var counterFont = ++counter.Font;
                        sb.AppendLine($"{indent}var {counter.FontVarName}{counterFont} = {counter.PaintVarName}{counterPaint}.ToFont();");
                        var counterTextBlob = ++counter.TextBlob;
                        sb.AppendLine($"{indent}var {counter.TextBlobVarName}{counterTextBlob} = SKTextBlob.CreatePositioned(\"{text}\", {counter.FontVarName}{counterFont}, {points});");
                        var x = drawPositionedTextCanvasCommand.X;
                        var y = drawPositionedTextCanvasCommand.Y;
                        sb.AppendLine($"{indent}{counter.CanvasVarName}{counterCanvas}.DrawText({counter.TextBlobVarName}{counterTextBlob}, {x.ToFloatString()}, {y.ToFloatString()}, {counter.PaintVarName}{counterPaint});");

                        // NOTE: Do not dispose created SKTypeface by font manager.
#if USE_DISPOSE_TYPEFACE
                        if (drawPositionedTextCanvasCommand.Paint.Typeface is { })
                        {
                            sb.AppendLine($"{indent}if ({counter.PaintVarName}{counterPaint}.Typeface != SKTypeface.Default)");
                            sb.AppendLine($"{indent}{{");
                            sb.AppendLine($"{indent}    {counter.PaintVarName}{counterPaint}.Typeface?.Dispose();");
                            sb.AppendLine($"{indent}}}");
                        } 
#endif
                        if (drawPositionedTextCanvasCommand.Paint.Shader is { })
                        {
                            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.Shader?.Dispose();");
                        }
                        if (drawPositionedTextCanvasCommand.Paint.ColorFilter is { })
                        {
                            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.ColorFilter?.Dispose();");
                        }
                        if (drawPositionedTextCanvasCommand.Paint.ImageFilter is { })
                        {
                            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.ImageFilter?.Dispose();");
                        }
                        if (drawPositionedTextCanvasCommand.Paint.PathEffect is { })
                        {
                            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.PathEffect?.Dispose();");
                        }

                        sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.Dispose();");
                    }
                    break;
                }
                case DrawTextCanvasCommand drawTextCanvasCommand:
                {
                    if (drawTextCanvasCommand.Paint is { })
                    {
                        var text = EspaceString(drawTextCanvasCommand.Text);
                        var x = drawTextCanvasCommand.X;
                        var y = drawTextCanvasCommand.Y;
                        var counterPaint = ++counter.Paint;
                        drawTextCanvasCommand.Paint.ToSKPaint(counter, sb, indent);
                        sb.AppendLine($"{indent}{counter.CanvasVarName}{counterCanvas}.DrawText(\"{text}\", {x.ToFloatString()}, {y.ToFloatString()}, {counter.PaintVarName}{counterPaint});");

                        // NOTE: Do not dispose created SKTypeface by font manager.
#if USE_DISPOSE_TYPEFACE
                        if (drawTextCanvasCommand.Paint.Typeface is { })
                        {
                            sb.AppendLine($"{indent}if ({counter.PaintVarName}{counterPaint}.Typeface != SKTypeface.Default)");
                            sb.AppendLine($"{indent}{{");
                            sb.AppendLine($"{indent}    {counter.PaintVarName}{counterPaint}.Typeface?.Dispose();");
                            sb.AppendLine($"{indent}}}");
                        } 
#endif
                        if (drawTextCanvasCommand.Paint.Shader is { })
                        {
                            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.Shader?.Dispose();");
                        }
                        if (drawTextCanvasCommand.Paint.ColorFilter is { })
                        {
                            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.ColorFilter?.Dispose();");
                        }
                        if (drawTextCanvasCommand.Paint.ImageFilter is { })
                        {
                            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.ImageFilter?.Dispose();");
                        }
                        if (drawTextCanvasCommand.Paint.PathEffect is { })
                        {
                            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.PathEffect?.Dispose();");
                        }

                        sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.Dispose();");
                    }
                    break;
                }
                case DrawTextOnPathCanvasCommand drawTextOnPathCanvasCommand:
                {
                    if (drawTextOnPathCanvasCommand.Path is { } && drawTextOnPathCanvasCommand.Paint is { })
                    {
                        var text = EspaceString(drawTextOnPathCanvasCommand.Text);
                        var counterPath = ++counter.Path;
                        drawTextOnPathCanvasCommand.Path.ToSKPath(counter, sb, indent);
                        var hOffset = drawTextOnPathCanvasCommand.HOffset;
                        var vOffset = drawTextOnPathCanvasCommand.VOffset;
                        var counterPaint = ++counter.Paint;
                        drawTextOnPathCanvasCommand.Paint.ToSKPaint(counter, sb, indent);
                        sb.AppendLine($"{indent}{counter.CanvasVarName}{counterCanvas}.DrawTextOnPath(\"{text}\", {counter.PathVarName}{counterPath}, {hOffset.ToFloatString()}, {vOffset.ToFloatString()}, {counter.PaintVarName}{counterPaint});");

                        // NOTE: Do not dispose created SKTypeface by font manager.
#if USE_DISPOSE_TYPEFACE
                        if (drawTextOnPathCanvasCommand.Paint.Typeface is { })
                        {
                            sb.AppendLine($"{indent}if ({counter.PaintVarName}{counterPaint}.Typeface != SKTypeface.Default)");
                            sb.AppendLine($"{indent}{{");
                            sb.AppendLine($"{indent}    {counter.PaintVarName}{counterPaint}.Typeface?.Dispose();");
                            sb.AppendLine($"{indent}}}");
                        }
#endif
                        if (drawTextOnPathCanvasCommand.Paint.Shader is { })
                        {
                            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.Shader?.Dispose();");
                        }
                        if (drawTextOnPathCanvasCommand.Paint.ColorFilter is { })
                        {
                            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.ColorFilter?.Dispose();");
                        }
                        if (drawTextOnPathCanvasCommand.Paint.ImageFilter is { })
                        {
                            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.ImageFilter?.Dispose();");
                        }
                        if (drawTextOnPathCanvasCommand.Paint.PathEffect is { })
                        {
                            sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.PathEffect?.Dispose();");
                        }

                        sb.AppendLine($"{indent}{counter.PaintVarName}{counterPaint}?.Dispose();");
                        sb.AppendLine($"{indent}{counter.PathVarName}{counterPath}?.Dispose();");
                    }
                    break;
                }
                default:
                {
                    break;
                }
            }
        }

        sb.AppendLine($"{indent}var {counter.PictureVarName}{counterPicture} = {counter.PictureRecorderVarName}{counterPictureRecorder}.EndRecording();");

        sb.AppendLine($"{indent}{counter.PictureRecorderVarName}{counterPictureRecorder}?.Dispose();");
        sb.AppendLine($"{indent}{counter.CanvasVarName}{counterCanvas}?.Dispose();");
    }
}
