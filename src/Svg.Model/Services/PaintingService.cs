using System;
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg.DataTypes;
using Svg.Model.Drawables;
using Svg.Model.Drawables.Factories;

namespace Svg.Model.Services;

internal static class PaintingService
{
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

    internal static SKPathEffect? CreateDash(SvgElement svgElement, SKRect skBounds)
    {
        var strokeDashArray = svgElement.StrokeDashArray;
        var strokeDashOffset = svgElement.StrokeDashOffset;
        var count = strokeDashArray.Count;

        if (strokeDashArray is { } && count > 0)
        {
            var isOdd = count % 2 != 0;
            var sum = 0f;
            float[] intervals = new float[isOdd ? count * 2 : count];
            for (var i = 0; i < count; i++)
            {
                var dash = strokeDashArray[i].ToDeviceValue(UnitRenderingType.Other, svgElement, skBounds);
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

            var phase = strokeDashOffset.ToDeviceValue(UnitRenderingType.Other, svgElement, skBounds);

            return SKPathEffect.CreateDash(intervals, phase);
        }

        return default;
    }

    private static List<SvgPatternServer> GetLinkedPatternServer(SvgPatternServer svgPatternServer, SvgVisualElement svgVisualElement)
    {
        var svgPatternServers = new List<SvgPatternServer>();
        var currentPatternServer = svgPatternServer;
        do
        {
            svgPatternServers.Add(currentPatternServer);
            currentPatternServer = SvgDeferredPaintServer.TryGet<SvgPatternServer>(currentPatternServer.InheritGradient, svgVisualElement);
        } while (currentPatternServer is { } && currentPatternServer != svgPatternServer);
        return svgPatternServers;
    }

    private static List<SvgGradientServer> GetLinkedGradientServer(SvgGradientServer svgGradientServer, SvgVisualElement svgVisualElement)
    {
        var svgGradientServers = new List<SvgGradientServer>();
        var currentGradientServer = svgGradientServer;
        do
        {
            svgGradientServers.Add(currentGradientServer);
            currentGradientServer = SvgDeferredPaintServer.TryGet<SvgGradientServer>(currentGradientServer.InheritGradient, svgVisualElement);
        } while (currentGradientServer is { } && currentGradientServer != svgGradientServer);
        return svgGradientServers;
    }

    private static void GetStopsImpl(SvgGradientServer svgGradientServer, SKRect skBounds, List<SKColor> colors, List<float> colorPos, SvgVisualElement svgVisualElement, float opacity, DrawAttributes ignoreAttributes)
    {
        foreach (var child in svgGradientServer.Children)
        {
            if (child is SvgGradientStop svgGradientStop)
            {
                var server = svgGradientStop.StopColor;
                if (server is SvgDeferredPaintServer svgDeferredPaintServer)
                {
                    server = SvgDeferredPaintServer.TryGet<SvgPaintServer>(svgDeferredPaintServer, svgVisualElement);
                    if (server is null)
                    {
                        // TODO: server is sometimes null with currentColor
                    }
                }

                if (server is SvgColourServer stopColorSvgColourServer)
                {
                    var stopOpacity = AdjustSvgOpacity(svgGradientStop.StopOpacity);
                    var stopColor = GetColor(stopColorSvgColourServer, opacity * stopOpacity, ignoreAttributes);
                    var offset = svgGradientStop.Offset.ToDeviceValue(UnitRenderingType.Horizontal, svgGradientServer, skBounds);
                    offset /= skBounds.Width;
                    colors.Add(stopColor);
                    colorPos.Add(offset);
                }
            }
        }
    }

    internal static void GetStops(List<SvgGradientServer> svgReferencedGradientServers, SKRect skBounds, List<SKColor> colors, List<float> colorPos, SvgVisualElement svgVisualElement, float opacity, DrawAttributes ignoreAttributes)
    {
        foreach (var svgReferencedGradientServer in svgReferencedGradientServers)
        {
            if (colors.Count == 0)
            {
                GetStopsImpl(svgReferencedGradientServer, skBounds, colors, colorPos, svgVisualElement, opacity, ignoreAttributes);
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

    internal static SKColorF[] ToSkColorF(this SKColor[] skColors)
    {
        var skColorsF = new SKColorF[skColors.Length];

        for (var i = 0; i < skColors.Length; i++)
        {
            skColorsF[i] = skColors[i];
        }

        return skColorsF;
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
                var pSpreadMethod = p.SpreadMethod;
                if (pSpreadMethod != SvgGradientSpreadMethod.Pad)
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
                var pGradientUnits = p.GradientUnits;
                if (pGradientUnits != SvgCoordinateUnits.ObjectBoundingBox)
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

        var skStart = new SKPoint(x1, y1);
        var skEnd = new SKPoint(x2, y2);
        var colors = new List<SKColor>();
        var colorPos = new List<float>();

        GetStops(svgReferencedGradientServers, skBounds, colors, colorPos, svgVisualElement, opacity, ignoreAttributes);
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

        foreach (var p in svgReferencedGradientServers)
        {
            if (firstSpreadMethod is null)
            {
                var pSpreadMethod = p.SpreadMethod;
                if (pSpreadMethod != SvgGradientSpreadMethod.Pad)
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
                var pGradientUnits = p.GradientUnits;
                if (pGradientUnits != SvgCoordinateUnits.ObjectBoundingBox)
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

        var normalizedCenterX = centerXUnit.Normalize(svgGradientUnits);
        var normalizedCenterY = centerYUnit.Normalize(svgGradientUnits);
        var normalizedRadius = radiusUnit.Normalize(svgGradientUnits);
        var normalizedFocalX = focalXUnit.Normalize(svgGradientUnits);
        var normalizedFocalY = focalYUnit.Normalize(svgGradientUnits);

        var centerX = normalizedCenterX.ToDeviceValue(UnitRenderingType.Horizontal, svgRadialGradientServer, skBounds);
        var centerY = normalizedCenterY.ToDeviceValue(UnitRenderingType.Vertical, svgRadialGradientServer, skBounds);

        var radius = normalizedRadius.ToDeviceValue(UnitRenderingType.Other, svgRadialGradientServer, skBounds);

        var focalX = normalizedFocalX.ToDeviceValue(UnitRenderingType.Horizontal, svgRadialGradientServer, skBounds);
        var focalY = normalizedFocalY.ToDeviceValue(UnitRenderingType.Vertical, svgRadialGradientServer, skBounds);

        var skCenter = new SKPoint(centerX, centerY);
        var skFocal = new SKPoint(focalX, focalY);

        var colors = new List<SKColor>();
        var colorPos = new List<float>();

        GetStops(svgReferencedGradientServers, skBounds, colors, colorPos, svgVisualElement, opacity, ignoreAttributes);
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

        var isRadialGradient = skCenter.X == skFocal.X && skCenter.Y == skFocal.Y;
        
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
                    skFocal, 0,
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
                        skFocal, 0,
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
                        skFocal, 0,
                        skCenter, radius,
                        skColorsF, skColorSpace, skColorPos,
                        shaderTileMode);
                }
            }
        }
    }

    internal static SKPicture RecordPicture(SvgElementCollection svgElementCollection, float width, float height, SKMatrix skMatrix, float opacity, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes)
    {
        var skSize = new SKSize(width, height);
        var skBounds = SKRect.Create(skSize);
        var skPictureRecorder = new SKPictureRecorder();
        var skCanvas = skPictureRecorder.BeginRecording(skBounds);

        skCanvas.SetMatrix(skMatrix);

        var skPaintOpacity = ignoreAttributes.HasFlag(DrawAttributes.Opacity) ? null : GetOpacityPaint(opacity);
        if (skPaintOpacity is { })
        {
            skCanvas.SaveLayer(skPaintOpacity);
        }

        var drawables = new List<DrawableBase>();
        
        foreach (var svgElement in svgElementCollection)
        {
            var drawable = DrawableFactory.Create(svgElement, skBounds, null, assetLoader, references, ignoreAttributes);
            if (drawable is { })
            {
                drawables.Add(drawable);
            }
        }

        foreach (var drawable in drawables)
        {
            drawable.PostProcess(skBounds, skMatrix);
        }

        foreach (var drawable in drawables)
        {
            drawable.Draw(skCanvas, ignoreAttributes, null, true);
        }

        if (skPaintOpacity is { })
        {
            skCanvas.Restore();
        }

        skCanvas.Restore();

        return skPictureRecorder.EndRecording();
    }

    internal static SKShader? CreatePicture(SvgPatternServer svgPatternServer, SKRect skBounds, SvgVisualElement svgVisualElement, float opacity, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes)
    {
        var svgReferencedPatternServers = GetLinkedPatternServer(svgPatternServer, svgVisualElement);

        SvgPatternServer? firstChildren = default;
        SvgPatternServer? firstX = default;
        SvgPatternServer? firstY = default;
        SvgPatternServer? firstWidth = default;
        SvgPatternServer? firstHeight = default;
        SvgPatternServer? firstPatternUnit = default;
        SvgPatternServer? firstPatternContentUnit = default;
        SvgPatternServer? firstViewBox = default;
        SvgPatternServer? firstAspectRatio = default;

        foreach (var p in svgReferencedPatternServers)
        {
            if (firstChildren is null && p.Children.Count > 0)
            {
                firstChildren = p;
            }

            if (firstX is null)
            {
                var pX = p.X;
                if (pX != SvgUnit.None)
                {
                    firstX = p;
                }
            }
            if (firstY is null)
            {
                var pY = p.Y;
                if (pY != SvgUnit.None)
                {
                    firstY = p;
                }
            }
            if (firstWidth is null)
            {
                var pWidth = p.Width;
                if (pWidth != SvgUnit.None)
                {
                    firstWidth = p;
                }
            }
            if (firstHeight is null)
            {
                var pHeight = p.Height;
                if (pHeight != SvgUnit.None)
                {
                    firstHeight = p;
                }
            }
            if (firstPatternUnit is null)
            {
                if (SvgService.TryGetAttribute(p, "patternUnits", out _))
                {
                    firstPatternUnit = p;
                }
            }
            if (firstPatternContentUnit is null)
            {
                if (SvgService.TryGetAttribute(p, "patternContentUnits", out _))
                {
                    firstPatternContentUnit = p;
                }
            }
            if (firstViewBox is null)
            {
                var pViewBox = p.ViewBox;
                if (pViewBox != SvgViewBox.Empty)
                {
                    firstViewBox = p;
                }
            }
            if (firstAspectRatio is null)
            {
                var pAspectRatio = p.AspectRatio;
                // TODO: We don't reference Defer elsewhere. Probably something to be implemented.
                if (pAspectRatio.Align != SvgPreserveAspectRatio.xMidYMid || pAspectRatio.Slice || pAspectRatio.Defer)
                {
                    firstAspectRatio = p;
                }
            }
        }

        if (firstChildren is null || firstWidth is null || firstHeight is null)
        {
            return default;
        }

        var xUnit = firstX?.X ?? new SvgUnit(0f);
        var yUnit = firstY?.Y ?? new SvgUnit(0f);
        var widthUnit = firstWidth.Width;
        var heightUnit = firstHeight.Height;
        var patternUnits = firstPatternUnit?.PatternUnits ?? SvgCoordinateUnits.ObjectBoundingBox;
        var patternContentUnits = firstPatternContentUnit?.PatternContentUnits ?? SvgCoordinateUnits.UserSpaceOnUse;
        var viewBox = firstViewBox?.ViewBox ?? SvgViewBox.Empty;
        var aspectRatio = firstAspectRatio is null ? new SvgAspectRatio(SvgPreserveAspectRatio.xMidYMid, false) : firstAspectRatio.AspectRatio;

        // TODO: Pass correct skViewport
        var skRectTransformed = TransformsService.CalculateRect(xUnit, yUnit, widthUnit, heightUnit, patternUnits, skBounds, skBounds, svgPatternServer);
        if (skRectTransformed is null)
        {
            return default;
        }

        var skMatrix = SKMatrix.CreateIdentity();

        var skPatternTransformMatrix = TransformsService.ToMatrix(svgPatternServer.PatternTransform);
        skMatrix = skMatrix.PreConcat(skPatternTransformMatrix);

        var translateTransform = SKMatrix.CreateTranslation(skRectTransformed.Value.Left, skRectTransformed.Value.Top);
        skMatrix = skMatrix.PreConcat(translateTransform);

        var skPictureTransform = SKMatrix.CreateIdentity();
        if (!viewBox.Equals(SvgViewBox.Empty))
        {
            var viewBoxTransform = TransformsService.ToMatrix(
                viewBox,
                aspectRatio,
                0f,
                0f,
                skRectTransformed.Value.Width,
                skRectTransformed.Value.Height);
            skPictureTransform = skPictureTransform.PreConcat(viewBoxTransform);
        }
        else
        {
            if (patternContentUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                var skBoundsScaleTransform = SKMatrix.CreateScale(skBounds.Width, skBounds.Height);
                skPictureTransform = skPictureTransform.PreConcat(skBoundsScaleTransform);
            }
        }

        var skPicture = RecordPicture(firstChildren.Children, skRectTransformed.Value.Width, skRectTransformed.Value.Height, skPictureTransform, opacity, assetLoader, references, ignoreAttributes);

        return SKShader.CreatePicture(skPicture, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, skMatrix, skPicture.CullRect);
    }

    internal static bool SetColorOrShader(SvgVisualElement svgVisualElement, SvgPaintServer server, float opacity, SKRect skBounds, SKPaint skPaint, bool forStroke, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes)
    {
        var fallbackServer = SvgPaintServer.None;
        if (server is SvgDeferredPaintServer deferredServer)
        {
            server = SvgDeferredPaintServer.TryGet<SvgPaintServer>(deferredServer, svgVisualElement);
            fallbackServer = deferredServer.FallbackServer;
            if (server is null)
            {
                server = fallbackServer;
                if (server is null)
                {
                    server = SvgPaintServer.NotSet;
                    fallbackServer = null;
                }
            }
        }

        if (server == SvgPaintServer.None)
        {
            return false;
        }

        switch (server)
        {
            case SvgColourServer svgColourServer:
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
                    var skColorShader = SKShader.CreateColor(skColor, skColorSpace);
                    if (skColorShader is { })
                    {
                        skPaint.Shader = skColorShader;
                        return true;
                    }
                }
                break;

            case SvgPatternServer svgPatternServer:
                {
                    var colorInterpolation = GetColorInterpolation(svgVisualElement);
                    var isLinearRgb = colorInterpolation == SvgColourInterpolation.LinearRGB;
                    var skColorSpace = isLinearRgb ? SKColorSpace.SrgbLinear : SKColorSpace.Srgb;
                    // TODO: Use skColorSpace in CreatePicture
                    var skPatternShader = CreatePicture(svgPatternServer, skBounds, svgVisualElement, opacity, assetLoader, references, ignoreAttributes);
                    if (skPatternShader is { })
                    {
                        skPaint.Shader = skPatternShader;
                        return true;
                    }
                    else
                    {
                        if (fallbackServer is SvgColourServer svgColourServerFallback)
                        {
                            var skColor = GetColor(svgColourServerFallback, opacity, ignoreAttributes);
                            
                            if (skColorSpace == SKColorSpace.Srgb)
                            {
                                skPaint.Color = skColor;
                                skPaint.Shader = null;
                                return true;
                            }

                            var skColorShader = SKShader.CreateColor(skColor, skColorSpace);
                            if (skColorShader is { })
                            {
                                skPaint.Shader = skColorShader;
                                return true;
                            }
                        }
                        else
                        {
                            // Do not draw element.
                            return false;
                        }
                    }
                }
                break;

            case SvgLinearGradientServer svgLinearGradientServer:
                {
                    var colorInterpolation = GetColorInterpolation(svgLinearGradientServer);
                    var isLinearRgb = colorInterpolation == SvgColourInterpolation.LinearRGB;
                    var skColorSpace = isLinearRgb ? SKColorSpace.SrgbLinear : SKColorSpace.Srgb;

                    if (svgLinearGradientServer.GradientUnits == SvgCoordinateUnits.ObjectBoundingBox && (skBounds.Width == 0f || skBounds.Height == 0f))
                    {
                        if (fallbackServer is SvgColourServer svgColourServerFallback)
                        {
                            var skColor = GetColor(svgColourServerFallback, opacity, ignoreAttributes);

                            if (skColorSpace == SKColorSpace.Srgb)
                            {
                                skPaint.Color = skColor;
                                skPaint.Shader = null;
                                return true;
                            }

                            var skColorShader = SKShader.CreateColor(skColor, skColorSpace);
                            if (skColorShader is { })
                            {
                                skPaint.Shader = skColorShader;
                                return true;
                            }
                        }
                        else
                        {
                            // Do not draw element.
                            return false;
                        }
                    }
                    else
                    {
                        var skLinearGradientShader = CreateLinearGradient(svgLinearGradientServer, skBounds, svgVisualElement, opacity, ignoreAttributes, skColorSpace);
                        if (skLinearGradientShader is { })
                        {
                            skPaint.Shader = skLinearGradientShader;
                            return true;
                        }
                        else
                        {
                            // Do not draw element.
                            return false;
                        }
                    }
                }
                break;

            case SvgRadialGradientServer svgRadialGradientServer:
                {
                    var colorInterpolation = GetColorInterpolation(svgRadialGradientServer);
                    var isLinearRgb = colorInterpolation == SvgColourInterpolation.LinearRGB;
                    var skColorSpace = isLinearRgb ? SKColorSpace.SrgbLinear : SKColorSpace.Srgb;

                    if (svgRadialGradientServer.GradientUnits == SvgCoordinateUnits.ObjectBoundingBox && (skBounds.Width == 0f || skBounds.Height == 0f))
                    {
                        if (fallbackServer is SvgColourServer svgColourServerFallback)
                        {
                            var skColor = GetColor(svgColourServerFallback, opacity, ignoreAttributes);

                            if (skColorSpace == SKColorSpace.Srgb)
                            {
                                skPaint.Color = skColor;
                                skPaint.Shader = null;
                                return true;
                            }
 
                            var skColorShader = SKShader.CreateColor(skColor, skColorSpace);
                            if (skColorShader is { })
                            {
                                skPaint.Shader = skColorShader;
                                return true;
                            }
                        }
                        else
                        {
                            // Do not draw element.
                            return false;
                        }
                    }
                    else
                    {
                        var skRadialGradientShader = CreateTwoPointConicalGradient(svgRadialGradientServer, skBounds, svgVisualElement, opacity, ignoreAttributes, skColorSpace);
                        if (skRadialGradientShader is { })
                        {
                            skPaint.Shader = skRadialGradientShader;
                            return true;
                        }
                        else
                        {
                            // Do not draw element.
                            return false;
                        }
                    }
                }
                break;

            case SvgDeferredPaintServer svgDeferredPaintServer:
                return SetColorOrShader(svgVisualElement, svgDeferredPaintServer, opacity, skBounds, skPaint, forStroke, assetLoader, references, ignoreAttributes);

            default:
                // Do not draw element.
                return false;
        }
        return true;
    }

    internal static void SetDash(SvgVisualElement svgVisualElement, SKPaint skPaint, SKRect skBounds)
    {
        var skPathEffect = CreateDash(svgVisualElement, skBounds);
        if (skPathEffect is { })
        {
            skPaint.PathEffect = skPathEffect;
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

    internal static SKPaint? GetFillPaint(SvgVisualElement svgVisualElement, SKRect skBounds, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes)
    {
        var skPaint = new SKPaint
        {
            IsAntialias = IsAntialias(svgVisualElement),
            Style = SKPaintStyle.Fill
        };

        var server = svgVisualElement.Fill;
        var opacity = AdjustSvgOpacity(svgVisualElement.FillOpacity);
        if (SetColorOrShader(svgVisualElement, server, opacity, skBounds, skPaint, forStroke: false, assetLoader: assetLoader, references, ignoreAttributes: ignoreAttributes) == false)
        {
            return default;
        }

        return skPaint;
    }

    internal static SKPaint? GetStrokePaint(SvgVisualElement svgVisualElement, SKRect skBounds, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes)
    {
        var skPaint = new SKPaint
        {
            IsAntialias = IsAntialias(svgVisualElement),
            Style = SKPaintStyle.Stroke
        };

        var server = svgVisualElement.Stroke;
        var opacity = AdjustSvgOpacity(svgVisualElement.StrokeOpacity);
        if (SetColorOrShader(svgVisualElement, server, opacity, skBounds, skPaint, forStroke: true, assetLoader: assetLoader, references, ignoreAttributes: ignoreAttributes) == false)
        {
            return default;
        }

        switch (svgVisualElement.StrokeLineCap)
        {
            case SvgStrokeLineCap.Butt:
                skPaint.StrokeCap = SKStrokeCap.Butt;
                break;

            case SvgStrokeLineCap.Round:
                skPaint.StrokeCap = SKStrokeCap.Round;
                break;

            case SvgStrokeLineCap.Square:
                skPaint.StrokeCap = SKStrokeCap.Square;
                break;
        }

        switch (svgVisualElement.StrokeLineJoin)
        {
            case SvgStrokeLineJoin.Miter:
                skPaint.StrokeJoin = SKStrokeJoin.Miter;
                break;

            case SvgStrokeLineJoin.Round:
                skPaint.StrokeJoin = SKStrokeJoin.Round;
                break;

            case SvgStrokeLineJoin.Bevel:
                skPaint.StrokeJoin = SKStrokeJoin.Bevel;
                break;
        }

        skPaint.StrokeMiter = svgVisualElement.StrokeMiterLimit;

        skPaint.StrokeWidth = svgVisualElement.StrokeWidth.ToDeviceValue(UnitRenderingType.Other, svgVisualElement, skBounds);

        var strokeDashArray = svgVisualElement.StrokeDashArray;
        if (strokeDashArray is { })
        {
            SetDash(svgVisualElement, skPaint, skBounds);
        }

        return skPaint;
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

    internal static SKPaint? GetOpacityPaint(SvgElement svgElement)
    {
        var opacity = AdjustSvgOpacity(svgElement.Opacity);
        var skPaint = GetOpacityPaint(opacity);
        if (skPaint is { })
        {
            return skPaint;
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

    internal static SKTextAlign ToTextAlign(SvgTextAnchor textAnchor)
    {
        return textAnchor switch
        {
            SvgTextAnchor.Middle => SKTextAlign.Center,
            SvgTextAnchor.End => SKTextAlign.Right,
            _ => SKTextAlign.Left,
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
        var fontWeight = ToFontStyleWeight(svgText.FontWeight);
        var fontWidth = ToFontStyleWidth(svgText.FontStretch);
        var fontStyle = ToFontStyleSlant(svgText.FontStyle);
        skPaint.Typeface = SKTypeface.FromFamilyName(fontFamily, fontWeight, fontWidth, fontStyle);
    }

    internal static void SetPaintText(SvgTextBase svgText, SKRect skBounds, SKPaint skPaint)
    {
        skPaint.LcdRenderText = true;
        skPaint.SubpixelText = true;
        skPaint.TextEncoding = SKTextEncoding.Utf16;

        skPaint.TextAlign = ToTextAlign(svgText.TextAnchor);

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

        float fontSize;
        var fontSizeUnit = svgText.FontSize;
        if (fontSizeUnit == SvgUnit.None || fontSizeUnit == SvgUnit.Empty)
        {
            // TODO: Do not use implicit float conversion from SvgUnit.ToDeviceValue
            // fontSize = new SvgUnit(SvgUnitType.Em, 1.0f);
            // NOTE: Use default SkPaint Font_Size
            fontSize = 12f;
        }
        else
        {
            fontSize = fontSizeUnit.ToDeviceValue(UnitRenderingType.Vertical, svgText, skBounds);
        }

        skPaint.TextSize = fontSize;

        SetTypeface(svgText, skPaint);
    }
}
