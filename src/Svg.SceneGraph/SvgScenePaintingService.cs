using System;
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg;
using Svg.DataTypes;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia;

internal static class SvgScenePaintingService
{
    internal static float AdjustSvgOpacity(float opacity)
    {
        return Math.Min(Math.Max(opacity, 0f), 1f);
    }

    internal static SKPaint? GetOpacityPaint(float opacity)
    {
        var adjustedOpacity = AdjustSvgOpacity(opacity);
        if (adjustedOpacity >= 1f)
        {
            return null;
        }

        return new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(255, 255, 255, (byte)Math.Round(adjustedOpacity * 255f)),
            Style = SKPaintStyle.StrokeAndFill
        };
    }

    internal static bool IsValidFill(SvgElement svgElement)
    {
        var fill = svgElement.Fill;
        return fill is not null && fill != SvgPaintServer.None;
    }

    internal static bool IsValidStroke(SvgElement svgElement, SKRect skBounds)
    {
        var stroke = svgElement.Stroke;
        var strokeWidth = svgElement.StrokeWidth;
        return stroke is not null
            && stroke != SvgPaintServer.None
            && strokeWidth.ToDeviceValue(UnitRenderingType.Other, svgElement, skBounds) > 0f;
    }

    internal static SKPaint? GetFillPaint(
        SvgVisualElement svgVisualElement,
        SKRect skBounds,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes)
    {
        var skPaint = new SKPaint
        {
            IsAntialias = PaintingService.IsAntialias(svgVisualElement),
            Style = SKPaintStyle.Fill
        };

        var opacity = AdjustSvgOpacity(svgVisualElement.FillOpacity);
        return TryApplyPaintServer(svgVisualElement, svgVisualElement.Fill, opacity, skBounds, skPaint, forStroke: false, assetLoader, ignoreAttributes)
            ? skPaint
            : null;
    }

    internal static SKPaint? GetStrokePaint(
        SvgVisualElement svgVisualElement,
        SKRect skBounds,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes)
    {
        var skPaint = new SKPaint
        {
            IsAntialias = PaintingService.IsAntialias(svgVisualElement),
            Style = SKPaintStyle.Stroke
        };

        var opacity = AdjustSvgOpacity(svgVisualElement.StrokeOpacity);
        if (!TryApplyPaintServer(svgVisualElement, svgVisualElement.Stroke, opacity, skBounds, skPaint, forStroke: true, assetLoader, ignoreAttributes))
        {
            return null;
        }

        skPaint.StrokeCap = svgVisualElement.StrokeLineCap switch
        {
            SvgStrokeLineCap.Round => SKStrokeCap.Round,
            SvgStrokeLineCap.Square => SKStrokeCap.Square,
            _ => SKStrokeCap.Butt
        };

        skPaint.StrokeJoin = svgVisualElement.StrokeLineJoin switch
        {
            SvgStrokeLineJoin.Round => SKStrokeJoin.Round,
            SvgStrokeLineJoin.Bevel => SKStrokeJoin.Bevel,
            _ => SKStrokeJoin.Miter
        };

        skPaint.StrokeMiter = svgVisualElement.StrokeMiterLimit;
        skPaint.StrokeWidth = svgVisualElement.StrokeWidth.ToDeviceValue(UnitRenderingType.Other, svgVisualElement, skBounds);

        if (svgVisualElement.StrokeDashArray is { })
        {
            SetDash(svgVisualElement, skPaint, skBounds);
        }

        return skPaint;
    }

    private static bool TryApplyPaintServer(
        SvgVisualElement svgVisualElement,
        SvgPaintServer? server,
        float opacity,
        SKRect skBounds,
        SKPaint skPaint,
        bool forStroke,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes)
    {
        if (server is null)
        {
            return false;
        }

        var fallbackServer = SvgPaintServer.None;
        if (server is SvgDeferredPaintServer deferredServer)
        {
            server = SvgDeferredPaintServer.TryGet<SvgPaintServer>(deferredServer, svgVisualElement);
            fallbackServer = deferredServer.FallbackServer;
            if (server is null)
            {
                server = fallbackServer ?? SvgPaintServer.NotSet;
                fallbackServer = null;
            }
        }

        if (server == SvgPaintServer.None)
        {
            return false;
        }

        switch (server)
        {
            case SvgColourServer svgColourServer:
                return TryApplyColor(svgVisualElement, svgColourServer, opacity, skPaint, ignoreAttributes);

            case SvgPatternServer svgPatternServer:
                {
                    var colorInterpolation = GetColorInterpolation(svgVisualElement);
                    var isLinearRgb = colorInterpolation == SvgColourInterpolation.LinearRGB;
                    var skColorSpace = isLinearRgb ? SKColorSpace.SrgbLinear : SKColorSpace.Srgb;
                    var skPatternShader = CreatePatternShader(svgPatternServer, skBounds, svgVisualElement, opacity, assetLoader, ignoreAttributes);
                    if (skPatternShader is not null)
                    {
                        skPaint.Shader = skPatternShader;
                        return true;
                    }

                    return TryApplyFallbackColor(fallbackServer, opacity, skPaint, ignoreAttributes, skColorSpace);
                }

            case SvgLinearGradientServer svgLinearGradientServer:
                {
                    var colorInterpolation = GetColorInterpolation(svgLinearGradientServer);
                    var isLinearRgb = colorInterpolation == SvgColourInterpolation.LinearRGB;
                    var skColorSpace = isLinearRgb ? SKColorSpace.SrgbLinear : SKColorSpace.Srgb;

                    if (svgLinearGradientServer.GradientUnits == SvgCoordinateUnits.ObjectBoundingBox &&
                        (skBounds.Width == 0f || skBounds.Height == 0f))
                    {
                        return TryApplyFallbackColor(fallbackServer, opacity, skPaint, ignoreAttributes, skColorSpace);
                    }

                    var shader = CreateLinearGradient(svgLinearGradientServer, skBounds, svgVisualElement, opacity, ignoreAttributes, skColorSpace);
                    if (shader is null)
                    {
                        return false;
                    }

                    skPaint.Shader = shader;
                    return true;
                }

            case SvgRadialGradientServer svgRadialGradientServer:
                {
                    var colorInterpolation = GetColorInterpolation(svgRadialGradientServer);
                    var isLinearRgb = colorInterpolation == SvgColourInterpolation.LinearRGB;
                    var skColorSpace = isLinearRgb ? SKColorSpace.SrgbLinear : SKColorSpace.Srgb;

                    if (svgRadialGradientServer.GradientUnits == SvgCoordinateUnits.ObjectBoundingBox &&
                        (skBounds.Width == 0f || skBounds.Height == 0f))
                    {
                        return TryApplyFallbackColor(fallbackServer, opacity, skPaint, ignoreAttributes, skColorSpace);
                    }

                    var shader = CreateTwoPointConicalGradient(svgRadialGradientServer, skBounds, svgVisualElement, opacity, ignoreAttributes, skColorSpace);
                    if (shader is null)
                    {
                        return false;
                    }

                    skPaint.Shader = shader;
                    return true;
                }

            case SvgDeferredPaintServer svgDeferredPaintServer:
                return TryApplyPaintServer(svgVisualElement, svgDeferredPaintServer, opacity, skBounds, skPaint, forStroke, assetLoader, ignoreAttributes);

            default:
                return false;
        }
    }

    private static bool TryApplyColor(
        SvgVisualElement svgVisualElement,
        SvgColourServer svgColourServer,
        float opacity,
        SKPaint skPaint,
        DrawAttributes ignoreAttributes)
    {
        var skColor = GetColor(svgColourServer, opacity, ignoreAttributes);
        var colorInterpolation = GetColorInterpolation(svgVisualElement);
        var isLinearRgb = colorInterpolation == SvgColourInterpolation.LinearRGB;

        if (colorInterpolation == SvgColourInterpolation.SRGB)
        {
            skPaint.Color = skColor;
            skPaint.Shader = null;
            return true;
        }

        var skColorSpace = isLinearRgb ? SKColorSpace.SrgbLinear : SKColorSpace.Srgb;
        if (isLinearRgb)
        {
            skColor = ToLinear(skColor);
        }

        var shader = SKShader.CreateColor(skColor, skColorSpace);
        if (shader is null)
        {
            return false;
        }

        skPaint.Shader = shader;
        return true;
    }

    private static bool TryApplyFallbackColor(
        SvgPaintServer? fallbackServer,
        float opacity,
        SKPaint skPaint,
        DrawAttributes ignoreAttributes,
        SKColorSpace skColorSpace)
    {
        if (fallbackServer is not SvgColourServer svgColourServerFallback)
        {
            return false;
        }

        var skColor = GetColor(svgColourServerFallback, opacity, ignoreAttributes);
        if (skColorSpace == SKColorSpace.Srgb)
        {
            skPaint.Color = skColor;
            skPaint.Shader = null;
            return true;
        }

        if (skColorSpace == SKColorSpace.SrgbLinear)
        {
            skColor = ToLinear(skColor);
        }

        var shader = SKShader.CreateColor(skColor, skColorSpace);
        if (shader is null)
        {
            return false;
        }

        skPaint.Shader = shader;
        return true;
    }

    private static SKColor GetColor(SvgColourServer svgColourServer, float opacity, DrawAttributes ignoreAttributes)
    {
        var colour = svgColourServer.Colour;
        var alpha = ignoreAttributes.HasFlag(DrawAttributes.Opacity)
            ? svgColourServer.Colour.A
            : CombineWithOpacity(svgColourServer.Colour.A, opacity);

        return new SKColor(colour.R, colour.G, colour.B, alpha);
    }

    private static byte CombineWithOpacity(byte alpha, float opacity)
    {
        return (byte)Math.Round(opacity * (alpha / 255.0) * 255.0);
    }

    private static SKColor ToLinear(SKColor color)
    {
        var r = (byte)Math.Round(FilterEffectsService.SRGBToLinear(color.Red / 255f) * 255f);
        var g = (byte)Math.Round(FilterEffectsService.SRGBToLinear(color.Green / 255f) * 255f);
        var b = (byte)Math.Round(FilterEffectsService.SRGBToLinear(color.Blue / 255f) * 255f);
        return new SKColor(r, g, b, color.Alpha);
    }

    private static SvgColourInterpolation GetColorInterpolation(SvgElement svgElement)
    {
        return svgElement.ColorInterpolation switch
        {
            SvgColourInterpolation.Auto => SvgColourInterpolation.SRGB,
            SvgColourInterpolation.LinearRGB => SvgColourInterpolation.LinearRGB,
            _ => SvgColourInterpolation.SRGB,
        };
    }

    private static SKPathEffect? CreateDash(SvgElement svgElement, SKRect skBounds)
    {
        var strokeDashArray = svgElement.StrokeDashArray;
        var strokeDashOffset = svgElement.StrokeDashOffset;
        var count = strokeDashArray.Count;

        if (strokeDashArray is null || count <= 0)
        {
            return null;
        }

        var isOdd = count % 2 != 0;
        var sum = 0f;
        var intervals = new float[isOdd ? count * 2 : count];
        for (var i = 0; i < count; i++)
        {
            var dash = strokeDashArray[i].ToDeviceValue(UnitRenderingType.Other, svgElement, skBounds);
            if (dash < 0f)
            {
                return null;
            }

            intervals[i] = dash;
            if (isOdd)
            {
                intervals[i + count] = dash;
            }

            sum += dash;
        }

        if (sum <= 0f)
        {
            return null;
        }

        var phase = strokeDashOffset.ToDeviceValue(UnitRenderingType.Other, svgElement, skBounds);
        return SKPathEffect.CreateDash(intervals, phase);
    }

    private static void SetDash(SvgVisualElement svgVisualElement, SKPaint skPaint, SKRect skBounds)
    {
        if (CreateDash(svgVisualElement, skBounds) is { } dash)
        {
            skPaint.PathEffect = dash;
        }
    }

    private static List<SvgGradientServer> GetLinkedGradientServers(SvgGradientServer svgGradientServer, SvgVisualElement svgVisualElement)
    {
        var gradientServers = new List<SvgGradientServer>();
        var currentGradientServer = svgGradientServer;
        do
        {
            gradientServers.Add(currentGradientServer);
            currentGradientServer = SvgDeferredPaintServer.TryGet<SvgGradientServer>(currentGradientServer.InheritGradient, svgVisualElement);
        } while (currentGradientServer is not null && currentGradientServer != svgGradientServer);

        return gradientServers;
    }

    private static void GetStops(
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
            if (colors.Count != 0)
            {
                continue;
            }

            foreach (var child in svgReferencedGradientServer.Children)
            {
                if (child is not SvgGradientStop svgGradientStop)
                {
                    continue;
                }

                var server = svgGradientStop.StopColor;
                if (server is SvgDeferredPaintServer svgDeferredPaintServer)
                {
                    // Match the model-path behavior: stop-level currentColor/inherit must resolve
                    // against the gradient definition tree rather than the referencing element.
                    server = SvgDeferredPaintServer.TryGet<SvgPaintServer>(svgDeferredPaintServer, svgGradientStop);
                }

                if (server is not SvgColourServer stopColorSvgColourServer)
                {
                    continue;
                }

                var stopOpacity = AdjustSvgOpacity(svgGradientStop.StopOpacity);
                var stopColor = GetColor(stopColorSvgColourServer, opacity * stopOpacity, ignoreAttributes);
                if (isLinearRgb)
                {
                    stopColor = ToLinear(stopColor);
                }

                var offset = svgGradientStop.Offset.ToDeviceValue(UnitRenderingType.Horizontal, svgReferencedGradientServer, skBounds);
                offset /= skBounds.Width;
                colors.Add(stopColor);
                colorPos.Add(offset);
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

    private static SKColorF[] ToSkColorF(IReadOnlyList<SKColor> skColors)
    {
        var skColorsF = new SKColorF[skColors.Count];
        for (var i = 0; i < skColors.Count; i++)
        {
            skColorsF[i] = skColors[i];
        }

        return skColorsF;
    }

    private static SKShader CreateLinearGradient(
        SvgLinearGradientServer svgLinearGradientServer,
        SKRect skBounds,
        SvgVisualElement svgVisualElement,
        float opacity,
        DrawAttributes ignoreAttributes,
        SKColorSpace skColorSpace)
    {
        var svgReferencedGradientServers = GetLinkedGradientServers(svgLinearGradientServer, svgVisualElement);

        SvgGradientServer? firstSpreadMethod = null;
        SvgGradientServer? firstGradientTransform = null;
        SvgGradientServer? firstGradientUnits = null;
        SvgLinearGradientServer? firstX1 = null;
        SvgLinearGradientServer? firstY1 = null;
        SvgLinearGradientServer? firstX2 = null;
        SvgLinearGradientServer? firstY2 = null;

        foreach (var p in svgReferencedGradientServers)
        {
            if (firstSpreadMethod is null && p.SpreadMethod != SvgGradientSpreadMethod.Pad)
            {
                firstSpreadMethod = p;
            }

            if (firstGradientTransform is null && p.GradientTransform is { Count: > 0 })
            {
                firstGradientTransform = p;
            }

            if (firstGradientUnits is null && p.GradientUnits != SvgCoordinateUnits.ObjectBoundingBox)
            {
                firstGradientUnits = p;
            }

            if (p is not SvgLinearGradientServer gradientServerHref)
            {
                continue;
            }

            if (firstX1 is null && gradientServerHref.X1 != SvgUnit.None && SvgService.TryGetAttribute(gradientServerHref, "x1", out _))
            {
                firstX1 = gradientServerHref;
            }

            if (firstY1 is null && gradientServerHref.Y1 != SvgUnit.None && SvgService.TryGetAttribute(gradientServerHref, "y1", out _))
            {
                firstY1 = gradientServerHref;
            }

            var x2UnitCandidate = gradientServerHref.X2;
            if (firstX2 is null && x2UnitCandidate != SvgUnit.None && SvgService.TryGetAttribute(gradientServerHref, "x2", out _))
            {
                firstX2 = gradientServerHref;
            }

            if (firstY2 is null && gradientServerHref.Y2 != SvgUnit.None && SvgService.TryGetAttribute(gradientServerHref, "y2", out _))
            {
                firstY2 = gradientServerHref;
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

        var colors = new List<SKColor>();
        var colorPos = new List<float>();
        var isLinearRgb = skColorSpace == SKColorSpace.SrgbLinear;
        GetStops(svgReferencedGradientServers, skBounds, colors, colorPos, svgVisualElement, opacity, ignoreAttributes, isLinearRgb);
        AdjustStopColorPos(colorPos);

        var shaderTileMode = svgSpreadMethod switch
        {
            SvgGradientSpreadMethod.Reflect => SKShaderTileMode.Mirror,
            SvgGradientSpreadMethod.Repeat => SKShaderTileMode.Repeat,
            _ => SKShaderTileMode.Clamp
        };

        if (colors.Count == 0)
        {
            return SKShader.CreateColor(new SKColor(0xFF, 0xFF, 0xFF, 0x00), skColorSpace);
        }

        if (colors.Count == 1)
        {
            return SKShader.CreateColor(colors[0], skColorSpace);
        }

        var skColorsF = ToSkColorF(colors);
        var skColorPos = colorPos.ToArray();
        var skStart = new SKPoint(x1, y1);
        var skEnd = new SKPoint(x2, y2);

        if (svgGradientUnits == SvgCoordinateUnits.ObjectBoundingBox)
        {
            var skBoundingBoxTransform = new SKMatrix
            {
                ScaleX = skBounds.Width,
                ScaleY = skBounds.Height,
                TransX = skBounds.Left,
                TransY = skBounds.Top,
                Persp2 = 1
            };

            if (svgGradientTransform is { Count: > 0 })
            {
                skBoundingBoxTransform = skBoundingBoxTransform.PreConcat(TransformsService.ToMatrix(svgGradientTransform));
            }

            return SKShader.CreateLinearGradient(skStart, skEnd, skColorsF, skColorSpace, skColorPos, shaderTileMode, skBoundingBoxTransform);
        }

        if (svgGradientTransform is { Count: > 0 })
        {
            return SKShader.CreateLinearGradient(skStart, skEnd, skColorsF, skColorSpace, skColorPos, shaderTileMode, TransformsService.ToMatrix(svgGradientTransform));
        }

        return SKShader.CreateLinearGradient(skStart, skEnd, skColorsF, skColorSpace, skColorPos, shaderTileMode);
    }

    private static SKShader CreateTwoPointConicalGradient(
        SvgRadialGradientServer svgRadialGradientServer,
        SKRect skBounds,
        SvgVisualElement svgVisualElement,
        float opacity,
        DrawAttributes ignoreAttributes,
        SKColorSpace skColorSpace)
    {
        var svgReferencedGradientServers = GetLinkedGradientServers(svgRadialGradientServer, svgVisualElement);

        SvgGradientServer? firstSpreadMethod = null;
        SvgGradientServer? firstGradientTransform = null;
        SvgGradientServer? firstGradientUnits = null;
        SvgRadialGradientServer? firstCenterX = null;
        SvgRadialGradientServer? firstCenterY = null;
        SvgRadialGradientServer? firstRadius = null;
        SvgRadialGradientServer? firstFocalX = null;
        SvgRadialGradientServer? firstFocalY = null;

        foreach (var p in svgReferencedGradientServers)
        {
            if (firstSpreadMethod is null && p.SpreadMethod != SvgGradientSpreadMethod.Pad)
            {
                firstSpreadMethod = p;
            }

            if (firstGradientTransform is null && p.GradientTransform is { Count: > 0 })
            {
                firstGradientTransform = p;
            }

            if (firstGradientUnits is null && p.GradientUnits != SvgCoordinateUnits.ObjectBoundingBox)
            {
                firstGradientUnits = p;
            }

            if (p is not SvgRadialGradientServer gradientServerHref)
            {
                continue;
            }

            if (firstCenterX is null && gradientServerHref.CenterX != SvgUnit.None && SvgService.TryGetAttribute(gradientServerHref, "cx", out _))
            {
                firstCenterX = gradientServerHref;
            }

            if (firstCenterY is null && gradientServerHref.CenterY != SvgUnit.None && SvgService.TryGetAttribute(gradientServerHref, "cy", out _))
            {
                firstCenterY = gradientServerHref;
            }

            if (firstRadius is null && gradientServerHref.Radius != SvgUnit.None && SvgService.TryGetAttribute(gradientServerHref, "r", out _))
            {
                firstRadius = gradientServerHref;
            }

            if (firstFocalX is null && gradientServerHref.FocalX != SvgUnit.None && SvgService.TryGetAttribute(gradientServerHref, "fx", out _))
            {
                firstFocalX = gradientServerHref;
            }

            if (firstFocalY is null && gradientServerHref.FocalY != SvgUnit.None && SvgService.TryGetAttribute(gradientServerHref, "fy", out _))
            {
                firstFocalY = gradientServerHref;
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

        var centerX = centerXUnit.Normalize(svgGradientUnits).ToDeviceValue(UnitRenderingType.Horizontal, svgRadialGradientServer, skBounds);
        var centerY = centerYUnit.Normalize(svgGradientUnits).ToDeviceValue(UnitRenderingType.Vertical, svgRadialGradientServer, skBounds);
        var radius = radiusUnit.Normalize(svgGradientUnits).ToDeviceValue(UnitRenderingType.Other, svgRadialGradientServer, skBounds);
        var focalX = focalXUnit.Normalize(svgGradientUnits).ToDeviceValue(UnitRenderingType.Horizontal, svgRadialGradientServer, skBounds);
        var focalY = focalYUnit.Normalize(svgGradientUnits).ToDeviceValue(UnitRenderingType.Vertical, svgRadialGradientServer, skBounds);

        var colors = new List<SKColor>();
        var colorPos = new List<float>();
        var isLinearRgb = skColorSpace == SKColorSpace.SrgbLinear;
        GetStops(svgReferencedGradientServers, skBounds, colors, colorPos, svgVisualElement, opacity, ignoreAttributes, isLinearRgb);
        AdjustStopColorPos(colorPos);

        var shaderTileMode = svgSpreadMethod switch
        {
            SvgGradientSpreadMethod.Reflect => SKShaderTileMode.Mirror,
            SvgGradientSpreadMethod.Repeat => SKShaderTileMode.Repeat,
            _ => SKShaderTileMode.Clamp
        };

        if (colors.Count == 0)
        {
            return SKShader.CreateColor(new SKColor(0xFF, 0xFF, 0xFF, 0x00), skColorSpace);
        }

        if (colors.Count == 1)
        {
            return SKShader.CreateColor(colors[0], skColorSpace);
        }

        if (radius == 0f)
        {
            return SKShader.CreateColor(colors[colors.Count - 1], skColorSpace);
        }

        var skColorsF = ToSkColorF(colors);
        var skColorPos = colorPos.ToArray();
        var skCenter = new SKPoint(centerX, centerY);
        var skFocal = new SKPoint(focalX, focalY);
        var isRadialGradient = skCenter.X == skFocal.X && skCenter.Y == skFocal.Y;

        if (svgGradientUnits == SvgCoordinateUnits.ObjectBoundingBox)
        {
            var skBoundingBoxTransform = new SKMatrix
            {
                ScaleX = skBounds.Width,
                ScaleY = skBounds.Height,
                TransX = skBounds.Left,
                TransY = skBounds.Top,
                Persp2 = 1
            };

            if (svgGradientTransform is { Count: > 0 })
            {
                skBoundingBoxTransform = skBoundingBoxTransform.PreConcat(TransformsService.ToMatrix(svgGradientTransform));
            }

            return isRadialGradient
                ? SKShader.CreateRadialGradient(skCenter, radius, skColorsF, skColorSpace, skColorPos, shaderTileMode, skBoundingBoxTransform)
                : SKShader.CreateTwoPointConicalGradient(skFocal, 0f, skCenter, radius, skColorsF, skColorSpace, skColorPos, shaderTileMode, skBoundingBoxTransform);
        }

        if (svgGradientTransform is { Count: > 0 })
        {
            var gradientTransform = TransformsService.ToMatrix(svgGradientTransform);
            return isRadialGradient
                ? SKShader.CreateRadialGradient(skCenter, radius, skColorsF, skColorSpace, skColorPos, shaderTileMode, gradientTransform)
                : SKShader.CreateTwoPointConicalGradient(skFocal, 0f, skCenter, radius, skColorsF, skColorSpace, skColorPos, shaderTileMode, gradientTransform);
        }

        return isRadialGradient
            ? SKShader.CreateRadialGradient(skCenter, radius, skColorsF, skColorSpace, skColorPos, shaderTileMode)
            : SKShader.CreateTwoPointConicalGradient(skFocal, 0f, skCenter, radius, skColorsF, skColorSpace, skColorPos, shaderTileMode);
    }

    private static SKShader? CreatePatternShader(
        SvgPatternServer svgPatternServer,
        SKRect skBounds,
        SvgVisualElement svgVisualElement,
        float opacity,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes)
    {
        if (!SvgPatternPaintStateResolver.TryCreate(svgPatternServer, svgVisualElement, skBounds, out var patternState) ||
            patternState is null)
        {
            return null;
        }
        var patternScene = SvgSceneCompiler.CompileTemporaryChildrenScene(
            patternState.ContentSource,
            patternState.Children,
            patternState.PictureViewport,
            patternState.PictureViewport,
            patternState.PictureTransform,
            opacity,
            assetLoader,
            ignoreAttributes);
        if (patternScene is null)
        {
            return null;
        }

        var picture = SvgSceneRenderer.Render(patternScene);
        return picture is null
            ? null
            : SKShader.CreatePicture(picture, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, patternState.ShaderMatrix, picture.CullRect);
    }
}
