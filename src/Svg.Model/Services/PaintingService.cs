// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg.DataTypes;

namespace Svg.Model.Services;

internal static class PaintingService
{
    private static readonly ConcurrentDictionary<string, SvgUnit> s_fontSizeUnitCache = new(StringComparer.Ordinal);
    private const int FontSizeUnitCacheLimit = 512;

    internal static float AdjustSvgOpacity(float opacity)
    {
        return Math.Min(Math.Max(opacity, 0), 1);
    }

    internal static byte CombineWithOpacity(byte alpha, float opacity)
    {
        return (byte)Math.Round(opacity * (alpha / 255.0) * 255);
    }

    internal static SKColor GetColor(SvgColourServer svgColourServer, float opacity, DrawAttributes ignoreAttributes)
    {
        var colour = svgColourServer.Colour;
        var alpha = ignoreAttributes.HasFlag(DrawAttributes.Opacity) ?
            svgColourServer.Colour.A :
            CombineWithOpacity(svgColourServer.Colour.A, opacity);

        return new SKColor(colour.R, colour.G, colour.B, alpha);
    }

    internal static SKColor? GetColor(SvgVisualElement svgVisualElement, SvgPaintServer server)
    {
        if (server is SvgDeferredPaintServer svgDeferredPaintServer)
        {
            server = SvgDeferredPaintServer.TryGet<SvgPaintServer>(svgDeferredPaintServer, svgVisualElement);
        }

        if (server is SvgColourServer stopColorSvgColourServer)
        {
            return GetColor(stopColorSvgColourServer, 1f, DrawAttributes.None);
        }

        return new SKColor(0x00, 0x00, 0x00, 0xFF);
    }

    internal static SKPathEffect? CreateDash(SvgElement svgElement, SKRect skBounds, SKPath? geometryPath = null)
    {
        var strokeDashArray = svgElement.StrokeDashArray;
        var strokeDashOffset = svgElement.StrokeDashOffset;
        var count = strokeDashArray.Count;

        if (strokeDashArray is { } && count > 0)
        {
            var isOdd = count % 2 != 0;
            var sum = 0f;
            var intervals = new float[isOdd ? count * 2 : count];
            if (geometryPath is null)
            {
                _ = SvgGeometryService.TryCreateEquivalentPath(svgElement, skBounds, out geometryPath);
            }

            var normalization = SvgGeometryService.CreatePathLengthNormalization(svgElement, geometryPath);
            for (var i = 0; i < count; i++)
            {
                var dash = normalization.ToActualDistance(strokeDashArray[i].ToDeviceValue(UnitRenderingType.Other, svgElement, skBounds));
                if (dash < 0f)
                {
                    return default;
                }

                intervals[i] = dash;

                if (isOdd)
                {
                    intervals[i + count] = intervals[i];
                }

                sum += dash;
            }

            if (sum <= 0f)
            {
                return default;
            }

            var phase = normalization.ToActualDistance(strokeDashOffset.ToDeviceValue(UnitRenderingType.Other, svgElement, skBounds));

            return SKPathEffect.CreateDash(intervals, phase);
        }

        return default;
    }

    private static List<SvgGradientServer> GetLinkedGradientServer(SvgGradientServer svgGradientServer, SvgVisualElement svgVisualElement)
    {
        var svgGradientServers = new List<SvgGradientServer>();
        var visited = new HashSet<SvgGradientServer>();
        var currentGradientServer = svgGradientServer;
        do
        {
            if (!visited.Add(currentGradientServer))
            {
                break;
            }

            svgGradientServers.Add(currentGradientServer);
            currentGradientServer = SvgDeferredPaintServer.TryGet<SvgGradientServer>(currentGradientServer.InheritGradient, svgVisualElement);
        } while (currentGradientServer is { });
        return svgGradientServers;
    }

    private static void GetStopsImpl(
        SvgGradientServer svgGradientServer,
        SKRect skBounds,
        List<SKColor> colors,
        List<float> colorPos,
        SvgVisualElement svgVisualElement,
        float opacity,
        DrawAttributes ignoreAttributes,
        bool isLinearRgb)
    {
        foreach (var child in svgGradientServer.Children)
        {
            if (child is SvgGradientStop svgGradientStop)
            {
                var server = svgGradientStop.StopColor;
                if (server is SvgDeferredPaintServer svgDeferredPaintServer)
                {
                    // Gradient stop paint servers resolve in the gradient definition context, not
                    // on the consuming element. This matters for currentColor/inherit cases like
                    // W3C pservers-grad-18-b where the gradient is defined under one color scope
                    // and referenced from another.
                    server = SvgDeferredPaintServer.TryGet<SvgPaintServer>(svgDeferredPaintServer, svgGradientStop);
                    if (server is null)
                    {
                        // TODO: server is sometimes null with currentColor
                    }
                }

                if (server is SvgColourServer stopColorSvgColourServer)
                {
                    var stopOpacity = AdjustSvgOpacity(svgGradientStop.StopOpacity);
                    var stopColor = GetColor(stopColorSvgColourServer, opacity * stopOpacity, ignoreAttributes);
                    if (isLinearRgb)
                    {
                        stopColor = ToLinear(stopColor);
                    }
                    colors.Add(stopColor);
                    colorPos.Add(GetGradientStopOffset(svgGradientStop));
                }
            }
        }
    }

    internal static void GetStops(
        List<SvgGradientServer> svgReferencedGradientServers,
        SKRect skBounds,
        List<SKColor> colors,
        List<float> colorPos,
        SvgVisualElement svgVisualElement,
        float opacity,
        DrawAttributes ignoreAttributes,
        bool isLinearRgb)
    {
        foreach (var svgReferencedGradientServer in svgReferencedGradientServers)
        {
            if (colors.Count == 0)
            {
                GetStopsImpl(svgReferencedGradientServer, skBounds, colors, colorPos, svgVisualElement, opacity, ignoreAttributes, isLinearRgb);
                if (colors.Count > 0)
                {
                    return;
                }
            }
        }
    }

    private static void AdjustStopColorPos(List<float> colorPos)
    {
        var maxPos = float.MinValue;
        for (var i = 0; i < colorPos.Count; i++)
        {
            var pos = colorPos[i];
            if (pos > maxPos)
            {
                maxPos = pos;
            }
            else if (pos < maxPos)
            {
                colorPos[i] = maxPos;
            }
        }
    }

    private static float GetGradientStopOffset(SvgGradientStop svgGradientStop)
    {
        var offset = svgGradientStop.Offset;
        var value = offset.Type == SvgUnitType.Percentage ? offset.Value / 100f : offset.Value;
        if (float.IsNaN(value) || float.IsNegativeInfinity(value))
        {
            return 0f;
        }

        if (float.IsPositiveInfinity(value))
        {
            return 1f;
        }

        return Math.Min(Math.Max(value, 0f), 1f);
    }

    internal static SKColorF[] ToSkColorF(this SKColor[] skColors)
    {
        var skColorsF = new SKColorF[skColors.Length];

        for (var i = 0; i < skColors.Length; i++)
        {
            skColorsF[i] = skColors[i];
        }

        return skColorsF;
    }

    private static SKColor ToLinear(SKColor color)
    {
        byte r = (byte)Math.Round(FilterEffectsService.SRGBToLinear(color.Red / 255f) * 255f);
        byte g = (byte)Math.Round(FilterEffectsService.SRGBToLinear(color.Green / 255f) * 255f);
        byte b = (byte)Math.Round(FilterEffectsService.SRGBToLinear(color.Blue / 255f) * 255f);
        return new SKColor(r, g, b, color.Alpha);
    }

    internal static SvgColourInterpolation GetColorInterpolation(SvgElement svgElement)
    {
        return svgElement.ColorInterpolation switch
        {
            SvgColourInterpolation.Auto => SvgColourInterpolation.SRGB,
            SvgColourInterpolation.SRGB => SvgColourInterpolation.SRGB,
            SvgColourInterpolation.LinearRGB => SvgColourInterpolation.LinearRGB,
            _ => SvgColourInterpolation.SRGB,
        };
    }

    internal static SKShader CreateLinearGradient(SvgLinearGradientServer svgLinearGradientServer, SKRect skBounds, SvgVisualElement svgVisualElement, float opacity, DrawAttributes ignoreAttributes, SKColorSpace skColorSpace)
    {
        var svgReferencedGradientServers = GetLinkedGradientServer(svgLinearGradientServer, svgVisualElement);

        SvgGradientServer? firstSpreadMethod = default;
        SvgGradientServer? firstGradientTransform = default;
        SvgGradientServer? firstGradientUnits = default;
        SvgLinearGradientServer? firstX1 = default;
        SvgLinearGradientServer? firstY1 = default;
        SvgLinearGradientServer? firstX2 = default;
        SvgLinearGradientServer? firstY2 = default;

        foreach (var p in svgReferencedGradientServers)
        {
            if (firstSpreadMethod is null)
            {
                if (SvgService.TryGetAttribute(p, "spreadMethod", out _))
                {
                    firstSpreadMethod = p;
                }
            }
            if (firstGradientTransform is null)
            {
                var pGradientTransform = p.GradientTransform;
                if (pGradientTransform is { } && pGradientTransform.Count > 0)
                {
                    firstGradientTransform = p;
                }
            }
            if (firstGradientUnits is null)
            {
                if (SvgService.TryGetAttribute(p, "gradientUnits", out _))
                {
                    firstGradientUnits = p;
                }
            }

            if (p is SvgLinearGradientServer svgLinearGradientServerHref)
            {
                if (firstX1 is null)
                {
                    var pX1 = svgLinearGradientServerHref.X1;
                    if (pX1 != SvgUnit.None && SvgService.TryGetAttribute(svgLinearGradientServerHref, "x1", out _))
                    {
                        firstX1 = svgLinearGradientServerHref;
                    }
                }
                if (firstY1 is null)
                {
                    var pY1 = svgLinearGradientServerHref.Y1;
                    if (pY1 != SvgUnit.None && SvgService.TryGetAttribute(svgLinearGradientServerHref, "y1", out _))
                    {
                        firstY1 = svgLinearGradientServerHref;
                    }
                }
                if (firstX2 is null)
                {
                    if (svgLinearGradientServerHref.X2 is { } pX2 && pX2 != SvgUnit.None && SvgService.TryGetAttribute(svgLinearGradientServerHref, "x2", out _))
                    {
                        firstX2 = svgLinearGradientServerHref;
                    }
                }
                if (firstY2 is null)
                {
                    var pY2 = svgLinearGradientServerHref.Y2;
                    if (pY2 != SvgUnit.None && SvgService.TryGetAttribute(svgLinearGradientServerHref, "y2", out _))
                    {
                        firstY2 = svgLinearGradientServerHref;
                    }
                }
            }
        }

        var svgSpreadMethod = firstSpreadMethod?.SpreadMethod ?? SvgGradientSpreadMethod.Pad;
        var svgGradientTransform = firstGradientTransform?.GradientTransform;
        var svgGradientUnits = firstGradientUnits?.GradientUnits ?? SvgCoordinateUnits.ObjectBoundingBox;
        var x1Unit = firstX1?.X1 ?? new SvgUnit(SvgUnitType.Percentage, 0f);
        var y1Unit = firstY1?.Y1 ?? new SvgUnit(SvgUnitType.Percentage, 0f);
        var x2Unit = firstX2?.X2 ?? new SvgUnit(SvgUnitType.Percentage, 100f);
        var y2Unit = firstY2?.Y2 ?? new SvgUnit(SvgUnitType.Percentage, 0f);

        var normalizedX1 = x1Unit.Normalize(svgGradientUnits);
        var normalizedY1 = y1Unit.Normalize(svgGradientUnits);
        var normalizedX2 = x2Unit.Normalize(svgGradientUnits);
        var normalizedY2 = y2Unit.Normalize(svgGradientUnits);

        var x1 = normalizedX1.ToDeviceValue(UnitRenderingType.Horizontal, svgLinearGradientServer, skBounds);
        var y1 = normalizedY1.ToDeviceValue(UnitRenderingType.Vertical, svgLinearGradientServer, skBounds);
        var x2 = normalizedX2.ToDeviceValue(UnitRenderingType.Horizontal, svgLinearGradientServer, skBounds);
        var y2 = normalizedY2.ToDeviceValue(UnitRenderingType.Vertical, svgLinearGradientServer, skBounds);

        if (!IsFinite(x1) || !IsFinite(y1) || !IsFinite(x2) || !IsFinite(y2))
        {
            return SKShader.CreateColor(new SKColor(0xFF, 0xFF, 0xFF, 0x00), skColorSpace);
        }

        var skStart = new SKPoint(x1, y1);
        var skEnd = new SKPoint(x2, y2);
        var colors = new List<SKColor>();
        var colorPos = new List<float>();

        var isLinearRgb = skColorSpace == SKColorSpace.SrgbLinear;
        GetStops(svgReferencedGradientServers, skBounds, colors, colorPos, svgVisualElement, opacity, ignoreAttributes, isLinearRgb);
        AdjustStopColorPos(colorPos);

        var shaderTileMode = svgSpreadMethod switch
        {
            SvgGradientSpreadMethod.Reflect => SKShaderTileMode.Mirror,
            SvgGradientSpreadMethod.Repeat => SKShaderTileMode.Repeat,
            _ => SKShaderTileMode.Clamp,
        };
        var skColors = colors.ToArray();
        float[] skColorPos = colorPos.ToArray();

        if (skColors.Length == 0)
        {
            return SKShader.CreateColor(new SKColor(0xFF, 0xFF, 0xFF, 0x00), skColorSpace);
        }
        else if (skColors.Length == 1)
        {
            return SKShader.CreateColor(skColors[0], skColorSpace);
        }

        if (svgGradientUnits == SvgCoordinateUnits.ObjectBoundingBox)
        {
            var skBoundingBoxTransform = new SKMatrix
            {
                ScaleX = skBounds.Width,
                SkewY = 0f,
                SkewX = 0f,
                ScaleY = skBounds.Height,
                TransX = skBounds.Left,
                TransY = skBounds.Top,
                Persp0 = 0,
                Persp1 = 0,
                Persp2 = 1
            };

            if (svgGradientTransform is { } && svgGradientTransform.Count > 0)
            {
                var gradientTransform = TransformsService.ToMatrix(svgGradientTransform);
                skBoundingBoxTransform = skBoundingBoxTransform.PreConcat(gradientTransform);
            }

            var skColorsF = ToSkColorF(skColors);
            return SKShader.CreateLinearGradient(skStart, skEnd, skColorsF, skColorSpace, skColorPos, shaderTileMode, skBoundingBoxTransform);
        }
        else
        {
            if (svgGradientTransform is { } && svgGradientTransform.Count > 0)
            {
                var gradientTransform = TransformsService.ToMatrix(svgGradientTransform);
                var skColorsF = ToSkColorF(skColors);
                return SKShader.CreateLinearGradient(skStart, skEnd, skColorsF, skColorSpace, skColorPos, shaderTileMode, gradientTransform);
            }
            else
            {
                var skColorsF = ToSkColorF(skColors);
                return SKShader.CreateLinearGradient(skStart, skEnd, skColorsF, skColorSpace, skColorPos, shaderTileMode);
            }
        }
    }

    internal static SKShader CreateTwoPointConicalGradient(SvgRadialGradientServer svgRadialGradientServer, SKRect skBounds, SvgVisualElement svgVisualElement, float opacity, DrawAttributes ignoreAttributes, SKColorSpace skColorSpace)
    {
        var svgReferencedGradientServers = GetLinkedGradientServer(svgRadialGradientServer, svgVisualElement);

        SvgGradientServer? firstSpreadMethod = default;
        SvgGradientServer? firstGradientTransform = default;
        SvgGradientServer? firstGradientUnits = default;
        SvgRadialGradientServer? firstCenterX = default;
        SvgRadialGradientServer? firstCenterY = default;
        SvgRadialGradientServer? firstRadius = default;
        SvgRadialGradientServer? firstFocalX = default;
        SvgRadialGradientServer? firstFocalY = default;
        SvgRadialGradientServer? firstFocalRadius = default;

        foreach (var p in svgReferencedGradientServers)
        {
            if (firstSpreadMethod is null)
            {
                if (SvgService.TryGetAttribute(p, "spreadMethod", out _))
                {
                    firstSpreadMethod = p;
                }
            }
            if (firstGradientTransform is null)
            {
                var pGradientTransform = p.GradientTransform;
                if (pGradientTransform is { } && pGradientTransform.Count > 0)
                {
                    firstGradientTransform = p;
                }
            }
            if (firstGradientUnits is null)
            {
                if (SvgService.TryGetAttribute(p, "gradientUnits", out _))
                {
                    firstGradientUnits = p;
                }
            }

            if (p is SvgRadialGradientServer svgRadialGradientServerHref)
            {
                if (firstCenterX is null)
                {
                    var pCenterX = svgRadialGradientServerHref.CenterX;
                    if (pCenterX != SvgUnit.None && SvgService.TryGetAttribute(svgRadialGradientServerHref, "cx", out _))
                    {
                        firstCenterX = svgRadialGradientServerHref;
                    }
                }
                if (firstCenterY is null)
                {
                    if (svgRadialGradientServerHref.CenterY is { } pCenterY && pCenterY != SvgUnit.None && SvgService.TryGetAttribute(svgRadialGradientServerHref, "cy", out _))
                    {
                        firstCenterY = svgRadialGradientServerHref;
                    }
                }
                if (firstRadius is null)
                {
                    var pRadius = svgRadialGradientServerHref.Radius;
                    if (pRadius != SvgUnit.None && SvgService.TryGetAttribute(svgRadialGradientServerHref, "r", out _))
                    {
                        firstRadius = svgRadialGradientServerHref;
                    }
                }
                if (firstFocalX is null)
                {
                    var pFocalX = svgRadialGradientServerHref.FocalX;
                    if (pFocalX != SvgUnit.None && SvgService.TryGetAttribute(svgRadialGradientServerHref, "fx", out _))
                    {
                        firstFocalX = svgRadialGradientServerHref;
                    }
                }
                if (firstFocalY is null)
                {
                    var pFocalY = svgRadialGradientServerHref.FocalY;
                    if (pFocalY != SvgUnit.None && SvgService.TryGetAttribute(svgRadialGradientServerHref, "fy", out _))
                    {
                        firstFocalY = svgRadialGradientServerHref;
                    }
                }
                if (firstFocalRadius is null)
                {
                    var pFocalRadius = svgRadialGradientServerHref.FocalRadius;
                    if (pFocalRadius != SvgUnit.None && SvgService.TryGetAttribute(svgRadialGradientServerHref, "fr", out _))
                    {
                        firstFocalRadius = svgRadialGradientServerHref;
                    }
                }
            }
        }

        var svgSpreadMethod = firstSpreadMethod?.SpreadMethod ?? SvgGradientSpreadMethod.Pad;
        var svgGradientTransform = firstGradientTransform?.GradientTransform;
        var svgGradientUnits = firstGradientUnits?.GradientUnits ?? SvgCoordinateUnits.ObjectBoundingBox;
        var centerXUnit = firstCenterX?.CenterX ?? new SvgUnit(SvgUnitType.Percentage, 50f);
        var centerYUnit = firstCenterY?.CenterY ?? new SvgUnit(SvgUnitType.Percentage, 50f);
        var radiusUnit = firstRadius?.Radius ?? new SvgUnit(SvgUnitType.Percentage, 50f);
        var focalXUnit = firstFocalX?.FocalX ?? centerXUnit;
        var focalYUnit = firstFocalY?.FocalY ?? centerYUnit;
        var focalRadiusUnit = firstFocalRadius?.FocalRadius ?? new SvgUnit(SvgUnitType.Percentage, 0f);

        var normalizedCenterX = centerXUnit.Normalize(svgGradientUnits);
        var normalizedCenterY = centerYUnit.Normalize(svgGradientUnits);
        var normalizedRadius = radiusUnit.Normalize(svgGradientUnits);
        var normalizedFocalX = focalXUnit.Normalize(svgGradientUnits);
        var normalizedFocalY = focalYUnit.Normalize(svgGradientUnits);

        var centerX = normalizedCenterX.ToDeviceValue(UnitRenderingType.Horizontal, svgRadialGradientServer, skBounds);
        var centerY = normalizedCenterY.ToDeviceValue(UnitRenderingType.Vertical, svgRadialGradientServer, skBounds);

        var radius = normalizedRadius.ToDeviceValue(UnitRenderingType.Other, svgRadialGradientServer, skBounds);
        if (radius < 0f)
        {
            return SKShader.CreateColor(new SKColor(0xFF, 0xFF, 0xFF, 0x00), skColorSpace);
        }

        var focalX = normalizedFocalX.ToDeviceValue(UnitRenderingType.Horizontal, svgRadialGradientServer, skBounds);
        var focalY = normalizedFocalY.ToDeviceValue(UnitRenderingType.Vertical, svgRadialGradientServer, skBounds);
        var focalRadius = focalRadiusUnit.Normalize(svgGradientUnits).ToDeviceValue(UnitRenderingType.Other, svgRadialGradientServer, skBounds);

        if (!IsFinite(centerX) || !IsFinite(centerY) || !IsFinite(radius) ||
            !IsFinite(focalX) || !IsFinite(focalY) || !IsFinite(focalRadius))
        {
            return SKShader.CreateColor(new SKColor(0xFF, 0xFF, 0xFF, 0x00), skColorSpace);
        }

        var skCenter = new SKPoint(centerX, centerY);
        var skFocal = new SKPoint(focalX, focalY);

        var colors = new List<SKColor>();
        var colorPos = new List<float>();

        var isLinearRgb = skColorSpace == SKColorSpace.SrgbLinear;
        GetStops(svgReferencedGradientServers, skBounds, colors, colorPos, svgVisualElement, opacity, ignoreAttributes, isLinearRgb);
        AdjustStopColorPos(colorPos);

        var shaderTileMode = svgSpreadMethod switch
        {
            SvgGradientSpreadMethod.Reflect => SKShaderTileMode.Mirror,
            SvgGradientSpreadMethod.Repeat => SKShaderTileMode.Repeat,
            _ => SKShaderTileMode.Clamp,
        };
        var skColors = colors.ToArray();
        float[] skColorPos = colorPos.ToArray();

        if (skColors.Length == 0)
        {
            return SKShader.CreateColor(new SKColor(0xFF, 0xFF, 0xFF, 0x00), skColorSpace);
        }
        else if (skColors.Length == 1)
        {
            return SKShader.CreateColor(skColors[0], skColorSpace);
        }

        if (radius == 0.0)
        {
            return SKShader.CreateColor(
                skColors.Length > 0 ? skColors[skColors.Length - 1] : new SKColor(0x00, 0x00, 0x00, 0xFF),
                skColorSpace);
        }

        focalRadius = Math.Max(0f, focalRadius);
        if (svgGradientUnits != SvgCoordinateUnits.ObjectBoundingBox)
        {
            skFocal = CorrectRadialGradientFocalPoint(skCenter, radius, skFocal, focalRadius);
        }

        var isRadialGradient = focalRadius == 0f && skCenter.X == skFocal.X && skCenter.Y == skFocal.Y;

        if (svgGradientUnits == SvgCoordinateUnits.ObjectBoundingBox)
        {
            var skBoundingBoxTransform = new SKMatrix
            {
                ScaleX = skBounds.Width,
                SkewY = 0f,
                SkewX = 0f,
                ScaleY = skBounds.Height,
                TransX = skBounds.Left,
                TransY = skBounds.Top,
                Persp0 = 0,
                Persp1 = 0,
                Persp2 = 1
            };

            if (svgGradientTransform is { } && svgGradientTransform.Count > 0)
            {
                var gradientTransform = TransformsService.ToMatrix(svgGradientTransform);
                skBoundingBoxTransform = skBoundingBoxTransform.PreConcat(gradientTransform);
            }

            var skColorsF = ToSkColorF(skColors);

            if (isRadialGradient)
            {
                return SKShader.CreateRadialGradient(
                    skCenter, radius,
                    skColorsF, skColorSpace, skColorPos,
                    shaderTileMode,
                    skBoundingBoxTransform);
            }
            else
            {
                return SKShader.CreateTwoPointConicalGradient(
                    skFocal, focalRadius,
                    skCenter, radius,
                    skColorsF, skColorSpace, skColorPos,
                    shaderTileMode,
                    skBoundingBoxTransform);
            }
        }
        else
        {
            if (svgGradientTransform is { } && svgGradientTransform.Count > 0)
            {
                var gradientTransform = TransformsService.ToMatrix(svgGradientTransform);
                var skColorsF = ToSkColorF(skColors);
                if (isRadialGradient)
                {
                    return SKShader.CreateRadialGradient(
                        skCenter, radius,
                        skColorsF, skColorSpace, skColorPos,
                        shaderTileMode,
                        gradientTransform);
                }
                else
                {
                    return SKShader.CreateTwoPointConicalGradient(
                        skFocal, focalRadius,
                        skCenter, radius,
                        skColorsF, skColorSpace, skColorPos,
                        shaderTileMode, gradientTransform);
                }
            }
            else
            {
                var skColorsF = ToSkColorF(skColors);
                if (isRadialGradient)
                {
                    return SKShader.CreateRadialGradient(
                        skCenter, radius,
                        skColorsF, skColorSpace, skColorPos,
                        shaderTileMode);
                }
                else
                {
                    return SKShader.CreateTwoPointConicalGradient(
                        skFocal, focalRadius,
                        skCenter, radius,
                        skColorsF, skColorSpace, skColorPos,
                        shaderTileMode);
                }
            }
        }
    }

    internal static bool IsAntialias(SvgElement svgElement)
    {
        return svgElement.ShapeRendering switch
        {
            SvgShapeRendering.Inherit => true,
            SvgShapeRendering.Auto => true,
            SvgShapeRendering.GeometricPrecision => true,
            SvgShapeRendering.OptimizeSpeed => false,
            SvgShapeRendering.CrispEdges => false,
            _ => true
        };
    }

    internal static SKPoint CorrectRadialGradientFocalPoint(SKPoint center, float radius, SKPoint focal, float focalRadius)
    {
        if (radius <= 0f)
        {
            return focal;
        }

        var maxDistance = Math.Max(0f, radius - Math.Max(0f, focalRadius));
        var dx = focal.X - center.X;
        var dy = focal.Y - center.Y;
        var distance = (float)Math.Sqrt((dx * dx) + (dy * dy));
        if (distance <= maxDistance || distance <= 0f)
        {
            return focal;
        }

        var scale = maxDistance / distance;
        return new SKPoint(center.X + (dx * scale), center.Y + (dy * scale));
    }

    internal static bool IsValidFill(SvgElement svgElement)
    {
        var fill = svgElement.Fill;
        return fill is { }
            && fill != SvgPaintServer.None;
    }

    internal static bool IsValidStroke(SvgElement svgElement, SKRect skBounds)
    {
        var stroke = svgElement.Stroke;
        var strokeWidth = svgElement.StrokeWidth;
        return stroke is { }
            && stroke != SvgPaintServer.None
            && strokeWidth.ToDeviceValue(UnitRenderingType.Other, svgElement, skBounds) > 0f;
    }

    internal static SKPaint? GetOpacityPaint(float opacity)
    {
        if (opacity < 1f)
        {
            return new SKPaint
            {
                IsAntialias = true,
                Color = new SKColor(255, 255, 255, (byte)Math.Round(opacity * 255)),
                Style = SKPaintStyle.StrokeAndFill
            };
        }
        return default;
    }

    internal static SKFontStyleWeight ToFontStyleWeight(SvgFontWeight svgFontWeight)
    {
        var fontWeight = SKFontStyleWeight.Normal;

        switch (svgFontWeight)
        {
            case SvgFontWeight.Inherit:
                // TODO: Implement SvgFontWeight.Inherit
                break;

            case SvgFontWeight.Bolder:
                // TODO: Implement SvgFontWeight.Bolder
                break;

            case SvgFontWeight.Lighter:
                // TODO: Implement SvgFontWeight.Lighter
                break;

            case SvgFontWeight.W100:
                fontWeight = SKFontStyleWeight.Thin;
                break;

            case SvgFontWeight.W200:
                fontWeight = SKFontStyleWeight.ExtraLight;
                break;

            case SvgFontWeight.W300:
                fontWeight = SKFontStyleWeight.Light;
                break;

            case SvgFontWeight.Normal:
            case SvgFontWeight.W400:
                fontWeight = SKFontStyleWeight.Normal;
                break;

            case SvgFontWeight.W500:
                fontWeight = SKFontStyleWeight.Medium;
                break;

            case SvgFontWeight.W600:
                fontWeight = SKFontStyleWeight.SemiBold;
                break;

            case SvgFontWeight.Bold:
            case SvgFontWeight.W700:
                fontWeight = SKFontStyleWeight.Bold;
                break;

            case SvgFontWeight.W800:
                fontWeight = SKFontStyleWeight.ExtraBold;
                break;

            case SvgFontWeight.W900:
                fontWeight = SKFontStyleWeight.Black;
                break;
        }

        return fontWeight;
    }

    internal static SvgFontWeight ResolveFontWeight(SvgElement svgElement, SvgFontWeight requestedWeight)
    {
        if (requestedWeight == SvgFontWeight.Inherit)
        {
            return GetComputedFontWeight(svgElement.Parent);
        }

        if (requestedWeight == SvgFontWeight.Bolder)
        {
            return NormalizeRelativeFontWeight(GetComputedFontWeight(svgElement.Parent)) switch
            {
                SvgFontWeight.W100 => SvgFontWeight.Normal,
                SvgFontWeight.W200 => SvgFontWeight.Normal,
                SvgFontWeight.W300 => SvgFontWeight.Normal,
                SvgFontWeight.W400 => SvgFontWeight.Bold,
                SvgFontWeight.W500 => SvgFontWeight.Bold,
                SvgFontWeight.W600 => SvgFontWeight.W900,
                SvgFontWeight.W700 => SvgFontWeight.W900,
                SvgFontWeight.W800 => SvgFontWeight.W900,
                SvgFontWeight.W900 => SvgFontWeight.W900,
                _ => SvgFontWeight.Bold
            };
        }

        if (requestedWeight == SvgFontWeight.Lighter)
        {
            return NormalizeRelativeFontWeight(GetComputedFontWeight(svgElement.Parent)) switch
            {
                SvgFontWeight.W100 => SvgFontWeight.W100,
                SvgFontWeight.W200 => SvgFontWeight.W100,
                SvgFontWeight.W300 => SvgFontWeight.W100,
                SvgFontWeight.W400 => SvgFontWeight.W100,
                SvgFontWeight.W500 => SvgFontWeight.W100,
                SvgFontWeight.W600 => SvgFontWeight.Normal,
                SvgFontWeight.W700 => SvgFontWeight.Normal,
                SvgFontWeight.W800 => SvgFontWeight.Bold,
                SvgFontWeight.W900 => SvgFontWeight.Bold,
                _ => SvgFontWeight.Normal
            };
        }

        return requestedWeight;
    }

    private static SvgFontWeight GetComputedFontWeight(SvgElement? svgElement)
    {
        if (svgElement is null)
        {
            return SvgFontWeight.Normal;
        }

        return NormalizeRelativeFontWeight(ResolveFontWeight(svgElement, svgElement.FontWeight));
    }

    private static SvgFontWeight NormalizeRelativeFontWeight(SvgFontWeight fontWeight)
    {
        return fontWeight switch
        {
            SvgFontWeight.Normal => SvgFontWeight.W400,
            SvgFontWeight.Bold => SvgFontWeight.W700,
            _ => fontWeight
        };
    }

    internal static SKFontStyleWidth ToFontStyleWidth(SvgFontStretch svgFontStretch)
    {
        var fontWidth = SKFontStyleWidth.Normal;

        switch (svgFontStretch)
        {
            // TODO: Implement SvgFontStretch.Inherit
            case SvgFontStretch.Inherit:
                break;

            case SvgFontStretch.Normal:
                fontWidth = SKFontStyleWidth.Normal;
                break;

            // TODO: Implement SvgFontStretch.Wider
            case SvgFontStretch.Wider:
                break;

            // TODO: Implement SvgFontStretch.Narrower
            case SvgFontStretch.Narrower:
                break;

            case SvgFontStretch.UltraCondensed:
                fontWidth = SKFontStyleWidth.UltraCondensed;
                break;

            case SvgFontStretch.ExtraCondensed:
                fontWidth = SKFontStyleWidth.ExtraCondensed;
                break;

            case SvgFontStretch.Condensed:
                fontWidth = SKFontStyleWidth.Condensed;
                break;

            case SvgFontStretch.SemiCondensed:
                fontWidth = SKFontStyleWidth.SemiCondensed;
                break;

            case SvgFontStretch.SemiExpanded:
                fontWidth = SKFontStyleWidth.SemiExpanded;
                break;

            case SvgFontStretch.Expanded:
                fontWidth = SKFontStyleWidth.Expanded;
                break;

            case SvgFontStretch.ExtraExpanded:
                fontWidth = SKFontStyleWidth.ExtraExpanded;
                break;

            case SvgFontStretch.UltraExpanded:
                fontWidth = SKFontStyleWidth.UltraExpanded;
                break;
        }

        return fontWidth;
    }

    internal static bool IsRightToLeft(SvgTextBase svgText)
    {
        for (SvgElement? current = svgText; current is not null; current = current.Parent)
        {
            if (current.TryGetOwnCascadedStyleValue("direction", out var direction) &&
                !string.IsNullOrWhiteSpace(direction) &&
                TryResolveDeclaredDirection(direction, out var isRightToLeft))
            {
                return isRightToLeft;
            }

            if (current is SvgTextSpan &&
                current.TryGetAttribute("writing-mode", out _))
            {
                continue;
            }

            if (current.TryGetAttribute("writing-mode", out var writingMode) &&
                !string.IsNullOrWhiteSpace(writingMode))
            {
                if (IsRightToLeftWritingModeValue(writingMode))
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal static bool IsVerticalWritingMode(SvgTextBase svgText)
    {
        for (SvgElement? current = svgText; current is not null; current = current.Parent)
        {
            if (current is SvgTextSpan &&
                current.TryGetAttribute("writing-mode", out _))
            {
                continue;
            }

            if (!current.TryGetAttribute("writing-mode", out var writingMode) ||
                string.IsNullOrWhiteSpace(writingMode))
            {
                continue;
            }

            return IsVerticalWritingModeValue(writingMode);
        }

        return false;
    }

    private static bool TryResolveDeclaredDirection(string direction, out bool isRightToLeft)
    {
        var normalized = direction.AsSpan().Trim();
        if (normalized.Equals("rtl".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            isRightToLeft = true;
            return true;
        }

        if (normalized.Equals("ltr".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("initial".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            isRightToLeft = false;
            return true;
        }

        isRightToLeft = false;
        return false;
    }

    private static bool IsRightToLeftWritingModeValue(string writingMode)
    {
        var normalized = writingMode.AsSpan().Trim();
        return normalized.Equals("rl".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("rl-tb".AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVerticalWritingModeValue(string writingMode)
    {
        var normalized = writingMode.AsSpan().Trim();
        return normalized.Equals("tb".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("tb-rl".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("vertical-rl".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("vertical-lr".AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    internal static SKTextAlign ToTextAlign(SvgTextAnchor textAnchor, bool isRightToLeft)
    {
        return textAnchor switch
        {
            SvgTextAnchor.Middle => SKTextAlign.Center,
            SvgTextAnchor.End => isRightToLeft ? SKTextAlign.Left : SKTextAlign.Right,
            _ => isRightToLeft ? SKTextAlign.Right : SKTextAlign.Left,
        };
    }

    internal static SKFontStyleSlant ToFontStyleSlant(SvgFontStyle fontStyle)
    {
        return fontStyle switch
        {
            SvgFontStyle.Oblique => SKFontStyleSlant.Oblique,
            SvgFontStyle.Italic => SKFontStyleSlant.Italic,
            _ => SKFontStyleSlant.Upright,
        };
    }

    private static void SetTypeface(SvgTextBase svgText, SKPaint skPaint)
    {
        var fontFamily = svgText.FontFamily;
        var fontWeight = ToFontStyleWeight(ResolveFontWeight(svgText, svgText.FontWeight));
        var fontWidth = ToFontStyleWidth(svgText.FontStretch);
        var fontStyle = ToFontStyleSlant(svgText.FontStyle);
        skPaint.Typeface = SKTypeface.FromFamilyName(fontFamily, fontWeight, fontWidth, fontStyle);
    }

    internal static void SetPaintText(SvgTextBase svgText, SKRect skBounds, SKPaint skPaint)
    {
        skPaint.LcdRenderText = true;
        skPaint.SubpixelText = true;
        skPaint.TextEncoding = SKTextEncoding.Utf16;
        if (HasInheritedTextOpenTypePaintProperty(svgText))
        {
            skPaint.FontFeatureSettings = ResolveInheritedTextPaintProperty(svgText, "font-feature-settings", "normal");
            skPaint.FontKerning = ResolveInheritedTextPaintProperty(svgText, "font-kerning", "auto");
            skPaint.FontVariantLigatures = ResolveInheritedTextPaintProperty(svgText, "font-variant-ligatures", "normal");
        }
        else
        {
            skPaint.FontFeatureSettings = null;
            skPaint.FontKerning = null;
            skPaint.FontVariantLigatures = null;
        }

        var isVertical = IsVerticalWritingMode(svgText);
        skPaint.TextAlign = ToTextAlign(svgText.TextAnchor, isVertical ? false : IsRightToLeft(svgText));

        if (svgText.TextDecoration.HasFlag(SvgTextDecoration.Underline))
        {
            // TODO: Implement SvgTextDecoration.Underline
        }

        if (svgText.TextDecoration.HasFlag(SvgTextDecoration.Overline))
        {
            // TODO: Implement SvgTextDecoration.Overline
        }

        if (svgText.TextDecoration.HasFlag(SvgTextDecoration.LineThrough))
        {
            // TODO: Implement SvgTextDecoration.LineThrough
        }

        var fontSize = ResolveFontSize(svgText, skBounds);

        skPaint.TextSize = fontSize;

        SetTypeface(svgText, skPaint);
    }

    private static bool HasInheritedTextOpenTypePaintProperty(SvgElement element)
    {
        for (SvgElement? current = element; current is not null; current = current.Parent)
        {
            if ((current.GetOwnCascadedStyleFeatureFlags(SvgCascadedStyleFeatureFlags.TextOpenType) &
                 SvgCascadedStyleFeatureFlags.TextOpenType) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string? ResolveInheritedTextPaintProperty(
        SvgElement element,
        string propertyName,
        string defaultValue)
    {
        for (SvgElement? current = element; current is not null; current = current.Parent)
        {
            if (!current.TryGetOwnCascadedStyleValue(propertyName, out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var trimmed = value.AsSpan().Trim();
            if (trimmed.Equals("inherit".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("unset".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (trimmed.Equals("initial".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals(defaultValue.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return value;
        }

        return null;
    }

    private static float ResolveFontSize(SvgElement element, SKRect skBounds)
        => ResolveFontSize(element, skBounds, depth: 0);

    private static float ResolveFontSize(SvgElement element, SKRect skBounds, int depth)
    {
        const int maxFontSizeInheritanceDepth = 256;
        if (depth > maxFontSizeInheritanceDepth)
        {
            return 12f;
        }

        if (element is SvgTextBase textBase)
        {
            var inheritedFontSize = textBase.FontSize;
            if (inheritedFontSize != SvgUnit.None &&
                inheritedFontSize != SvgUnit.Empty &&
                inheritedFontSize.Type is not SvgUnitType.Percentage and not SvgUnitType.Em and not SvgUnitType.Ex)
            {
                return inheritedFontSize.ToDeviceValue(UnitRenderingType.Vertical, element, skBounds);
            }
        }

        if (!TryResolveSpecifiedFontSizeUnit(element, out var fontSizeUnit))
        {
            return ResolveParentFontSize(element, skBounds, depth);
        }

        if (fontSizeUnit == SvgUnit.None || fontSizeUnit == SvgUnit.Empty)
        {
            return ResolveParentFontSize(element, skBounds, depth);
        }

        return fontSizeUnit.Type switch
        {
            SvgUnitType.Percentage => ResolveParentFontSize(element, skBounds, depth) * fontSizeUnit.Value / 100f,
            SvgUnitType.Em => ResolveParentFontSize(element, skBounds, depth) * fontSizeUnit.Value,
            SvgUnitType.Ex => ResolveParentFontSize(element, skBounds, depth) * 0.5f * fontSizeUnit.Value,
            _ => fontSizeUnit.ToDeviceValue(UnitRenderingType.Vertical, element, skBounds)
        };
    }

    private static bool TryResolveSpecifiedFontSizeUnit(SvgElement element, out SvgUnit fontSizeUnit)
    {
        if (element.ComputedStyle.TryGetPropertyValue("font-size", out var rawFontSize) &&
            TryParseFontSizeUnit(rawFontSize, out fontSizeUnit))
        {
            return true;
        }

        fontSizeUnit = SvgUnit.Empty;
        return false;
    }

    private static bool TryParseFontSizeUnit(string? value, out SvgUnit fontSizeUnit)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            var cacheKey = value!;
            if (s_fontSizeUnitCache.TryGetValue(cacheKey, out fontSizeUnit))
            {
                return true;
            }

            try
            {
                fontSizeUnit = SvgUnitConverter.Parse(cacheKey.AsSpan().Trim());
                s_fontSizeUnitCache.TryAdd(cacheKey, fontSizeUnit);
                if (s_fontSizeUnitCache.Count > FontSizeUnitCacheLimit)
                {
                    s_fontSizeUnitCache.Clear();
                }

                return true;
            }
            catch (FormatException)
            {
            }
        }

        fontSizeUnit = SvgUnit.Empty;
        return false;
    }

    private static float ResolveParentFontSize(SvgElement element, SKRect skBounds, int depth)
    {
        if (element.Parent is { } parent)
        {
            return ResolveFontSize(parent, skBounds, depth + 1);
        }

        return 12f;
    }

    private static bool IsFinite(float value)
        => !float.IsNaN(value) && !float.IsInfinity(value);
}
