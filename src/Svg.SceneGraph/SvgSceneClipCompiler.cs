using System;
using System.Collections.Generic;
using System.Globalization;
using ShimSkiaSharp;
using Svg;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia;

internal static class SvgSceneClipCompiler
{
    private enum BasicShapeRadiusMode
    {
        Length,
        ClosestSide,
        FarthestSide
    }

    public static ClipPath? CompileClipPath(SvgClipPath svgClipPath, SKRect targetBounds, ISvgAssetLoader assetLoader)
    {
        if (!svgClipPath.PassesConditionalProcessing(DrawAttributes.None))
        {
            return null;
        }

        var clipPath = new ClipPath
        {
            Clip = new ClipPath()
        };

        PopulateClipPath(svgClipPath, targetBounds, assetLoader, new HashSet<Uri>(), clipPath, svgClipPathClipRule: null);
        return HasClipGeometry(clipPath)
            ? clipPath
            : null;
    }

    public static ClipPath? CompileBasicShapeClipPath(SvgElement svgElement, SKRect targetBounds, SKRect viewport)
    {
        if (!TryGetBasicShapeClipPathValue(svgElement, out var clipPathValue))
        {
            return null;
        }

        return TryCreateBasicShapePath(svgElement, clipPathValue, targetBounds, viewport, out var path) && path is not null
            ? new ClipPath
            {
                Clips = new List<PathClip>
                {
                    new()
                    {
                        Path = path,
                        Transform = SKMatrix.CreateIdentity()
                    }
                }
            }
            : null;
    }

    internal static bool HasBasicShapeClipPath(SvgElement svgElement)
    {
        return TryGetBasicShapeClipPathValue(svgElement, out _);
    }

    private static void PopulateClipPath(
        SvgClipPath svgClipPath,
        SKRect targetBounds,
        ISvgAssetLoader assetLoader,
        HashSet<Uri> uris,
        ClipPath clipPath,
        SvgClipRule? svgClipPathClipRule)
    {
        PopulateClipPathReference(svgClipPath, targetBounds, assetLoader, uris, clipPath);

        var clipRule = GetSvgClipRule(svgClipPath) ?? svgClipPathClipRule;
        PopulateClipChildren(svgClipPath.Children, targetBounds, assetLoader, uris, clipPath, clipRule);

        var transform = SKMatrix.CreateIdentity();
        if (svgClipPath.ClipPathUnits == SvgCoordinateUnits.ObjectBoundingBox)
        {
            transform = transform.PostConcat(SKMatrix.CreateScale(targetBounds.Width, targetBounds.Height));
            transform = transform.PostConcat(SKMatrix.CreateTranslation(targetBounds.Left, targetBounds.Top));
        }

        transform = transform.PostConcat(TransformsService.ToMatrix(svgClipPath.Transforms, svgClipPath, targetBounds, targetBounds));
        clipPath.Transform = transform;

        if (clipPath.Clips is { Count: 0 } && !HasClipGeometry(clipPath.Clip))
        {
            clipPath.Clips.Add(new PathClip
            {
                Path = new SKPath(),
                Transform = SKMatrix.CreateIdentity()
            });
        }
    }

    private static void PopulateClipPathReference(
        SvgClipPath svgClipPath,
        SKRect targetBounds,
        ISvgAssetLoader assetLoader,
        HashSet<Uri> uris,
        ClipPath clipPath)
    {
        if (clipPath.Clip is null)
        {
            clipPath.Clip = new ClipPath();
        }

        var referenceUri = GetReferenceUri(svgClipPath, "clip-path");
        if (referenceUri is not null && !uris.Add(referenceUri))
        {
            return;
        }

        var referencedClipPath = referenceUri is null
            ? null
            : SvgService.GetReference<SvgClipPath>(svgClipPath, referenceUri);
        if (referencedClipPath?.Children is null ||
            !referencedClipPath.PassesConditionalProcessing(DrawAttributes.None))
        {
            return;
        }

        PopulateClipPath(referencedClipPath, targetBounds, assetLoader, uris, clipPath.Clip, svgClipPathClipRule: null);

        var transform = SKMatrix.CreateIdentity();
        if (referencedClipPath.ClipPathUnits == SvgCoordinateUnits.ObjectBoundingBox)
        {
            transform = transform.PostConcat(SKMatrix.CreateScale(targetBounds.Width, targetBounds.Height));
            transform = transform.PostConcat(SKMatrix.CreateTranslation(targetBounds.Left, targetBounds.Top));
        }

        transform = transform.PostConcat(TransformsService.ToMatrix(referencedClipPath.Transforms, referencedClipPath, targetBounds, targetBounds));
        clipPath.Clip.Transform = transform;
    }

    private static Uri? GetReferenceUri(SvgElement element, string name)
    {
        if ((!element.TryGetOwnCascadedStyleValue(name, out var value) &&
             !element.TryGetAttribute(name, out value)) ||
            string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalizedValue = value.Trim();
        if (string.Equals(normalizedValue, "none", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedValue, "inherit", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (normalizedValue.StartsWith("url(", StringComparison.OrdinalIgnoreCase) &&
            normalizedValue.EndsWith(")", StringComparison.Ordinal))
        {
            normalizedValue = normalizedValue.Substring(4, normalizedValue.Length - 5).Trim();
        }

        if (normalizedValue.Length >= 2 &&
            ((normalizedValue[0] == '\'' && normalizedValue[normalizedValue.Length - 1] == '\'') ||
             (normalizedValue[0] == '"' && normalizedValue[normalizedValue.Length - 1] == '"')))
        {
            normalizedValue = normalizedValue.Substring(1, normalizedValue.Length - 2);
        }

        return string.IsNullOrWhiteSpace(normalizedValue)
            ? null
            : new Uri(normalizedValue, UriKind.RelativeOrAbsolute);
    }

    private static bool TryGetBasicShapeClipPathValue(SvgElement element, out string value)
    {
        value = string.Empty;
        if (!TryGetRawClipPathValue(element, out var rawValue) ||
            !TryGetTrimmedRange(rawValue, out var start, out var length) ||
            IsCssWideClipPathValue(rawValue, start, length) ||
            IsUrlClipPathValue(rawValue, start, length) ||
            !StartsWithBasicShapeFunction(rawValue, start, length))
        {
            return false;
        }

        value = start == 0 && length == rawValue.Length
            ? rawValue
            : rawValue.Substring(start, length);
        return true;
    }

    private static bool TryGetRawClipPathValue(SvgElement element, out string value)
    {
        if (element.TryGetOwnCascadedStyleValue("clip-path", out value) ||
            element.TryGetAttribute("clip-path", out value))
        {
            return !string.IsNullOrEmpty(value);
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetTrimmedRange(string value, out int start, out int length)
    {
        start = 0;
        var end = value.Length - 1;

        while (start <= end && char.IsWhiteSpace(value[start]))
        {
            start++;
        }

        while (end >= start && char.IsWhiteSpace(value[end]))
        {
            end--;
        }

        length = end - start + 1;
        return length > 0;
    }

    private static bool IsCssWideClipPathValue(string value, int start, int length)
    {
        return EqualsRange(value, start, length, "none") ||
               EqualsRange(value, start, length, "inherit") ||
               EqualsRange(value, start, length, "initial") ||
               EqualsRange(value, start, length, "unset");
    }

    private static bool IsUrlClipPathValue(string value, int start, int length)
    {
        return StartsWithRange(value, start, length, "url(") ||
               value[start] == '#';
    }

    private static bool StartsWithBasicShapeFunction(string value, int start, int length)
    {
        return StartsWithRange(value, start, length, "circle(") ||
               StartsWithRange(value, start, length, "ellipse(") ||
               StartsWithRange(value, start, length, "inset(") ||
               StartsWithRange(value, start, length, "polygon(") ||
               StartsWithRange(value, start, length, "path(");
    }

    private static bool EqualsRange(string value, int start, int length, string expected)
    {
        return length == expected.Length &&
               string.Compare(value, start, expected, 0, expected.Length, StringComparison.OrdinalIgnoreCase) == 0;
    }

    private static bool StartsWithRange(string value, int start, int length, string expected)
    {
        return length >= expected.Length &&
               string.Compare(value, start, expected, 0, expected.Length, StringComparison.OrdinalIgnoreCase) == 0;
    }

    private static bool TryCreateBasicShapePath(
        SvgElement svgElement,
        string clipPathValue,
        SKRect targetBounds,
        SKRect viewport,
        out SKPath? path)
    {
        path = null;
        if (!TrySplitBasicShape(clipPathValue, out var shapeName, out var arguments, out var geometryBox))
        {
            return false;
        }

        var referenceBox = ResolveBasicShapeReferenceBox(svgElement, targetBounds, viewport, geometryBox);
        if (referenceBox.Width <= 0f || referenceBox.Height <= 0f)
        {
            return false;
        }

        return shapeName switch
        {
            "circle" => TryCreateCircleClipPath(svgElement, arguments, referenceBox, out path),
            "ellipse" => TryCreateEllipseClipPath(svgElement, arguments, referenceBox, out path),
            "inset" => TryCreateInsetClipPath(svgElement, arguments, referenceBox, out path),
            "polygon" => TryCreatePolygonClipPath(svgElement, arguments, referenceBox, out path),
            "path" => TryCreatePathClipPath(arguments, out path),
            _ => false
        };
    }

    private static bool TrySplitBasicShape(string value, out string shapeName, out string arguments, out string? geometryBox)
    {
        shapeName = string.Empty;
        arguments = string.Empty;
        geometryBox = null;

        var openParen = value.IndexOf('(');
        if (openParen <= 0)
        {
            return false;
        }

        var closeParen = FindMatchingParen(value, openParen);
        if (closeParen <= openParen)
        {
            return false;
        }

        shapeName = value.Substring(0, openParen).Trim().ToLowerInvariant();
        arguments = value.Substring(openParen + 1, closeParen - openParen - 1).Trim();

        if (TryGetFirstToken(value, closeParen + 1, out var tokenStart, out var tokenLength))
        {
            geometryBox = value.Substring(tokenStart, tokenLength);
        }

        return shapeName.Length > 0;
    }

    private static int FindMatchingParen(string value, int openParen)
    {
        var depth = 0;
        var quote = '\0';
        for (var i = openParen; i < value.Length; i++)
        {
            var c = value[i];
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
                continue;
            }

            if (c is '\'' or '"')
            {
                quote = c;
                continue;
            }

            if (c == '(')
            {
                depth++;
                continue;
            }

            if (c == ')' && --depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static SKRect ResolveBasicShapeReferenceBox(SvgElement svgElement, SKRect targetBounds, SKRect viewport, string? geometryBox)
    {
        if (geometryBox is not null && string.Equals(geometryBox, "view-box", StringComparison.OrdinalIgnoreCase))
        {
            return viewport.Width > 0f && viewport.Height > 0f ? viewport : targetBounds;
        }

        if (geometryBox is not null &&
            string.Equals(geometryBox, "stroke-box", StringComparison.OrdinalIgnoreCase) &&
            svgElement is SvgVisualElement visualElement &&
            SvgScenePaintingService.IsValidStroke(visualElement, targetBounds))
        {
            var strokeWidth = visualElement.StrokeWidth.ToDeviceValue(UnitRenderingType.Other, visualElement, targetBounds);
            var inflation = Math.Max(0f, strokeWidth / 2f);
            return new SKRect(
                targetBounds.Left - inflation,
                targetBounds.Top - inflation,
                targetBounds.Right + inflation,
                targetBounds.Bottom + inflation);
        }

        return targetBounds;
    }

    private static bool TryCreateCircleClipPath(SvgElement owner, string arguments, SKRect referenceBox, out SKPath? path)
    {
        path = null;
        var centerX = referenceBox.Left + (referenceBox.Width / 2f);
        var centerY = referenceBox.Top + (referenceBox.Height / 2f);
        var radius = 0f;
        var radiusMode = BasicShapeRadiusMode.ClosestSide;

        if (!string.IsNullOrWhiteSpace(arguments))
        {
            var atIndex = IndexOfAtKeyword(arguments);
            var radiusText = atIndex >= 0 ? arguments.Substring(0, atIndex).Trim() : arguments.Trim();
            var centerText = atIndex >= 0 ? arguments.Substring(atIndex + 2).Trim() : string.Empty;

            if (radiusText.Length > 0)
            {
                if (string.Equals(radiusText, "closest-side", StringComparison.OrdinalIgnoreCase))
                {
                    radiusMode = BasicShapeRadiusMode.ClosestSide;
                }
                else if (string.Equals(radiusText, "farthest-side", StringComparison.OrdinalIgnoreCase))
                {
                    radiusMode = BasicShapeRadiusMode.FarthestSide;
                }
                else if (!TryResolveLength(radiusText, owner, referenceBox, UnitRenderingType.Other, out radius))
                {
                    return false;
                }
                else
                {
                    radiusMode = BasicShapeRadiusMode.Length;
                }
            }

            if (centerText.Length > 0 && !TryResolvePosition(centerText, owner, referenceBox, out centerX, out centerY))
            {
                return false;
            }
        }

        if (radiusMode != BasicShapeRadiusMode.Length)
        {
            radius = ResolveCircleRadiusKeyword(radiusMode, centerX, centerY, referenceBox);
        }

        if (radius <= 0f)
        {
            return false;
        }

        path = new SKPath { FillType = SKPathFillType.Winding };
        path.AddOval(SKRect.Create(centerX - radius, centerY - radius, radius + radius, radius + radius));
        return true;
    }

    private static bool TryCreateEllipseClipPath(SvgElement owner, string arguments, SKRect referenceBox, out SKPath? path)
    {
        path = null;
        var centerX = referenceBox.Left + (referenceBox.Width / 2f);
        var centerY = referenceBox.Top + (referenceBox.Height / 2f);
        var rx = 0f;
        var ry = 0f;
        var rxMode = BasicShapeRadiusMode.ClosestSide;
        var ryMode = BasicShapeRadiusMode.ClosestSide;

        if (!string.IsNullOrWhiteSpace(arguments))
        {
            var atIndex = IndexOfAtKeyword(arguments);
            var radiusText = atIndex >= 0 ? arguments.Substring(0, atIndex).Trim() : arguments.Trim();
            var centerText = atIndex >= 0 ? arguments.Substring(atIndex + 2).Trim() : string.Empty;
            if (radiusText.Length > 0)
            {
                var radiusTokens = SplitShapeTokens(radiusText);
                if (radiusTokens.Length > 0)
                {
                    if (!TryResolveEllipseRadius(radiusTokens[0], owner, referenceBox, UnitRenderingType.Horizontal, out rx, out rxMode))
                    {
                        return false;
                    }

                    var ryToken = radiusTokens.Length > 1 ? radiusTokens[1] : radiusTokens[0];
                    if (!TryResolveEllipseRadius(ryToken, owner, referenceBox, UnitRenderingType.Vertical, out ry, out ryMode))
                    {
                        return false;
                    }
                }
            }

            if (centerText.Length > 0 && !TryResolvePosition(centerText, owner, referenceBox, out centerX, out centerY))
            {
                return false;
            }
        }

        if (rxMode != BasicShapeRadiusMode.Length)
        {
            rx = ResolveEllipseRadiusKeyword(rxMode, centerX, referenceBox.Left, referenceBox.Right);
        }

        if (ryMode != BasicShapeRadiusMode.Length)
        {
            ry = ResolveEllipseRadiusKeyword(ryMode, centerY, referenceBox.Top, referenceBox.Bottom);
        }

        if (rx <= 0f || ry <= 0f)
        {
            return false;
        }

        path = new SKPath { FillType = SKPathFillType.Winding };
        path.AddOval(SKRect.Create(centerX - rx, centerY - ry, rx + rx, ry + ry));
        return true;
    }

    private static bool TryResolveEllipseRadius(
        string token,
        SvgElement owner,
        SKRect referenceBox,
        UnitRenderingType renderType,
        out float radius,
        out BasicShapeRadiusMode radiusMode)
    {
        radius = 0f;
        radiusMode = BasicShapeRadiusMode.Length;

        if (string.Equals(token, "closest-side", StringComparison.OrdinalIgnoreCase))
        {
            radiusMode = BasicShapeRadiusMode.ClosestSide;
            return true;
        }

        if (string.Equals(token, "farthest-side", StringComparison.OrdinalIgnoreCase))
        {
            radiusMode = BasicShapeRadiusMode.FarthestSide;
            return true;
        }

        return TryResolveLength(token, owner, referenceBox, renderType, out radius);
    }

    private static float ResolveCircleRadiusKeyword(BasicShapeRadiusMode radiusMode, float centerX, float centerY, SKRect referenceBox)
    {
        var left = Math.Abs(centerX - referenceBox.Left);
        var right = Math.Abs(referenceBox.Right - centerX);
        var top = Math.Abs(centerY - referenceBox.Top);
        var bottom = Math.Abs(referenceBox.Bottom - centerY);

        return radiusMode == BasicShapeRadiusMode.FarthestSide
            ? Math.Max(Math.Max(left, right), Math.Max(top, bottom))
            : Math.Min(Math.Min(left, right), Math.Min(top, bottom));
    }

    private static float ResolveEllipseRadiusKeyword(BasicShapeRadiusMode radiusMode, float center, float start, float end)
    {
        var startDistance = Math.Abs(center - start);
        var endDistance = Math.Abs(end - center);

        return radiusMode == BasicShapeRadiusMode.FarthestSide
            ? Math.Max(startDistance, endDistance)
            : Math.Min(startDistance, endDistance);
    }

    private static bool TryCreateInsetClipPath(SvgElement owner, string arguments, SKRect referenceBox, out SKPath? path)
    {
        path = null;
        var roundIndex = arguments.IndexOf("round", StringComparison.OrdinalIgnoreCase);
        var insetText = roundIndex >= 0 ? arguments.Substring(0, roundIndex).Trim() : arguments.Trim();
        var tokens = SplitShapeTokens(insetText);
        if (tokens.Length == 0 || tokens.Length > 4)
        {
            return false;
        }

        if (!TryResolveLength(tokens[0], owner, referenceBox, UnitRenderingType.Vertical, out var top))
        {
            return false;
        }

        var rightToken = tokens.Length > 1 ? tokens[1] : tokens[0];
        var bottomToken = tokens.Length > 2 ? tokens[2] : tokens[0];
        var leftToken = tokens.Length > 3 ? tokens[3] : rightToken;

        if (!TryResolveLength(rightToken, owner, referenceBox, UnitRenderingType.Horizontal, out var right) ||
            !TryResolveLength(bottomToken, owner, referenceBox, UnitRenderingType.Vertical, out var bottom) ||
            !TryResolveLength(leftToken, owner, referenceBox, UnitRenderingType.Horizontal, out var left))
        {
            return false;
        }

        var rect = new SKRect(
            referenceBox.Left + left,
            referenceBox.Top + top,
            referenceBox.Right - right,
            referenceBox.Bottom - bottom);
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            return false;
        }

        path = new SKPath { FillType = SKPathFillType.Winding };
        path.AddRect(rect);
        return true;
    }

    private static bool TryCreatePolygonClipPath(SvgElement owner, string arguments, SKRect referenceBox, out SKPath? path)
    {
        path = null;
        var fillType = SKPathFillType.Winding;
        var values = arguments.Trim();
        if (values.StartsWith("evenodd,", StringComparison.OrdinalIgnoreCase))
        {
            fillType = SKPathFillType.EvenOdd;
            values = values.Substring("evenodd,".Length).Trim();
        }
        else if (values.StartsWith("nonzero,", StringComparison.OrdinalIgnoreCase))
        {
            values = values.Substring("nonzero,".Length).Trim();
        }

        path = new SKPath { FillType = fillType };
        var pointCount = 0;
        var pointStart = 0;
        for (var i = 0; i <= values.Length; i++)
        {
            if (i < values.Length && values[i] != ',')
            {
                continue;
            }

            var pointToken = values.Substring(pointStart, i - pointStart).Trim();
            pointStart = i + 1;
            if (pointToken.Length == 0)
            {
                continue;
            }

            if (!TryResolvePosition(pointToken, owner, referenceBox, out var x, out var y))
            {
                path = null;
                return false;
            }

            if (pointCount == 0)
            {
                path.MoveTo(x, y);
            }
            else
            {
                path.LineTo(x, y);
            }

            pointCount++;
        }

        if (pointCount < 3)
        {
            path = null;
            return false;
        }

        path.Close();
        return true;
    }

    private static bool TryCreatePathClipPath(string arguments, out SKPath? path)
    {
        path = null;
        var pathData = arguments.Trim();
        var fillRule = SvgFillRule.NonZero;
        if (TryConsumePathFillRule(ref pathData, out var parsedFillRule))
        {
            fillRule = parsedFillRule;
        }

        if (pathData.Length >= 2 &&
            ((pathData[0] == '\'' && pathData[pathData.Length - 1] == '\'') ||
             (pathData[0] == '"' && pathData[pathData.Length - 1] == '"')))
        {
            pathData = pathData.Substring(1, pathData.Length - 2);
        }

        if (string.IsNullOrWhiteSpace(pathData))
        {
            return false;
        }

        var segments = SvgPathBuilder.Parse(pathData.AsSpan());
        path = segments.ToPath(fillRule);
        return path is not null && !path.IsEmpty;
    }

    private static bool TryConsumePathFillRule(ref string pathData, out SvgFillRule fillRule)
    {
        fillRule = SvgFillRule.NonZero;

        if (!TryConsumePathFillRule(pathData, "evenodd", SvgFillRule.EvenOdd, out var consumedPathData, out fillRule) &&
            !TryConsumePathFillRule(pathData, "nonzero", SvgFillRule.NonZero, out consumedPathData, out fillRule))
        {
            return false;
        }

        pathData = consumedPathData;
        return true;
    }

    private static bool TryConsumePathFillRule(
        string pathData,
        string keyword,
        SvgFillRule keywordFillRule,
        out string consumedPathData,
        out SvgFillRule fillRule)
    {
        consumedPathData = pathData;
        fillRule = SvgFillRule.NonZero;

        if (!pathData.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var index = keyword.Length;
        while (index < pathData.Length && char.IsWhiteSpace(pathData[index]))
        {
            index++;
        }

        if (index >= pathData.Length || pathData[index] != ',')
        {
            return false;
        }

        fillRule = keywordFillRule;
        consumedPathData = pathData.Substring(index + 1).Trim();
        return true;
    }

    private static int IndexOfAtKeyword(string value)
    {
        var tokens = SplitShapeTokens(value);
        var offset = 0;
        foreach (var token in tokens)
        {
            var index = value.IndexOf(token, offset, StringComparison.Ordinal);
            if (index < 0)
            {
                return -1;
            }

            if (string.Equals(token, "at", StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }

            offset = index + token.Length;
        }

        return -1;
    }

    private static string[] SplitShapeTokens(string value)
    {
        List<string>? tokens = null;
        var tokenStart = -1;
        for (var i = 0; i <= value.Length; i++)
        {
            if (i < value.Length && !char.IsWhiteSpace(value[i]))
            {
                if (tokenStart < 0)
                {
                    tokenStart = i;
                }

                continue;
            }

            if (tokenStart < 0)
            {
                continue;
            }

            tokens ??= new List<string>();
            tokens.Add(value.Substring(tokenStart, i - tokenStart));
            tokenStart = -1;
        }

        return tokens?.ToArray() ?? Array.Empty<string>();
    }

    private static bool TryGetFirstToken(string value, int start, out int tokenStart, out int tokenLength)
    {
        tokenStart = -1;
        tokenLength = 0;

        while (start < value.Length && char.IsWhiteSpace(value[start]))
        {
            start++;
        }

        if (start >= value.Length)
        {
            return false;
        }

        var end = start;
        while (end < value.Length && !char.IsWhiteSpace(value[end]))
        {
            end++;
        }

        tokenStart = start;
        tokenLength = end - start;
        return tokenLength > 0;
    }

    private static bool TryResolvePosition(string value, SvgElement owner, SKRect referenceBox, out float x, out float y)
    {
        var tokens = SplitShapeTokens(value);
        if (tokens.Length != 2)
        {
            x = 0f;
            y = 0f;
            return false;
        }

        if (!TryResolveLength(tokens[0], owner, referenceBox, UnitRenderingType.Horizontal, out var xOffset) ||
            !TryResolveLength(tokens[1], owner, referenceBox, UnitRenderingType.Vertical, out var yOffset))
        {
            x = 0f;
            y = 0f;
            return false;
        }

        x = referenceBox.Left + xOffset;
        y = referenceBox.Top + yOffset;
        return true;
    }

    private static bool TryResolveLength(
        string value,
        SvgElement owner,
        SKRect referenceBox,
        UnitRenderingType renderType,
        out float resolvedValue)
    {
        resolvedValue = 0f;
        SvgUnit unit;
        try
        {
            unit = SvgUnitConverter.Parse(value.AsSpan().Trim());
        }
        catch (FormatException)
        {
            return false;
        }

        resolvedValue = unit.ToDeviceValue(renderType, owner, referenceBox);
        return !float.IsNaN(resolvedValue) && !float.IsInfinity(resolvedValue);
    }

    private static bool HasClipGeometry(ClipPath? clipPath)
    {
        if (clipPath is null)
        {
            return false;
        }

        if (clipPath.Clips is { Count: > 0 })
        {
            return true;
        }

        return HasClipGeometry(clipPath.Clip);
    }

    private static void PopulateClipChildren(
        SvgElementCollection children,
        SKRect targetBounds,
        ISvgAssetLoader assetLoader,
        HashSet<Uri> uris,
        ClipPath clipPath,
        SvgClipRule? svgClipPathClipRule)
    {
        foreach (var child in children)
        {
            if (child is not SvgVisualElement visualChild ||
                !visualChild.PassesConditionalProcessing(DrawAttributes.None) ||
                !MaskingService.CanDraw(visualChild, DrawAttributes.None))
            {
                continue;
            }

            PopulateVisualClip(visualChild, targetBounds, assetLoader, new HashSet<Uri>(uris), clipPath, svgClipPathClipRule);
        }
    }

    private static void PopulateVisualClip(
        SvgVisualElement visualElement,
        SKRect targetBounds,
        ISvgAssetLoader assetLoader,
        HashSet<Uri> uris,
        ClipPath clipPath,
        SvgClipRule? svgClipPathClipRule)
    {
        switch (visualElement)
        {
            case SvgPath svgPath:
                AddVisualPathClip(svgPath, svgPath.PathData?.ToPath(ToFillRule(svgPath, svgClipPathClipRule)), assetLoader, uris, clipPath);
                break;
            case SvgRectangle svgRectangle:
                AddVisualPathClip(svgRectangle, svgRectangle.ToPath(ToFillRule(svgRectangle, svgClipPathClipRule), targetBounds), assetLoader, uris, clipPath);
                break;
            case SvgCircle svgCircle:
                AddVisualPathClip(svgCircle, svgCircle.ToPath(ToFillRule(svgCircle, svgClipPathClipRule), targetBounds), assetLoader, uris, clipPath);
                break;
            case SvgEllipse svgEllipse:
                AddVisualPathClip(svgEllipse, svgEllipse.ToPath(ToFillRule(svgEllipse, svgClipPathClipRule), targetBounds), assetLoader, uris, clipPath);
                break;
            case SvgLine svgLine:
                AddVisualPathClip(svgLine, svgLine.ToPath(ToFillRule(svgLine, svgClipPathClipRule), targetBounds), assetLoader, uris, clipPath);
                break;
            case SvgPolyline svgPolyline:
                AddVisualPathClip(svgPolyline, svgPolyline.Points?.ToPath(ToFillRule(svgPolyline, svgClipPathClipRule), false, targetBounds), assetLoader, uris, clipPath);
                break;
            case SvgPolygon svgPolygon:
                AddVisualPathClip(svgPolygon, svgPolygon.Points?.ToPath(ToFillRule(svgPolygon, svgClipPathClipRule), true, targetBounds), assetLoader, uris, clipPath);
                break;
            case SvgUse svgUse:
                PopulateUseClip(svgUse, targetBounds, assetLoader, uris, clipPath, svgClipPathClipRule);
                break;
            case SvgText svgText:
                AddTextClip(svgText, targetBounds, assetLoader, uris, clipPath);
                break;
        }
    }

    private static void AddVisualPathClip(
        SvgVisualElement visualElement,
        SKPath? path,
        ISvgAssetLoader assetLoader,
        HashSet<Uri> uris,
        ClipPath clipPath)
    {
        if (path is null)
        {
            return;
        }

        var pathClip = new PathClip
        {
            Path = path,
            Transform = TransformsService.ToMatrix(visualElement.Transforms, visualElement, path.Bounds, path.Bounds),
            Clip = new ClipPath
            {
                Clip = new ClipPath()
            }
        };

        clipPath.Clips?.Add(pathClip);
        PopulateNestedClipPath(visualElement, path.Bounds, assetLoader, uris, pathClip.Clip!);
    }

    private static void PopulateUseClip(
        SvgUse svgUse,
        SKRect targetBounds,
        ISvgAssetLoader assetLoader,
        HashSet<Uri> uris,
        ClipPath clipPath,
        SvgClipRule? svgClipPathClipRule)
    {
        if (SvgService.HasRecursiveReference(svgUse, static e => SvgService.GetEffectiveReferenceUri(e, e.ReferencedElement), new HashSet<Uri>()))
        {
            return;
        }

        var referencedVisualElement = SvgService.GetReference<SvgVisualElement>(svgUse, SvgService.GetEffectiveReferenceUri(svgUse, svgUse.ReferencedElement));
        if (referencedVisualElement is null ||
            referencedVisualElement is SvgSymbol ||
            !referencedVisualElement.PassesConditionalProcessing(DrawAttributes.None))
        {
            return;
        }

        WithUseInstanceStyleScope(referencedVisualElement, svgUse, () =>
        {
            if (!referencedVisualElement.PassesConditionalProcessing(DrawAttributes.None) ||
                !MaskingService.CanDraw(referencedVisualElement, DrawAttributes.None))
            {
                return;
            }

            var previousClipCount = clipPath.Clips?.Count ?? 0;
            PopulateVisualClip(referencedVisualElement, targetBounds, assetLoader, uris, clipPath, svgClipPathClipRule);
            if (clipPath.Clips is { Count: > 0 } populatedClips &&
                populatedClips.Count > previousClipCount)
            {
                var useTransform = CreateUseClipTransform(svgUse, targetBounds);
                ApplyUseClipTransform(populatedClips, previousClipCount, useTransform);
            }

            if (clipPath.Clips is { Count: > 0 } clips &&
                clips.Count > previousClipCount &&
                clips[clips.Count - 1].Clip is { } lastClip)
            {
                PopulateNestedClipPath(svgUse, targetBounds, assetLoader, uris, lastClip);
            }
        });
    }

    private static SKMatrix CreateUseClipTransform(SvgUse svgUse, SKRect targetBounds)
    {
        var mayHaveGeometryLengthCssDeclarations = svgUse.MayHaveGeometryLengthCssDeclarations();
        var x = SvgGeometryService.GetComputedUnit(svgUse, "x", svgUse.X, mayHaveGeometryLengthCssDeclarations).ToDeviceValue(UnitRenderingType.Horizontal, svgUse, targetBounds);
        var y = SvgGeometryService.GetComputedUnit(svgUse, "y", svgUse.Y, mayHaveGeometryLengthCssDeclarations).ToDeviceValue(UnitRenderingType.Vertical, svgUse, targetBounds);
        var useBounds = SKRect.Create(x, y, targetBounds.Width, targetBounds.Height);
        return TransformsService.ToMatrix(svgUse.Transforms, svgUse, useBounds, targetBounds).PreConcat(SKMatrix.CreateTranslation(x, y));
    }

    private static void ApplyUseClipTransform(IList<PathClip> clips, int startIndex, SKMatrix useTransform)
    {
        if (useTransform.IsIdentity)
        {
            return;
        }

        for (var i = startIndex; i < clips.Count; i++)
        {
            var clip = clips[i];
            clip.Transform = useTransform.PreConcat(clip.Transform ?? SKMatrix.CreateIdentity());
        }
    }

    private static void WithUseInstanceStyleScope(SvgElement element, SvgUse useElement, Action action)
    {
        _ = element.WithUseInstanceStyleScope(useElement, () =>
        {
            action();
            return true;
        });
    }

    private static void AddTextClip(
        SvgText svgText,
        SKRect targetBounds,
        ISvgAssetLoader assetLoader,
        HashSet<Uri> uris,
        ClipPath clipPath)
    {
        var path = SvgSceneTextCompiler.CreateClipPath(svgText, targetBounds, assetLoader);
        if (path is null || path.IsEmpty)
        {
            return;
        }

        var pathClip = new PathClip
        {
            Path = path,
            Transform = TransformsService.ToMatrix(svgText.Transforms, svgText, path.Bounds, path.Bounds),
            Clip = new ClipPath
            {
                Clip = new ClipPath()
            }
        };

        clipPath.Clips?.Add(pathClip);
        PopulateNestedClipPath(svgText, path.Bounds, assetLoader, uris, pathClip.Clip!);
    }

    private static void PopulateNestedClipPath(
        SvgVisualElement visualElement,
        SKRect targetBounds,
        ISvgAssetLoader assetLoader,
        HashSet<Uri> uris,
        ClipPath clipPath)
    {
        var referenceUri = GetReferenceUri(visualElement, "clip-path");
        if (referenceUri is null ||
            !uris.Add(referenceUri))
        {
            return;
        }

        var referencedClipPath = SvgService.GetReference<SvgClipPath>(visualElement, referenceUri);
        if (referencedClipPath?.Children is null ||
            !referencedClipPath.PassesConditionalProcessing(DrawAttributes.None))
        {
            return;
        }

        PopulateClipPath(referencedClipPath, targetBounds, assetLoader, uris, clipPath, svgClipPathClipRule: null);
    }

    private static SvgFillRule ToFillRule(SvgVisualElement visualElement, SvgClipRule? svgClipPathClipRule)
    {
        var svgClipRule = svgClipPathClipRule ?? visualElement.ClipRule;
        return svgClipRule == SvgClipRule.EvenOdd
            ? SvgFillRule.EvenOdd
            : SvgFillRule.NonZero;
    }

    private static SvgClipRule? GetSvgClipRule(SvgClipPath svgClipPath)
    {
        if (!SvgService.TryGetAttribute(svgClipPath, "clip-rule", out var clipRuleString))
        {
            return null;
        }

        return clipRuleString switch
        {
            "nonzero" => SvgClipRule.NonZero,
            "evenodd" => SvgClipRule.EvenOdd,
            "inherit" => SvgClipRule.Inherit,
            _ => null
        };
    }
}
