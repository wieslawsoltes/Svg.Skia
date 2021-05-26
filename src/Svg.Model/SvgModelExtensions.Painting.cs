using System;
using System.Collections.Generic;
using Svg.DataTypes;
using Svg.Model.Drawables;
using Svg.Model.Painting;
using Svg.Model.Painting.Shaders;
using Svg.Model.Primitives;

namespace Svg.Model
{
    public static partial class SvgModelExtensions
    {
        internal static float AdjustSvgOpacity(float opacity)
        {
            return Math.Min(Math.Max(opacity, 0), 1);
        }

        internal static byte CombineWithOpacity(byte alpha, float opacity)
        {
            return (byte)Math.Round(opacity * (alpha / 255.0) * 255);
        }

        internal static Color GetColor(SvgColourServer svgColourServer, float opacity, Attributes ignoreAttributes)
        {
            var colour = svgColourServer.Colour;
            var alpha = ignoreAttributes.HasFlag(Attributes.Opacity) ?
                svgColourServer.Colour.A :
                CombineWithOpacity(svgColourServer.Colour.A, opacity);

            return new Color(colour.R, colour.G, colour.B, alpha);
        }

        internal static Color? GetColor(SvgVisualElement svgVisualElement, SvgPaintServer server)
        {
            if (server is SvgDeferredPaintServer svgDeferredPaintServer)
            {
                server = SvgDeferredPaintServer.TryGet<SvgPaintServer>(svgDeferredPaintServer, svgVisualElement);
            }

            if (server is SvgColourServer stopColorSvgColourServer)
            {
                return GetColor(stopColorSvgColourServer, 1f, Attributes.None);
            }

            return new Color(0x00, 0x00, 0x00, 0xFF);
        }

        internal static PathEffect? CreateDash(SvgElement svgElement, Rect skBounds)
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

                return PathEffect.CreateDash(intervals, phase);
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
            } while (currentPatternServer is { });
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
            } while (currentGradientServer is { });
            return svgGradientServers;
        }

        private static void GetStopsImpl(SvgGradientServer svgGradientServer, Rect skBounds, List<Color> colors, List<float> colorPos, SvgVisualElement svgVisualElement, float opacity, Attributes ignoreAttributes)
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

        internal static void GetStops(List<SvgGradientServer> svgReferencedGradientServers, Rect skBounds, List<Color> colors, List<float> colorPos, SvgVisualElement svgVisualElement, float opacity, Attributes ignoreAttributes)
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

        internal static ColorF[] ToSkColorF(this Color[] skColors)
        {
            var skColorsF = new ColorF[skColors.Length];

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

        internal static SvgColourInterpolation GetColorInterpolationFilters(SvgElement svgElement)
        {
            return svgElement.ColorInterpolationFilters switch
            {
                SvgColourInterpolation.Auto => SvgColourInterpolation.LinearRGB,
                SvgColourInterpolation.SRGB => SvgColourInterpolation.SRGB,
                SvgColourInterpolation.LinearRGB => SvgColourInterpolation.LinearRGB,
                _ => SvgColourInterpolation.LinearRGB,
            };
        }

        internal static Shader CreateLinearGradient(SvgLinearGradientServer svgLinearGradientServer, Rect skBounds, SvgVisualElement svgVisualElement, float opacity, Attributes ignoreAttributes, ColorSpace skColorSpace)
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
                        if (pX1 != SvgUnit.None && TryGetAttribute(svgLinearGradientServerHref, "x1", out _))
                        {
                            firstX1 = svgLinearGradientServerHref;
                        }
                    }
                    if (firstY1 is null)
                    {
                        var pY1 = svgLinearGradientServerHref.Y1;
                        if (pY1 != SvgUnit.None && TryGetAttribute(svgLinearGradientServerHref, "y1", out _))
                        {
                            firstY1 = svgLinearGradientServerHref;
                        }
                    }
                    if (firstX2 is null)
                    {
                        if (svgLinearGradientServerHref.X2 is { } pX2 && pX2 != SvgUnit.None && TryGetAttribute(svgLinearGradientServerHref, "x2", out _))
                        {
                            firstX2 = svgLinearGradientServerHref;
                        }
                    }
                    if (firstY2 is null)
                    {
                        var pY2 = svgLinearGradientServerHref.Y2;
                        if (pY2 != SvgUnit.None && TryGetAttribute(svgLinearGradientServerHref, "y2", out _))
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

            var skStart = new Point(x1, y1);
            var skEnd = new Point(x2, y2);
            var colors = new List<Color>();
            var colorPos = new List<float>();

            GetStops(svgReferencedGradientServers, skBounds, colors, colorPos, svgVisualElement, opacity, ignoreAttributes);
            AdjustStopColorPos(colorPos);

            var shaderTileMode = svgSpreadMethod switch
            {
                SvgGradientSpreadMethod.Reflect => ShaderTileMode.Mirror,
                SvgGradientSpreadMethod.Repeat => ShaderTileMode.Repeat,
                _ => ShaderTileMode.Clamp,
            };
            var skColors = colors.ToArray();
            float[] skColorPos = colorPos.ToArray();

            if (skColors.Length == 0)
            {
                return Shader.CreateColor(new Color(0xFF, 0xFF, 0xFF, 0x00), skColorSpace);
            }
            else if (skColors.Length == 1)
            {
                return Shader.CreateColor(skColors[0], skColorSpace);
            }

            if (svgGradientUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                var skBoundingBoxTransform = new Matrix
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
                    var gradientTransform = ToMatrix(svgGradientTransform);
                    skBoundingBoxTransform = skBoundingBoxTransform.PreConcat(gradientTransform);
                }

                var skColorsF = ToSkColorF(skColors);
                return Shader.CreateLinearGradient(skStart, skEnd, skColorsF, skColorSpace, skColorPos, shaderTileMode, skBoundingBoxTransform);
            }
            else
            {
                if (svgGradientTransform is { } && svgGradientTransform.Count > 0)
                {
                    var gradientTransform = ToMatrix(svgGradientTransform);
                    var skColorsF = ToSkColorF(skColors);
                    return Shader.CreateLinearGradient(skStart, skEnd, skColorsF, skColorSpace, skColorPos, shaderTileMode, gradientTransform);
                }
                else
                {
                    var skColorsF = ToSkColorF(skColors);
                    return Shader.CreateLinearGradient(skStart, skEnd, skColorsF, skColorSpace, skColorPos, shaderTileMode);
                }
            }
        }

        internal static Shader CreateTwoPointConicalGradient(SvgRadialGradientServer svgRadialGradientServer, Rect skBounds, SvgVisualElement svgVisualElement, float opacity, Attributes ignoreAttributes, ColorSpace skColorSpace)
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
                        if (pCenterX != SvgUnit.None && TryGetAttribute(svgRadialGradientServerHref, "cx", out _))
                        {
                            firstCenterX = svgRadialGradientServerHref;
                        }
                    }
                    if (firstCenterY is null)
                    {
                        if (svgRadialGradientServerHref.CenterY is { } pCenterY && pCenterY != SvgUnit.None && TryGetAttribute(svgRadialGradientServerHref, "cy", out _))
                        {
                            firstCenterY = svgRadialGradientServerHref;
                        }
                    }
                    if (firstRadius is null)
                    {
                        var pRadius = svgRadialGradientServerHref.Radius;
                        if (pRadius != SvgUnit.None && TryGetAttribute(svgRadialGradientServerHref, "r", out _))
                        {
                            firstRadius = svgRadialGradientServerHref;
                        }
                    }
                    if (firstFocalX is null)
                    {
                        var pFocalX = svgRadialGradientServerHref.FocalX;
                        if (pFocalX != SvgUnit.None && TryGetAttribute(svgRadialGradientServerHref, "fx", out _))
                        {
                            firstFocalX = svgRadialGradientServerHref;
                        }
                    }
                    if (firstFocalY is null)
                    {
                        var pFocalY = svgRadialGradientServerHref.FocalY;
                        if (pFocalY != SvgUnit.None && TryGetAttribute(svgRadialGradientServerHref, "fy", out _))
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

            var startRadius = 0f;
            var endRadius = normalizedRadius.ToDeviceValue(UnitRenderingType.Other, svgRadialGradientServer, skBounds);

            var focalX = normalizedFocalX.ToDeviceValue(UnitRenderingType.Horizontal, svgRadialGradientServer, skBounds);
            var focalY = normalizedFocalY.ToDeviceValue(UnitRenderingType.Vertical, svgRadialGradientServer, skBounds);

            var skStart = new Point(centerX, centerY);
            var skEnd = new Point(focalX, focalY);

            var colors = new List<Color>();
            var colorPos = new List<float>();

            GetStops(svgReferencedGradientServers, skBounds, colors, colorPos, svgVisualElement, opacity, ignoreAttributes);
            AdjustStopColorPos(colorPos);

            var shaderTileMode = svgSpreadMethod switch
            {
                SvgGradientSpreadMethod.Reflect => ShaderTileMode.Mirror,
                SvgGradientSpreadMethod.Repeat => ShaderTileMode.Repeat,
                _ => ShaderTileMode.Clamp,
            };
            var skColors = colors.ToArray();
            float[] skColorPos = colorPos.ToArray();

            if (skColors.Length == 0)
            {
                return Shader.CreateColor(new Color(0xFF, 0xFF, 0xFF, 0x00), skColorSpace);
            }
            else if (skColors.Length == 1)
            {
                return Shader.CreateColor(skColors[0], skColorSpace);
            }

            if (svgGradientUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                var skBoundingBoxTransform = new Matrix
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
                    var gradientTransform = ToMatrix(svgGradientTransform);
                    skBoundingBoxTransform = skBoundingBoxTransform.PreConcat(gradientTransform);
                }

                var skColorsF = ToSkColorF(skColors);
                return Shader.CreateTwoPointConicalGradient(
                    skStart, startRadius,
                    skEnd, endRadius,
                    skColorsF, skColorSpace, skColorPos,
                    shaderTileMode,
                    skBoundingBoxTransform);
            }
            else
            {
                if (svgGradientTransform is { } && svgGradientTransform.Count > 0)
                {
                    var gradientTransform = ToMatrix(svgGradientTransform);
                    var skColorsF = ToSkColorF(skColors);
                    return Shader.CreateTwoPointConicalGradient(
                        skStart, startRadius,
                        skEnd, endRadius,
                        skColorsF, skColorSpace, skColorPos,
                        shaderTileMode, gradientTransform);
                }
                else
                {
                    var skColorsF = ToSkColorF(skColors);
                    return Shader.CreateTwoPointConicalGradient(
                        skStart, startRadius,
                        skEnd, endRadius,
                        skColorsF, skColorSpace, skColorPos,
                        shaderTileMode);
                }
            }
        }

        internal static Picture RecordPicture(SvgElementCollection svgElementCollection, float width, float height, Matrix skMatrix, float opacity, IAssetLoader assetLoader, Attributes ignoreAttributes)
        {
            var skSize = new Size(width, height);
            var skBounds = Rect.Create(skSize);
            var skPictureRecorder = new PictureRecorder();
            var skCanvas = skPictureRecorder.BeginRecording(skBounds);

            skCanvas.SetMatrix(skMatrix);

            var skPaintOpacity = ignoreAttributes.HasFlag(Attributes.Opacity) ? null : GetOpacityPaint(opacity);
            if (skPaintOpacity is { })
            {
                skCanvas.SaveLayer(skPaintOpacity);
            }

            foreach (var svgElement in svgElementCollection)
            {
                var drawable = DrawableFactory.Create(svgElement, skBounds, null, assetLoader, ignoreAttributes);
                if (drawable is { })
                {
                    drawable.PostProcess(skBounds);
                    drawable.Draw(skCanvas, ignoreAttributes, null);
                }
            }

            if (skPaintOpacity is { })
            {
                skCanvas.Restore();
            }

            skCanvas.Restore();

            return skPictureRecorder.EndRecording();
        }

        internal static Shader? CreatePicture(SvgPatternServer svgPatternServer, Rect skBounds, SvgVisualElement svgVisualElement, float opacity, IAssetLoader assetLoader, Attributes ignoreAttributes)
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
                    if (TryGetAttribute(p, "patternUnits", out _))
                    {
                        firstPatternUnit = p;
                    }
                }
                if (firstPatternContentUnit is null)
                {
                    if (TryGetAttribute(p, "patternContentUnits", out _))
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
                    if (pAspectRatio is { } && pAspectRatio.Align != SvgPreserveAspectRatio.xMidYMid)
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

            var skRectTransformed = CalculateRect(xUnit, yUnit, widthUnit, heightUnit, patternUnits, skBounds, svgPatternServer);
            if (skRectTransformed is null)
            {
                return default;
            }

            var skMatrix = Matrix.CreateIdentity();

            var skPatternTransformMatrix = ToMatrix(svgPatternServer.PatternTransform);
            skMatrix = skMatrix.PreConcat(skPatternTransformMatrix);

            var translateTransform = Matrix.CreateTranslation(skRectTransformed.Value.Left, skRectTransformed.Value.Top);
            skMatrix = skMatrix.PreConcat(translateTransform);

            var skPictureTransform = Matrix.CreateIdentity();
            if (!viewBox.Equals(SvgViewBox.Empty))
            {
                var viewBoxTransform = ToMatrix(
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
                    var skBoundsScaleTransform = Matrix.CreateScale(skBounds.Width, skBounds.Height);
                    skPictureTransform = skPictureTransform.PreConcat(skBoundsScaleTransform);
                }
            }

            var skPicture = RecordPicture(firstChildren.Children, skRectTransformed.Value.Width, skRectTransformed.Value.Height, skPictureTransform, opacity, assetLoader, ignoreAttributes);

            return Shader.CreatePicture(skPicture, ShaderTileMode.Repeat, ShaderTileMode.Repeat, skMatrix, skPicture.CullRect);
        }

        internal static bool SetColorOrShader(SvgVisualElement svgVisualElement, SvgPaintServer server, float opacity, Rect skBounds, Paint skPaint, bool forStroke, IAssetLoader assetLoader, Attributes ignoreAttributes)
        {
            var fallbackServer = SvgPaintServer.None;
            if (server is SvgDeferredPaintServer deferredServer)
            {
                server = SvgDeferredPaintServer.TryGet<SvgPaintServer>(deferredServer, svgVisualElement);
                fallbackServer = deferredServer.FallbackServer;
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
                        var skColorSpace = isLinearRgb ? ColorSpace.SrgbLinear : ColorSpace.Srgb;
                        var skColorShader = Shader.CreateColor(skColor, skColorSpace);
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
                        var skColorSpace = isLinearRgb ? ColorSpace.SrgbLinear : ColorSpace.Srgb;
                        // TODO: Use skColorSpace in CreatePicture
                        var skPatternShader = CreatePicture(svgPatternServer, skBounds, svgVisualElement, opacity, assetLoader, ignoreAttributes);
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
                                var skColorShader = Shader.CreateColor(skColor, skColorSpace);
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
                        var skColorSpace = isLinearRgb ? ColorSpace.SrgbLinear : ColorSpace.Srgb;

                        if (svgLinearGradientServer.GradientUnits == SvgCoordinateUnits.ObjectBoundingBox && (skBounds.Width == 0f || skBounds.Height == 0f))
                        {
                            if (fallbackServer is SvgColourServer svgColourServerFallback)
                            {
                                var skColor = GetColor(svgColourServerFallback, opacity, ignoreAttributes);
                                var skColorShader = Shader.CreateColor(skColor, skColorSpace);
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
                        var skColorSpace = isLinearRgb ? ColorSpace.SrgbLinear : ColorSpace.Srgb;

                        if (svgRadialGradientServer.GradientUnits == SvgCoordinateUnits.ObjectBoundingBox && (skBounds.Width == 0f || skBounds.Height == 0f))
                        {
                            if (fallbackServer is SvgColourServer svgColourServerFallback)
                            {
                                var skColor = GetColor(svgColourServerFallback, opacity, ignoreAttributes);
                                var skColorShader = Shader.CreateColor(skColor, skColorSpace);
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
                    return SetColorOrShader(svgVisualElement, svgDeferredPaintServer, opacity, skBounds, skPaint, forStroke, assetLoader, ignoreAttributes);

                default:
                    // Do not draw element.
                    return false;
            }
            return true;
        }

        internal static void SetDash(SvgVisualElement svgVisualElement, Paint skPaint, Rect skBounds)
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

        internal static bool IsValidStroke(SvgElement svgElement, Rect skBounds)
        {
            var stroke = svgElement.Stroke;
            var strokeWidth = svgElement.StrokeWidth;
            return stroke is { }
                && stroke != SvgPaintServer.None
                && strokeWidth.ToDeviceValue(UnitRenderingType.Other, svgElement, skBounds) > 0f;
        }

        internal static Paint? GetFillPaint(SvgVisualElement svgVisualElement, Rect skBounds, IAssetLoader assetLoader, Attributes ignoreAttributes)
        {
            var skPaint = new Paint
            {
                IsAntialias = IsAntialias(svgVisualElement),
                Style = PaintStyle.Fill
            };

            var server = svgVisualElement.Fill;
            var opacity = AdjustSvgOpacity(svgVisualElement.FillOpacity);
            if (SetColorOrShader(svgVisualElement, server, opacity, skBounds, skPaint, forStroke: false, assetLoader: assetLoader, ignoreAttributes: ignoreAttributes) == false)
            {
                return default;
            }

            return skPaint;
        }

        internal static Paint? GetStrokePaint(SvgVisualElement svgVisualElement, Rect skBounds, IAssetLoader assetLoader, Attributes ignoreAttributes)
        {
            var skPaint = new Paint
            {
                IsAntialias = IsAntialias(svgVisualElement),
                Style = PaintStyle.Stroke
            };

            var server = svgVisualElement.Stroke;
            var opacity = AdjustSvgOpacity(svgVisualElement.StrokeOpacity);
            if (SetColorOrShader(svgVisualElement, server, opacity, skBounds, skPaint, forStroke: true, assetLoader: assetLoader, ignoreAttributes: ignoreAttributes) == false)
            {
                return default;
            }

            switch (svgVisualElement.StrokeLineCap)
            {
                case SvgStrokeLineCap.Butt:
                    skPaint.StrokeCap = StrokeCap.Butt;
                    break;

                case SvgStrokeLineCap.Round:
                    skPaint.StrokeCap = StrokeCap.Round;
                    break;

                case SvgStrokeLineCap.Square:
                    skPaint.StrokeCap = StrokeCap.Square;
                    break;
            }

            switch (svgVisualElement.StrokeLineJoin)
            {
                case SvgStrokeLineJoin.Miter:
                    skPaint.StrokeJoin = StrokeJoin.Miter;
                    break;

                case SvgStrokeLineJoin.Round:
                    skPaint.StrokeJoin = StrokeJoin.Round;
                    break;

                case SvgStrokeLineJoin.Bevel:
                    skPaint.StrokeJoin = StrokeJoin.Bevel;
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

        internal static Paint? GetOpacityPaint(float opacity)
        {
            if (opacity < 1f)
            {
                return new Paint
                {
                    IsAntialias = true,
                    Color = new Color(255, 255, 255, (byte)Math.Round(opacity * 255)),
                    Style = PaintStyle.StrokeAndFill
                };
            }
            return default;
        }

        internal static Paint? GetOpacityPaint(SvgElement svgElement)
        {
            var opacity = AdjustSvgOpacity(svgElement.Opacity);
            var skPaint = GetOpacityPaint(opacity);
            if (skPaint is { })
            {
                return skPaint;
            }
            return default;
        }

        internal static FontStyleWeight ToFontStyleWeight(SvgFontWeight svgFontWeight)
        {
            var fontWeight = FontStyleWeight.Normal;

            switch (svgFontWeight)
            {
                // TODO: Implement SvgFontWeight.Inherit
                case SvgFontWeight.Inherit:
                    break;

                // TODO: Implement SvgFontWeight.Bolder
                case SvgFontWeight.Bolder:
                    break;

                // TODO: Implement SvgFontWeight.Lighter
                case SvgFontWeight.Lighter:
                    break;

                case SvgFontWeight.W100:
                    fontWeight = FontStyleWeight.Thin;
                    break;

                case SvgFontWeight.W200:
                    fontWeight = FontStyleWeight.ExtraLight;
                    break;

                case SvgFontWeight.W300:
                    fontWeight = FontStyleWeight.Light;
                    break;

                case SvgFontWeight.Normal:
                case SvgFontWeight.W400:
                    fontWeight = FontStyleWeight.Normal;
                    break;

                case SvgFontWeight.W500:
                    fontWeight = FontStyleWeight.Medium;
                    break;

                case SvgFontWeight.W600:
                    fontWeight = FontStyleWeight.SemiBold;
                    break;

                case SvgFontWeight.Bold:
                case SvgFontWeight.W700:
                    fontWeight = FontStyleWeight.Bold;
                    break;

                case SvgFontWeight.W800:
                    fontWeight = FontStyleWeight.ExtraBold;
                    break;

                case SvgFontWeight.W900:
                    fontWeight = FontStyleWeight.Black;
                    break;
            }

            return fontWeight;
        }

        internal static FontStyleWidth ToFontStyleWidth(SvgFontStretch svgFontStretch)
        {
            var fontWidth = FontStyleWidth.Normal;

            switch (svgFontStretch)
            {
                // TODO: Implement SvgFontStretch.Inherit
                case SvgFontStretch.Inherit:
                    break;

                case SvgFontStretch.Normal:
                    fontWidth = FontStyleWidth.Normal;
                    break;

                // TODO: Implement SvgFontStretch.Wider
                case SvgFontStretch.Wider:
                    break;

                // TODO: Implement SvgFontStretch.Narrower
                case SvgFontStretch.Narrower:
                    break;

                case SvgFontStretch.UltraCondensed:
                    fontWidth = FontStyleWidth.UltraCondensed;
                    break;

                case SvgFontStretch.ExtraCondensed:
                    fontWidth = FontStyleWidth.ExtraCondensed;
                    break;

                case SvgFontStretch.Condensed:
                    fontWidth = FontStyleWidth.Condensed;
                    break;

                case SvgFontStretch.SemiCondensed:
                    fontWidth = FontStyleWidth.SemiCondensed;
                    break;

                case SvgFontStretch.SemiExpanded:
                    fontWidth = FontStyleWidth.SemiExpanded;
                    break;

                case SvgFontStretch.Expanded:
                    fontWidth = FontStyleWidth.Expanded;
                    break;

                case SvgFontStretch.ExtraExpanded:
                    fontWidth = FontStyleWidth.ExtraExpanded;
                    break;

                case SvgFontStretch.UltraExpanded:
                    fontWidth = FontStyleWidth.UltraExpanded;
                    break;
            }

            return fontWidth;
        }

        internal static TextAlign ToTextAlign(SvgTextAnchor textAnchor)
        {
            return textAnchor switch
            {
                SvgTextAnchor.Middle => TextAlign.Center,
                SvgTextAnchor.End => TextAlign.Right,
                _ => TextAlign.Left,
            };
        }

        internal static FontStyleSlant ToFontStyleSlant(SvgFontStyle fontStyle)
        {
            return fontStyle switch
            {
                SvgFontStyle.Oblique => FontStyleSlant.Oblique,
                SvgFontStyle.Italic => FontStyleSlant.Italic,
                _ => FontStyleSlant.Upright,
            };
        }

        private static void SetTypeface(SvgTextBase svgText, Paint skPaint)
        {
            var fontFamily = svgText.FontFamily;
            var fontWeight = ToFontStyleWeight(svgText.FontWeight);
            var fontWidth = ToFontStyleWidth(svgText.FontStretch);
            var fontStyle = ToFontStyleSlant(svgText.FontStyle);

            skPaint.Typeface = new Typeface
            {
                FamilyName = fontFamily,
                Weight = fontWeight,
                Width = fontWidth,
                Style = fontStyle
            };
        }

        internal static void SetPaintText(SvgTextBase svgText, Rect skBounds, Paint skPaint)
        {
            skPaint.LcdRenderText = true;
            skPaint.SubpixelText = true;
            skPaint.TextEncoding = TextEncoding.Utf16;

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
}
