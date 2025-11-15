// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using ShimSkiaSharp;
using Svg.Ast;
using Svg.Pathing;

namespace Svg.Model.Ast;

internal static class SvgAstRenderUtilities
{
    private static readonly CultureInfo s_invariant = CultureInfo.InvariantCulture;
    private static readonly char[] s_styleSeparators = { ';' };
    private static readonly char[] s_styleValueSeparators = { ':' };
    private static readonly char[] s_pointsSeparators = { ' ', ',', '\t', '\r', '\n' };
    private static readonly IReadOnlyDictionary<string, string> s_emptyStyleMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public static float Clamp(float value, float min, float max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    public static float Clamp01(float value) => Clamp(value, 0f, 1f);

    public static SKColor ApplyOpacity(SKColor color, float opacity)
    {
        var alpha = (byte)Math.Round(Clamp01(opacity) * 255f);
        return new SKColor(color.Red, color.Green, color.Blue, alpha);
    }

    public static bool TryExtractUrlReference(string? value, out string reference)
    {
        reference = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value!.Trim();
        if (!trimmed.StartsWith("url(", StringComparison.OrdinalIgnoreCase) || !trimmed.EndsWith(")", StringComparison.Ordinal))
        {
            return false;
        }

        var inner = trimmed.Substring(4, trimmed.Length - 5).Trim();
        if (inner.Length == 0)
        {
            return false;
        }

        if (inner[0] == '#' && inner.Length > 1)
        {
            reference = inner.Substring(1);
            return true;
        }

        if (inner.StartsWith("#", StringComparison.Ordinal))
        {
            reference = inner.Substring(1);
            return reference.Length > 0;
        }

        reference = inner;
        return true;
    }

    public static IReadOnlyDictionary<string, string> ParseStyleAttribute(SvgAstElement element)
    {
        if (!element.TryGetAttribute("style", out var styleAttribute) || styleAttribute is null)
        {
            return s_emptyStyleMap;
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var style = styleAttribute.GetValueText();
        if (string.IsNullOrWhiteSpace(style))
        {
            return result;
        }

        foreach (var part in style.Split(s_styleSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            var tokens = part.Split(s_styleValueSeparators, 2, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 2)
            {
                var key = tokens[0].Trim();
                var value = tokens[1].Trim();
                if (key.Length > 0)
                {
                    result[key] = value;
                }
            }
        }

        return result;
    }

    public static bool TryParseNumber(string? text, out float value)
        => TryParseNumberOrPercentage(text, out value, out _);

    public static bool TryParseNumberOrPercentage(string? text, out float value, out bool isPercentage)
    {
        value = 0;
        isPercentage = false;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text!.Trim();
        if (trimmed.EndsWith("%", StringComparison.Ordinal))
        {
            isPercentage = true;
            trimmed = trimmed.Substring(0, trimmed.Length - 1);
        }

        trimmed = StripUnit(trimmed);

        if (!float.TryParse(trimmed, NumberStyles.Float, s_invariant, out value))
        {
            return false;
        }

        if (isPercentage)
        {
            value /= 100f;
        }

        return true;
    }

    private static string StripUnit(string text)
    {
        var end = text.Length;
        while (end > 0 && (char.IsLetter(text[end - 1]) || text[end - 1] == '%'))
        {
            end--;
        }

        if (end <= 0)
        {
            return string.Empty;
        }

        if (end == text.Length)
        {
            return text;
        }

        return text.Substring(0, end);
    }

    public static bool TryParseViewBox(SvgAstElement element, out SvgAstViewBox viewBox)
    {
        if (element.TryGetAttribute("viewBox", out var attribute) && attribute is not null)
        {
            var viewBoxText = attribute.GetValueText();
            var parts = viewBoxText.Split(s_pointsSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 4 &&
                float.TryParse(parts[0], NumberStyles.Float, s_invariant, out var minX) &&
                float.TryParse(parts[1], NumberStyles.Float, s_invariant, out var minY) &&
                float.TryParse(parts[2], NumberStyles.Float, s_invariant, out var width) &&
                float.TryParse(parts[3], NumberStyles.Float, s_invariant, out var height) &&
                width > 0 &&
                height > 0)
            {
                viewBox = new SvgAstViewBox(minX, minY, width, height);
                return true;
            }
        }

        viewBox = default;
        return false;
    }

    public static SKMatrix CreateViewBoxMatrix(SvgAstViewBox viewBox, SKRect viewport, bool preserveAspectRatio)
    {
        if (viewBox.Width <= 0 || viewBox.Height <= 0)
        {
            return SKMatrix.Identity;
        }

        var scaleX = viewport.Width / viewBox.Width;
        var scaleY = viewport.Height / viewBox.Height;
        if (preserveAspectRatio)
        {
            var scale = Math.Min(scaleX, scaleY);
            var offsetX = viewport.Left + (viewport.Width - viewBox.Width * scale) / 2f;
            var offsetY = viewport.Top + (viewport.Height - viewBox.Height * scale) / 2f;
            var translate = SKMatrix.CreateTranslation(-viewBox.MinX, -viewBox.MinY);
            var scaleMatrix = SKMatrix.CreateScale(scale, scale);
            var align = SKMatrix.CreateTranslation(offsetX, offsetY);
            return align * scaleMatrix * translate;
        }
        else
        {
            var translate = SKMatrix.CreateTranslation(-viewBox.MinX, -viewBox.MinY);
            var scaleMatrix = SKMatrix.CreateScale(scaleX, scaleY);
            var align = SKMatrix.CreateTranslation(viewport.Left, viewport.Top);
            return align * scaleMatrix * translate;
        }
    }

    public static SKMatrix ParseTransform(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return SKMatrix.Identity;
        }

        var remaining = value!;
        var current = SKMatrix.Identity;

        while (!string.IsNullOrWhiteSpace(remaining))
        {
            remaining = remaining.TrimStart();
            var openIndex = remaining.IndexOf('(');
            if (openIndex < 0)
            {
                break;
            }

            var name = remaining.Substring(0, openIndex).Trim();
            remaining = remaining.Substring(openIndex + 1);
            var closeIndex = remaining.IndexOf(')');
            if (closeIndex < 0)
            {
                break;
            }

            var argsText = remaining.Substring(0, closeIndex);
            remaining = remaining.Substring(closeIndex + 1);

            var args = ParseFloatList(argsText);
            var matrix = name.ToLowerInvariant() switch
            {
                "translate" => CreateTranslate(args),
                "scale" => CreateScale(args),
                "rotate" => CreateRotate(args),
                "skewx" => CreateSkewX(args),
                "skewy" => CreateSkewY(args),
                "matrix" => CreateMatrix(args),
                _ => SKMatrix.Identity
            };

            current = current.PreConcat(matrix);
        }

        return current;
    }

    private static SKMatrix CreateTranslate(IList<float> args)
    {
        var x = args.Count > 0 ? args[0] : 0f;
        var y = args.Count > 1 ? args[1] : 0f;
        return SKMatrix.CreateTranslation(x, y);
    }

    private static SKMatrix CreateScale(IList<float> args)
    {
        var x = args.Count > 0 ? args[0] : 1f;
        var y = args.Count > 1 ? args[1] : x;
        return SKMatrix.CreateScale(x, y);
    }

    private static SKMatrix CreateRotate(IList<float> args)
    {
        if (args.Count >= 3)
        {
            return SKMatrix.CreateRotationDegrees(args[0], args[1], args[2]);
        }

        var angle = args.Count > 0 ? args[0] : 0f;
        return SKMatrix.CreateRotationDegrees(angle);
    }

    private static SKMatrix CreateSkewX(IList<float> args)
    {
        var angle = args.Count > 0 ? args[0] : 0f;
        var radians = angle * (float)Math.PI / 180f;
        return SKMatrix.CreateSkew((float)Math.Tan(radians), 0f);
    }

    private static SKMatrix CreateSkewY(IList<float> args)
    {
        var angle = args.Count > 0 ? args[0] : 0f;
        var radians = angle * (float)Math.PI / 180f;
        return SKMatrix.CreateSkew(0f, (float)Math.Tan(radians));
    }

    private static SKMatrix CreateMatrix(IList<float> args)
    {
        if (args.Count == 6)
        {
            return new SKMatrix(
                args[0], args[2], args[4],
                args[1], args[3], args[5],
                0, 0, 1);
        }

        return SKMatrix.Identity;
    }

    private static List<float> ParseFloatList(string text)
    {
        var results = new List<float>();
        var tokens = text.Split(new[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            if (float.TryParse(token, NumberStyles.Float, s_invariant, out var value))
            {
                results.Add(value);
            }
        }

        return results;
    }

    public static bool TryParsePoints(string? text, out List<SKPoint> points)
    {
        points = new List<SKPoint>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var pointText = text!;
        var tokens = pointText.Split(s_pointsSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2)
        {
            return false;
        }

        for (var i = 0; i + 1 < tokens.Length; i += 2)
        {
            if (float.TryParse(tokens[i], NumberStyles.Float, s_invariant, out var x) &&
                float.TryParse(tokens[i + 1], NumberStyles.Float, s_invariant, out var y))
            {
                points.Add(new SKPoint(x, y));
            }
        }

        return points.Count > 0;
    }

    public static SKPath? ParsePathData(string? data, bool isEvenOdd)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return null;
        }

        var segments = SvgPathBuilder.Parse(data.AsSpan());
        return ConvertSegmentsToPath(segments, isEvenOdd);
    }

    private static SKPath? ConvertSegmentsToPath(SvgPathSegmentList segments, bool isEvenOdd)
    {
        if (segments.Count == 0)
        {
            return null;
        }

        var skPath = new SKPath
        {
            FillType = isEvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding
        };

        var start = PointF.Empty;
        var prevMove = PointF.Empty;
        var haveFigure = false;

        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var isLast = i == segments.Count - 1;
            switch (segment)
            {
                case SvgMoveToSegment moveTo:
                    {
                        var end = ToAbsolute(moveTo.End, moveTo.IsRelative, start);
                        skPath.Commands?.Add(new MoveToPathCommand(end.X, end.Y));
                        start = end;
                        prevMove = end;
                        haveFigure = true;
                    }
                    break;
                case SvgLineSegment line:
                    {
                        if (!haveFigure)
                        {
                            break;
                        }
                        var end = ToAbsolute(line.End, line.IsRelative, start);
                        skPath.Commands?.Add(new LineToPathCommand(end.X, end.Y));
                        start = end;
                    }
                    break;
                case SvgClosePathSegment _:
                    {
                        skPath.Commands?.Add(new ClosePathCommand());
                        start = prevMove;
                        haveFigure = false;
                    }
                    break;
                case SvgQuadraticCurveSegment quad:
                    {
                        if (!haveFigure)
                        {
                            break;
                        }
                        var control = ToAbsolute(quad.ControlPoint, quad.IsRelative, start);
                        var end = ToAbsolute(quad.End, quad.IsRelative, start);
                        skPath.Commands?.Add(new QuadToPathCommand(control.X, control.Y, end.X, end.Y));
                        start = end;
                    }
                    break;
                case SvgCubicCurveSegment cubic:
                    {
                        if (!haveFigure)
                        {
                            break;
                        }
                        var cp1 = ToAbsolute(cubic.FirstControlPoint, cubic.IsRelative, start);
                        var cp2 = ToAbsolute(cubic.SecondControlPoint, cubic.IsRelative, start);
                        var end = ToAbsolute(cubic.End, cubic.IsRelative, start);
                        skPath.Commands?.Add(new CubicToPathCommand(cp1.X, cp1.Y, cp2.X, cp2.Y, end.X, end.Y));
                        start = end;
                    }
                    break;
                case SvgArcSegment arc:
                    {
                        if (!haveFigure)
                        {
                            break;
                        }

                        var end = ToAbsolute(arc.End, arc.IsRelative, start);
                        var sweep = arc.Sweep == SvgArcSweep.Negative ? SKPathDirection.CounterClockwise : SKPathDirection.Clockwise;
                        var size = arc.Size == SvgArcSize.Large ? SKPathArcSize.Large : SKPathArcSize.Small;
                        skPath.Commands?.Add(new ArcToPathCommand(
                            arc.RadiusX,
                            arc.RadiusY,
                            arc.Angle,
                            size,
                            sweep,
                            end.X,
                            end.Y));
                        start = end;
                    }
                    break;
                default:
                    break;
            }
        }

        return skPath;
    }

    private static PointF ToAbsolute(PointF point, bool isRelative, PointF start)
    {
        var x = float.IsNaN(point.X) ? start.X : point.X;
        var y = float.IsNaN(point.Y) ? start.Y : point.Y;

        if (isRelative)
        {
            x += start.X;
            y += start.Y;
        }

        return new PointF(x, y);
    }

    public static bool TryParseColor(string? text, out SKColor color)
    {
        return SvgAstColorParser.TryParse(text, out color);
    }
}

internal static class SvgAstColorParser
{
    private static readonly Dictionary<string, SKColor> s_namedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = new SKColor(0x00, 0x00, 0x00, 0xFF),
        ["white"] = new SKColor(0xFF, 0xFF, 0xFF, 0xFF),
        ["red"] = new SKColor(0xFF, 0x00, 0x00, 0xFF),
        ["green"] = new SKColor(0x00, 0x80, 0x00, 0xFF),
        ["blue"] = new SKColor(0x00, 0x00, 0xFF, 0xFF),
        ["yellow"] = new SKColor(0xFF, 0xFF, 0x00, 0xFF),
        ["cyan"] = new SKColor(0x00, 0xFF, 0xFF, 0xFF),
        ["magenta"] = new SKColor(0xFF, 0x00, 0xFF, 0xFF),
        ["gray"] = new SKColor(0x80, 0x80, 0x80, 0xFF),
        ["darkgray"] = new SKColor(0xA9, 0xA9, 0xA9, 0xFF),
        ["lightgray"] = new SKColor(0xD3, 0xD3, 0xD3, 0xFF),
        ["orange"] = new SKColor(0xFF, 0xA5, 0x00, 0xFF),
        ["pink"] = new SKColor(0xFF, 0xC0, 0xCB, 0xFF),
        ["purple"] = new SKColor(0x80, 0x00, 0x80, 0xFF),
        ["brown"] = new SKColor(0xA5, 0x2A, 0x2A, 0xFF)
    };

    public static bool TryParse(string? value, out SKColor color)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            color = default;
            return false;
        }

        var trimmed = value!.Trim();

        if (s_namedColors.TryGetValue(trimmed, out color))
        {
            return true;
        }

        if (trimmed.StartsWith("#", StringComparison.Ordinal))
        {
            return TryParseHex(trimmed.Substring(1), out color);
        }

        if (trimmed.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseRgbFunction(trimmed, out color);
        }

        color = default;
        return false;
    }

    private static bool TryParseHex(string hex, out SKColor color)
    {
        string? expandedString = null;
        if (hex.Length == 3 || hex.Length == 4)
        {
            var chars = new char[hex.Length * 2];
            for (var i = 0; i < hex.Length; i++)
            {
                chars[i * 2] = hex[i];
                chars[(i * 2) + 1] = hex[i];
            }

            expandedString = new string(chars);
            hex = expandedString;
        }

        if (hex.Length == 6 && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            var r = (byte)((rgb >> 16) & 0xFF);
            var g = (byte)((rgb >> 8) & 0xFF);
            var b = (byte)(rgb & 0xFF);
            color = new SKColor(r, g, b, 0xFF);
            return true;
        }

        if (hex.Length == 8 && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
        {
            var a = (byte)((argb >> 24) & 0xFF);
            var r = (byte)((argb >> 16) & 0xFF);
            var g = (byte)((argb >> 8) & 0xFF);
            var b = (byte)(argb & 0xFF);
            color = new SKColor(r, g, b, a);
            return true;
        }

        color = default;
        return false;
    }

    private static bool TryParseRgbFunction(string value, out SKColor color)
    {
        var start = value.IndexOf('(');
        var end = value.IndexOf(')');
        if (start < 0 || end < 0 || end <= start)
        {
            color = default;
            return false;
        }

        var args = value.Substring(start + 1, end - start - 1)
            .Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (args.Length < 3)
        {
            color = default;
            return false;
        }

        if (TryParseComponent(args[0], out var r) &&
            TryParseComponent(args[1], out var g) &&
            TryParseComponent(args[2], out var b))
        {
            var a = args.Length > 3 && SvgAstRenderUtilities.TryParseNumber(args[3], out var alpha)
                ? (byte)Math.Round(SvgAstRenderUtilities.Clamp01(alpha) * 255f)
                : (byte)0xFF;
            color = new SKColor(r, g, b, a);
            return true;
        }

        color = default;
        return false;
    }

    private static bool TryParseComponent(string text, out byte component)
    {
        text = text.Trim();
        if (text.EndsWith("%", StringComparison.Ordinal))
        {
            var percentText = text.Substring(0, text.Length - 1);
            if (float.TryParse(percentText, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
            {
                component = (byte)Math.Round(SvgAstRenderUtilities.Clamp01(percent / 100f) * 255f);
                return true;
            }
        }
        else if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            component = (byte)Math.Round(SvgAstRenderUtilities.Clamp(value, 0f, 255f));
            return true;
        }

        component = 0;
        return false;
    }
}

internal readonly struct SvgAstViewBox
{
    public float MinX { get; }

    public float MinY { get; }

    public float Width { get; }

    public float Height { get; }

    public SvgAstViewBox(float minX, float minY, float width, float height)
    {
        MinX = minX;
        MinY = minY;
        Width = width;
        Height = height;
    }
}
