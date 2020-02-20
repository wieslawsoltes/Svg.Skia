// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using SkiaSharp;
using Svg.DataTypes;

namespace Svg.Skia
{
    public static class SvgPaintingExtensions
    {
        public static SKColorSpace SrgbLinear = SKColorSpace.CreateRgb(SKNamedGamma.Linear, SKColorSpaceGamut.Srgb); // SKColorSpace.CreateSrgbLinear();

        public static SKColorSpace Srgb = SKColorSpace.CreateRgb(SKNamedGamma.Srgb, SKColorSpaceGamut.Srgb); // SKColorSpace.CreateSrgb();

        // Precomputed sRGB to LinearRGB table.
        // if (C_srgb <= 0.04045)
        //     C_lin = C_srgb / 12.92;
        //  else
        //     C_lin = pow((C_srgb + 0.055) / 1.055, 2.4);
        public static byte[] s_SRGBtoLinearRGB = new byte[256]
        {
            0,   0,   0,   0,   0,   0,  0,    1,   1,   1,   1,   1,   1,   1,   1,   1,
            1,   1,   2,   2,   2,   2,  2,    2,   2,   2,   3,   3,   3,   3,   3,   3,
            4,   4,   4,   4,   4,   5,  5,    5,   5,   6,   6,   6,   6,   7,   7,   7,
            8,   8,   8,   8,   9,   9,  9,   10,  10,  10,  11,  11,  12,  12,  12,  13,
            13,  13,  14,  14,  15,  15,  16,  16,  17,  17,  17,  18,  18,  19,  19,  20,
            20,  21,  22,  22,  23,  23,  24,  24,  25,  25,  26,  27,  27,  28,  29,  29,
            30,  30,  31,  32,  32,  33,  34,  35,  35,  36,  37,  37,  38,  39,  40,  41,
            41,  42,  43,  44,  45,  45,  46,  47,  48,  49,  50,  51,  51,  52,  53,  54,
            55,  56,  57,  58,  59,  60,  61,  62,  63,  64,  65,  66,  67,  68,  69,  70,
            71,  72,  73,  74,  76,  77,  78,  79,  80,  81,  82,  84,  85,  86,  87,  88,
            90,  91,  92,  93,  95,  96,  97,  99, 100, 101, 103, 104, 105, 107, 108, 109,
            111, 112, 114, 115, 116, 118, 119, 121, 122, 124, 125, 127, 128, 130, 131, 133,
            134, 136, 138, 139, 141, 142, 144, 146, 147, 149, 151, 152, 154, 156, 157, 159,
            161, 163, 164, 166, 168, 170, 171, 173, 175, 177, 179, 181, 183, 184, 186, 188,
            190, 192, 194, 196, 198, 200, 202, 204, 206, 208, 210, 212, 214, 216, 218, 220,
            222, 224, 226, 229, 231, 233, 235, 237, 239, 242, 244, 246, 248, 250, 253, 255,
        };

        // Precomputed LinearRGB to sRGB table.
        // if (C_lin <= 0.0031308)
        //     C_srgb = C_lin * 12.92;
        // else
        //     C_srgb = 1.055 * pow(C_lin, 1.0 / 2.4) - 0.055;
        public static byte[] s_LinearRGBtoSRGB = new byte[256]
        {
            0,  13,  22,  28,  34,  38,  42,  46,  50,  53,  56,  59,  61,  64,  66,  69,
            71,  73,  75,  77,  79,  81,  83,  85,  86,  88,  90,  92,  93,  95,  96,  98,
            99, 101, 102, 104, 105, 106, 108, 109, 110, 112, 113, 114, 115, 117, 118, 119,
            120, 121, 122, 124, 125, 126, 127, 128, 129, 130, 131, 132, 133, 134, 135, 136,
            137, 138, 139, 140, 141, 142, 143, 144, 145, 146, 147, 148, 148, 149, 150, 151,
            152, 153, 154, 155, 155, 156, 157, 158, 159, 159, 160, 161, 162, 163, 163, 164,
            165, 166, 167, 167, 168, 169, 170, 170, 171, 172, 173, 173, 174, 175, 175, 176,
            177, 178, 178, 179, 180, 180, 181, 182, 182, 183, 184, 185, 185, 186, 187, 187,
            188, 189, 189, 190, 190, 191, 192, 192, 193, 194, 194, 195, 196, 196, 197, 197,
            198, 199, 199, 200, 200, 201, 202, 202, 203, 203, 204, 205, 205, 206, 206, 207,
            208, 208, 209, 209, 210, 210, 211, 212, 212, 213, 213, 214, 214, 215, 215, 216,
            216, 217, 218, 218, 219, 219, 220, 220, 221, 221, 222, 222, 223, 223, 224, 224,
            225, 226, 226, 227, 227, 228, 228, 229, 229, 230, 230, 231, 231, 232, 232, 233,
            233, 234, 234, 235, 235, 236, 236, 237, 237, 238, 238, 238, 239, 239, 240, 240,
            241, 241, 242, 242, 243, 243, 244, 244, 245, 245, 246, 246, 246, 247, 247, 248,
            248, 249, 249, 250, 250, 251, 251, 251, 252, 252, 253, 253, 254, 254, 255, 255,
        };

        public static SKColor TransparentBlack = new SKColor(0, 0, 0, 255);

        public static float AdjustSvgOpacity(float opacity)
        {
            return Math.Min(Math.Max(opacity, 0), 1);
        }

        public static byte CombineWithOpacity(byte alpha, float opacity)
        {
            return (byte)Math.Round((opacity * (alpha / 255.0)) * 255);
        }

        public static SKColor GetColor(SvgColourServer svgColourServer, float opacity, Attributes ignoreAttributes)
        {
            var colour = svgColourServer.Colour;
            byte alpha = ignoreAttributes.HasFlag(Attributes.Opacity) ?
                svgColourServer.Colour.A :
                CombineWithOpacity(svgColourServer.Colour.A, opacity);

            return new SKColor(colour.R, colour.G, colour.B, alpha);
        }

        public static SKColor? GetColor(SvgVisualElement svgVisualElement, SvgPaintServer server)
        {
            if (server is SvgDeferredPaintServer svgDeferredPaintServer)
            {
                server = SvgDeferredPaintServer.TryGet<SvgPaintServer>(svgDeferredPaintServer, svgVisualElement);
            }

            if (server is SvgColourServer stopColorSvgColourServer)
            {
                return GetColor(stopColorSvgColourServer, 1f, Attributes.None);
            }

            return SKColors.Black;
        }

        public static SKPathEffect? CreateDash(SvgElement svgElement, SKRect skBounds)
        {
            var strokeDashArray = svgElement.StrokeDashArray;
            var strokeDashOffset = svgElement.StrokeDashOffset;
            var count = strokeDashArray.Count;

            if (strokeDashArray != null && count > 0)
            {
                bool isOdd = count % 2 != 0;
                float sum = 0f;
                float[] intervals = new float[isOdd ? count * 2 : count];
                for (int i = 0; i < count; i++)
                {
                    var dash = strokeDashArray[i].ToDeviceValue(UnitRenderingType.Other, svgElement, skBounds);
                    if (dash < 0f)
                    {
                        return null;
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
                    return null;
                }

                float phase = strokeDashOffset != null ? strokeDashOffset.ToDeviceValue(UnitRenderingType.Other, svgElement, skBounds) : 0f;

                return SKPathEffect.CreateDash(intervals, phase);
            }

            return null;
        }

        private static List<SvgPatternServer> GetLinkedPatternServer(SvgPatternServer svgPatternServer, SvgVisualElement svgVisualElement)
        {
            var svgPatternServers = new List<SvgPatternServer>();
            var currentPatternServer = svgPatternServer;
            do
            {
                svgPatternServers.Add(currentPatternServer);
                currentPatternServer = SvgDeferredPaintServer.TryGet<SvgPatternServer>(currentPatternServer.InheritGradient, svgVisualElement);
            } while (currentPatternServer != null);
            return svgPatternServers;
        }

        private static List<SvgGradientServer> GetLinkedGradientServer(SvgGradientServer svgGradientServer, SvgVisualElement svgVisualElement)
        {
            var svgGradientServers = new List<SvgGradientServer>();
            var currentGradientServer = svgGradientServer;
            do
            {
                svgGradientServers.Add((SvgGradientServer)currentGradientServer);
                currentGradientServer = SvgDeferredPaintServer.TryGet<SvgGradientServer>(currentGradientServer.InheritGradient, svgVisualElement);
            } while (currentGradientServer != null);
            return svgGradientServers;
        }

        private static void GetStopsImpl(SvgGradientServer svgGradientServer, SKRect skBounds, List<SKColor> colors, List<float> colorPos, SvgVisualElement svgVisualElement, float opacity, Attributes ignoreAttributes)
        {
            foreach (var child in svgGradientServer.Children)
            {
                if (child is SvgGradientStop svgGradientStop)
                {
                    var server = svgGradientStop.StopColor;
                    if (server is SvgDeferredPaintServer svgDeferredPaintServer)
                    {
                        server = SvgDeferredPaintServer.TryGet<SvgPaintServer>(svgDeferredPaintServer, svgVisualElement);
                        if (server == null)
                        {
                            // TODO: server is sometimes null with currentColor
                        }
                    }

                    if (server is SvgColourServer stopColorSvgColourServer)
                    {
                        var stopOpacity = AdjustSvgOpacity(svgGradientStop.StopOpacity);
                        var stopColor = GetColor(stopColorSvgColourServer, opacity * stopOpacity, ignoreAttributes);
                        float offset = svgGradientStop.Offset.ToDeviceValue(UnitRenderingType.Horizontal, svgGradientServer, skBounds);
                        offset /= skBounds.Width;
                        colors.Add(stopColor);
                        colorPos.Add(offset);
                    }
                }
            }
        }

        public static void GetStops(List<SvgGradientServer> svgReferencedGradientServers, SKRect skBounds, List<SKColor> colors, List<float> colorPos, SvgVisualElement svgVisualElement, float opacity, Attributes ignoreAttributes)
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
            float maxPos = float.MinValue;
            for (int i = 0; i < colorPos.Count; i++)
            {
                float pos = colorPos[i];
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
#if USE_COLORSPACE
        public static SKColorF[] ToSkColorF(this SKColor[] skColors)
        {
            var skColorsF = new SKColorF[skColors.Length];

            for (int i = 0; i < skColors.Length; i++)
            {
                skColorsF[i] = (SKColorF)skColors[i];
            }

            return skColorsF;
        }

        public static SvgColourInterpolation GetColorInterpolation(SvgElement svgElement)
        {
            switch (svgElement.ColorInterpolation)
            {
                case SvgColourInterpolation.Auto:
                    return SvgColourInterpolation.SRGB;
                case SvgColourInterpolation.SRGB:
                    return SvgColourInterpolation.SRGB;
                case SvgColourInterpolation.LinearRGB:
                    return SvgColourInterpolation.LinearRGB;
            }
            return SvgColourInterpolation.SRGB;
        }

        public static SvgColourInterpolation GetColorInterpolationFilters(SvgElement svgElement)
        {
            switch (svgElement.ColorInterpolationFilters)
            {
                case SvgColourInterpolation.Auto:
                    return SvgColourInterpolation.LinearRGB;
                case SvgColourInterpolation.SRGB:
                    return SvgColourInterpolation.SRGB;
                case SvgColourInterpolation.LinearRGB:
                    return SvgColourInterpolation.LinearRGB;
            }
            return SvgColourInterpolation.LinearRGB;
        }
#endif

#if USE_COLORSPACE
        public static SKShader CreateLinearGradient(SvgLinearGradientServer svgLinearGradientServer, SKRect skBounds, SvgVisualElement svgVisualElement, float opacity, Attributes ignoreAttributes, SKColorSpace skColorSpace)
#else
        public static SKShader CreateLinearGradient(SvgLinearGradientServer svgLinearGradientServer, SKRect skBounds, SvgVisualElement svgVisualElement, float opacity, Attributes ignoreAttributes)
#endif
        {
            var svgReferencedGradientServers = GetLinkedGradientServer(svgLinearGradientServer, svgVisualElement);

            SvgGradientServer? firstSpreadMethod = null;
            SvgGradientServer? firstGradientTransform = null;
            SvgGradientServer? firstGradientUnits = null;
            SvgLinearGradientServer? firstX1 = null;
            SvgLinearGradientServer? firstY1 = null;
            SvgLinearGradientServer? firstX2 = null;
            SvgLinearGradientServer? firstY2 = null;

            foreach (var p in svgReferencedGradientServers)
            {
                if (firstSpreadMethod == null)
                {
                    var pSpreadMethod = p.SpreadMethod;
                    if (pSpreadMethod != SvgGradientSpreadMethod.Pad)
                    {
                        firstSpreadMethod = p;
                    }
                }
                if (firstGradientTransform == null)
                {
                    var pGradientTransform = p.GradientTransform;
                    if (pGradientTransform != null && pGradientTransform.Count > 0)
                    {
                        firstGradientTransform = p;
                    }
                }
                if (firstGradientUnits == null)
                {
                    var pGradientUnits = p.GradientUnits;
                    if (pGradientUnits != SvgCoordinateUnits.ObjectBoundingBox)
                    {
                        firstGradientUnits = p;
                    }
                }

                if (p is SvgLinearGradientServer svgLinearGradientServerHref)
                {
                    if (firstX1 == null)
                    {
                        var pX1 = svgLinearGradientServerHref.X1;
                        if (pX1 != null && pX1 != SvgUnit.None && svgLinearGradientServerHref.GetAttribute("x1", out _) == true)
                        {
                            firstX1 = svgLinearGradientServerHref;
                        }
                    }
                    if (firstY1 == null)
                    {
                        var pY1 = svgLinearGradientServerHref.Y1;
                        if (pY1 != null && pY1 != SvgUnit.None && svgLinearGradientServerHref.GetAttribute("y1", out _) == true)
                        {
                            firstY1 = svgLinearGradientServerHref;
                        }
                    }
                    if (firstX2 == null)
                    {
                        var pX2 = svgLinearGradientServerHref.X2;
                        if (pX2 != null && pX2 != SvgUnit.None && svgLinearGradientServerHref.GetAttribute("x2", out _) == true)
                        {
                            firstX2 = svgLinearGradientServerHref;
                        }
                    }
                    if (firstY2 == null)
                    {
                        var pY2 = svgLinearGradientServerHref.Y2;
                        if (pY2 != null && pY2 != SvgUnit.None && svgLinearGradientServerHref.GetAttribute("y2", out _) == true)
                        {
                            firstY2 = svgLinearGradientServerHref;
                        }
                    }
                }
            }

            var svgSpreadMethod = firstSpreadMethod == null ? SvgGradientSpreadMethod.Pad : firstSpreadMethod.SpreadMethod;
            var svgGradientTransform = firstGradientTransform?.GradientTransform;
            var svgGradientUnits = firstGradientUnits == null ? SvgCoordinateUnits.ObjectBoundingBox : firstGradientUnits.GradientUnits;
            var x1Unit = firstX1 == null ? new SvgUnit(SvgUnitType.Percentage, 0f) : firstX1.X1;
            var y1Unit = firstY1 == null ? new SvgUnit(SvgUnitType.Percentage, 0f) : firstY1.Y1;
            var x2Unit = firstX2 == null ? new SvgUnit(SvgUnitType.Percentage, 100f) : firstX2.X2;
            var y2Unit = firstY2 == null ? new SvgUnit(SvgUnitType.Percentage, 0f) : firstY2.Y2;

            var normalizedX1 = x1Unit.Normalize(svgGradientUnits);
            var normalizedY1 = y1Unit.Normalize(svgGradientUnits);
            var normalizedX2 = x2Unit.Normalize(svgGradientUnits);
            var normalizedY2 = y2Unit.Normalize(svgGradientUnits);

            float x1 = normalizedX1.ToDeviceValue(UnitRenderingType.Horizontal, svgLinearGradientServer, skBounds);
            float y1 = normalizedY1.ToDeviceValue(UnitRenderingType.Vertical, svgLinearGradientServer, skBounds);
            float x2 = normalizedX2.ToDeviceValue(UnitRenderingType.Horizontal, svgLinearGradientServer, skBounds);
            float y2 = normalizedY2.ToDeviceValue(UnitRenderingType.Vertical, svgLinearGradientServer, skBounds);

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
#if USE_COLORSPACE
                return SKShader.CreateColor(SKColors.Transparent, skColorSpace);
#else
                return SKShader.CreateColor(SKColors.Transparent);
#endif
            }
            else if (skColors.Length == 1)
            {
#if USE_COLORSPACE
                return SKShader.CreateColor(skColors[0], skColorSpace);
#else
                return SKShader.CreateColor(skColors[0]);
#endif
            }

            if (svgGradientUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                var skBoundingBoxTransform = new SKMatrix()
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

                if (svgGradientTransform != null && svgGradientTransform.Count > 0)
                {
                    var gradientTransform = SvgTransformsExtensions.ToSKMatrix(svgGradientTransform);
                    SKMatrix.PreConcat(ref skBoundingBoxTransform, ref gradientTransform);
                }

#if USE_COLORSPACE
                var skColorsF = ToSkColorF(skColors);
                return SKShader.CreateLinearGradient(skStart, skEnd, skColorsF, skColorSpace, skColorPos, shaderTileMode, skBoundingBoxTransform);
#else
                return SKShader.CreateLinearGradient(skStart, skEnd, skColors, skColorPos, shaderTileMode, skBoundingBoxTransform);
#endif
            }
            else
            {
                if (svgGradientTransform != null && svgGradientTransform.Count > 0)
                {
                    var gradientTransform = SvgTransformsExtensions.ToSKMatrix(svgGradientTransform);
#if USE_COLORSPACE
                    var skColorsF = ToSkColorF(skColors);
                    return SKShader.CreateLinearGradient(skStart, skEnd, skColorsF, skColorSpace, skColorPos, shaderTileMode, gradientTransform);
#else
                    return SKShader.CreateLinearGradient(skStart, skEnd, skColors, skColorPos, shaderTileMode, gradientTransform);
#endif
                }
                else
                {
#if USE_COLORSPACE
                    var skColorsF = ToSkColorF(skColors);
                    return SKShader.CreateLinearGradient(skStart, skEnd, skColorsF, skColorSpace, skColorPos, shaderTileMode);
#else
                    return SKShader.CreateLinearGradient(skStart, skEnd, skColors, skColorPos, shaderTileMode);
#endif
                }
            }
        }

#if USE_COLORSPACE
        public static SKShader CreateTwoPointConicalGradient(SvgRadialGradientServer svgRadialGradientServer, SKRect skBounds, SvgVisualElement svgVisualElement, float opacity, Attributes ignoreAttributes, SKColorSpace skColorSpace)
#else
        public static SKShader CreateTwoPointConicalGradient(SvgRadialGradientServer svgRadialGradientServer, SKRect skBounds, SvgVisualElement svgVisualElement, float opacity, Attributes ignoreAttributes)
#endif
        {
            var svgReferencedGradientServers = GetLinkedGradientServer(svgRadialGradientServer, svgVisualElement);

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
                if (firstSpreadMethod == null)
                {
                    var pSpreadMethod = p.SpreadMethod;
                    if (pSpreadMethod != SvgGradientSpreadMethod.Pad)
                    {
                        firstSpreadMethod = p;
                    }
                }
                if (firstGradientTransform == null)
                {
                    var pGradientTransform = p.GradientTransform;
                    if (pGradientTransform != null && pGradientTransform.Count > 0)
                    {
                        firstGradientTransform = p;
                    }
                }
                if (firstGradientUnits == null)
                {
                    var pGradientUnits = p.GradientUnits;
                    if (pGradientUnits != SvgCoordinateUnits.ObjectBoundingBox)
                    {
                        firstGradientUnits = p;
                    }
                }

                if (p is SvgRadialGradientServer svgRadialGradientServerHref)
                {
                    if (firstCenterX == null)
                    {
                        var pCenterX = svgRadialGradientServerHref.CenterX;
                        if (pCenterX != null && pCenterX != SvgUnit.None && svgRadialGradientServerHref.GetAttribute("cx", out _) == true)
                        {
                            firstCenterX = svgRadialGradientServerHref;
                        }
                    }
                    if (firstCenterY == null)
                    {
                        var pCenterY = svgRadialGradientServerHref.CenterY;
                        if (pCenterY != null && pCenterY != SvgUnit.None && svgRadialGradientServerHref.GetAttribute("cy", out _) == true)
                        {
                            firstCenterY = svgRadialGradientServerHref;
                        }
                    }
                    if (firstRadius == null)
                    {
                        var pRadius = svgRadialGradientServerHref.Radius;
                        if (pRadius != null && pRadius != SvgUnit.None && svgRadialGradientServerHref.GetAttribute("r", out _) == true)
                        {
                            firstRadius = svgRadialGradientServerHref;
                        }
                    }
                    if (firstFocalX == null)
                    {
                        var pFocalX = svgRadialGradientServerHref.FocalX;
                        if (pFocalX != null && pFocalX != SvgUnit.None && svgRadialGradientServerHref.GetAttribute("fx", out _) == true)
                        {
                            firstFocalX = svgRadialGradientServerHref;
                        }
                    }
                    if (firstFocalY == null)
                    {
                        var pFocalY = svgRadialGradientServerHref.FocalY;
                        if (pFocalY != null && pFocalY != SvgUnit.None && svgRadialGradientServerHref.GetAttribute("fy", out _) == true)
                        {
                            firstFocalY = svgRadialGradientServerHref;
                        }
                    }
                }
            }

            var svgSpreadMethod = firstSpreadMethod == null ? SvgGradientSpreadMethod.Pad : firstSpreadMethod.SpreadMethod;
            var svgGradientTransform = firstGradientTransform?.GradientTransform;
            var svgGradientUnits = firstGradientUnits == null ? SvgCoordinateUnits.ObjectBoundingBox : firstGradientUnits.GradientUnits;
            var centerXUnit = firstCenterX == null ? new SvgUnit(SvgUnitType.Percentage, 50f) : firstCenterX.CenterX;
            var centerYUnit = firstCenterY == null ? new SvgUnit(SvgUnitType.Percentage, 50f) : firstCenterY.CenterY;
            var radiusUnit = firstRadius == null ? new SvgUnit(SvgUnitType.Percentage, 50f) : firstRadius.Radius;
            var focalXUnit = firstFocalX == null ? centerXUnit : firstFocalX.FocalX;
            var focalYUnit = firstFocalY == null ? centerYUnit : firstFocalY.FocalY;

            var normalizedCenterX = centerXUnit.Normalize(svgGradientUnits);
            var normalizedCenterY = centerYUnit.Normalize(svgGradientUnits);
            var normalizedRadius = radiusUnit.Normalize(svgGradientUnits);
            var normalizedFocalX = focalXUnit.Normalize(svgGradientUnits);
            var normalizedFocalY = focalYUnit.Normalize(svgGradientUnits);

            float centerX = normalizedCenterX.ToDeviceValue(UnitRenderingType.Horizontal, svgRadialGradientServer, skBounds);
            float centerY = normalizedCenterY.ToDeviceValue(UnitRenderingType.Vertical, svgRadialGradientServer, skBounds);

            float startRadius = 0f;
            float endRadius = normalizedRadius.ToDeviceValue(UnitRenderingType.Other, svgRadialGradientServer, skBounds);

            float focalX = normalizedFocalX.ToDeviceValue(UnitRenderingType.Horizontal, svgRadialGradientServer, skBounds);
            float focalY = normalizedFocalY.ToDeviceValue(UnitRenderingType.Vertical, svgRadialGradientServer, skBounds);

            var skStart = new SKPoint(centerX, centerY);
            var skEnd = new SKPoint(focalX, focalY);

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
#if USE_COLORSPACE
                return SKShader.CreateColor(SKColors.Transparent, skColorSpace);
#else
                return SKShader.CreateColor(SKColors.Transparent);
#endif
            }
            else if (skColors.Length == 1)
            {
#if USE_COLORSPACE
                return SKShader.CreateColor(skColors[0], skColorSpace);
#else
                return SKShader.CreateColor(skColors[0]);
#endif
            }

            if (svgGradientUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                var skBoundingBoxTransform = new SKMatrix()
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

                if (svgGradientTransform != null && svgGradientTransform.Count > 0)
                {
                    var gradientTransform = SvgTransformsExtensions.ToSKMatrix(svgGradientTransform);
                    SKMatrix.PreConcat(ref skBoundingBoxTransform, ref gradientTransform);
                }

#if USE_COLORSPACE
                var skColorsF = ToSkColorF(skColors);
                return SKShader.CreateTwoPointConicalGradient(
                    skStart, startRadius,
                    skEnd, endRadius,
                    skColorsF, skColorSpace, skColorPos,
                    shaderTileMode,
                    skBoundingBoxTransform);
#else
                return SKShader.CreateTwoPointConicalGradient(
                    skStart, startRadius,
                    skEnd, endRadius,
                    skColors, skColorPos,
                    shaderTileMode,
                    skBoundingBoxTransform);
#endif
            }
            else
            {
                if (svgGradientTransform != null && svgGradientTransform.Count > 0)
                {
                    var gradientTransform = SvgTransformsExtensions.ToSKMatrix(svgGradientTransform);
#if USE_COLORSPACE
                    var skColorsF = ToSkColorF(skColors);
                    return SKShader.CreateTwoPointConicalGradient(
                        skStart, startRadius,
                        skEnd, endRadius,
                        skColorsF, skColorSpace, skColorPos,
                        shaderTileMode, gradientTransform);
#else
                    return SKShader.CreateTwoPointConicalGradient(
                        skStart, startRadius,
                        skEnd, endRadius,
                        skColors, skColorPos,
                        shaderTileMode, gradientTransform);
#endif
                }
                else
                {
#if USE_COLORSPACE
                    var skColorsF = ToSkColorF(skColors);
                    return SKShader.CreateTwoPointConicalGradient(
                        skStart, startRadius,
                        skEnd, endRadius,
                        skColorsF, skColorSpace, skColorPos,
                        shaderTileMode);
#else
                    return SKShader.CreateTwoPointConicalGradient(
                        skStart, startRadius,
                        skEnd, endRadius,
                        skColors, skColorPos,
                        shaderTileMode);
#endif
                }
            }
        }

        public static SKPicture RecordPicture(SvgElementCollection svgElementCollection, float width, float height, SKMatrix skMatrix, float opacity, Attributes ignoreAttributes)
        {
            var skSize = new SKSize(width, height);
            var skBounds = SKRect.Create(skSize);
            using var skPictureRecorder = new SKPictureRecorder();
            using var skCanvas = skPictureRecorder.BeginRecording(skBounds);

            skCanvas.SetMatrix(skMatrix);

            using var skPaintOpacity = ignoreAttributes.HasFlag(Attributes.Opacity) ? null : GetOpacitySKPaint(opacity);
            if (skPaintOpacity != null)
            {
                skCanvas.SaveLayer(skPaintOpacity);
            }

            foreach (var svgElement in svgElementCollection)
            {
                using var drawable = DrawableFactory.Create(svgElement, skBounds, null, null, ignoreAttributes);
                drawable?.PostProcess();
                drawable?.Draw(skCanvas, ignoreAttributes, null);
            }

            if (skPaintOpacity != null)
            {
                skCanvas.Restore();
            }

            skCanvas.Restore();

            return skPictureRecorder.EndRecording();
        }

        public static SKShader? CreatePicture(SvgPatternServer svgPatternServer, SKRect skBounds, SvgVisualElement svgVisualElement, float opacity, Attributes ignoreAttributes, CompositeDisposable disposable)
        {
            var svgReferencedPatternServers = GetLinkedPatternServer(svgPatternServer, svgVisualElement);

            SvgPatternServer? firstChildren = null;
            SvgPatternServer? firstX = null;
            SvgPatternServer? firstY = null;
            SvgPatternServer? firstWidth = null;
            SvgPatternServer? firstHeight = null;
            SvgPatternServer? firstPatternUnit = null;
            SvgPatternServer? firstPatternContentUnit = null;
            SvgPatternServer? firstViewBox = null;
            SvgPatternServer? firstAspectRatio = null;

            foreach (var p in svgReferencedPatternServers)
            {
                if (firstChildren == null)
                {
                    if (p.Children.Count > 0)
                    {
                        firstChildren = p;
                    }
                }
                if (firstX == null)
                {
                    var pX = p.X;
                    if (pX != null && pX != SvgUnit.None)
                    {
                        firstX = p;
                    }
                }
                if (firstY == null)
                {
                    var pY = p.Y;
                    if (pY != null && pY != SvgUnit.None)
                    {
                        firstY = p;
                    }
                }
                if (firstWidth == null)
                {
                    var pWidth = p.Width;
                    if (pWidth != null && pWidth != SvgUnit.None)
                    {
                        firstWidth = p;
                    }
                }
                if (firstHeight == null)
                {
                    var pHeight = p.Height;
                    if (pHeight != null && pHeight != SvgUnit.None)
                    {
                        firstHeight = p;
                    }
                }
                if (firstPatternUnit == null)
                {
                    if (p.GetAttribute("patternUnits", out _) == true)
                    {
                        firstPatternUnit = p;
                    }
                }
                if (firstPatternContentUnit == null)
                {
                    if (p.GetAttribute("patternContentUnits", out _) == true)
                    {
                        firstPatternContentUnit = p;
                    }
                }
                if (firstViewBox == null)
                {
                    var pViewBox = p.ViewBox;
                    if (pViewBox != null && pViewBox != SvgViewBox.Empty)
                    {
                        firstViewBox = p;
                    }
                }
                if (firstAspectRatio == null)
                {
                    var pAspectRatio = p.AspectRatio;
                    if (pAspectRatio != null && pAspectRatio.Align != SvgPreserveAspectRatio.xMidYMid)
                    {
                        firstAspectRatio = p;
                    }
                }
            }

            if (firstChildren == null || firstWidth == null || firstHeight == null)
            {
                return null;
            }

            var xUnit = firstX == null ? new SvgUnit(0f) : firstX.X;
            var yUnit = firstY == null ? new SvgUnit(0f) : firstY.Y;
            var widthUnit = firstWidth.Width;
            var heightUnit = firstHeight.Height;
            var patternUnits = firstPatternUnit == null ? SvgCoordinateUnits.ObjectBoundingBox : firstPatternUnit.PatternUnits;
            var patternContentUnits = firstPatternContentUnit == null ? SvgCoordinateUnits.UserSpaceOnUse : firstPatternContentUnit.PatternContentUnits;
            var viewBox = firstViewBox == null ? SvgViewBox.Empty : firstViewBox.ViewBox;
            var aspectRatio = firstAspectRatio == null ? new SvgAspectRatio(SvgPreserveAspectRatio.xMidYMid, false) : firstAspectRatio.AspectRatio;

            float x = xUnit.ToDeviceValue(UnitRenderingType.Horizontal, svgPatternServer, skBounds);
            float y = yUnit.ToDeviceValue(UnitRenderingType.Vertical, svgPatternServer, skBounds);
            float width = widthUnit.ToDeviceValue(UnitRenderingType.Horizontal, svgPatternServer, skBounds);
            float height = heightUnit.ToDeviceValue(UnitRenderingType.Vertical, svgPatternServer, skBounds);

            if (width <= 0 || height <= 0)
            {
                return null;
            }

            if (patternUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                if (xUnit.Type != SvgUnitType.Percentage)
                {
                    x *= skBounds.Width;
                }

                if (yUnit.Type != SvgUnitType.Percentage)
                {
                    y *= skBounds.Height;
                }

                if (widthUnit.Type != SvgUnitType.Percentage)
                {
                    width *= skBounds.Width;
                }

                if (heightUnit.Type != SvgUnitType.Percentage)
                {
                    height *= skBounds.Height;
                }

                x += skBounds.Left;
                y += skBounds.Top;
            }

            SKRect skRectTransformed = SKRect.Create(x, y, width, height);

            var skMatrix = SKMatrix.MakeIdentity();

            var skPatternTransformMatrix = SvgTransformsExtensions.ToSKMatrix(svgPatternServer.PatternTransform);
            SKMatrix.PreConcat(ref skMatrix, ref skPatternTransformMatrix);

            var translateTransform = SKMatrix.MakeTranslation(skRectTransformed.Left, skRectTransformed.Top);
            SKMatrix.PreConcat(ref skMatrix, ref translateTransform);

            SKMatrix skPictureTransform = SKMatrix.MakeIdentity();
            if (!viewBox.Equals(SvgViewBox.Empty))
            {
                var viewBoxTransform = SvgTransformsExtensions.ToSKMatrix(
                    viewBox,
                    aspectRatio,
                    0f,
                    0f,
                    skRectTransformed.Width,
                    skRectTransformed.Height);
                SKMatrix.PreConcat(ref skPictureTransform, ref viewBoxTransform);
            }
            else
            {
                if (patternContentUnits == SvgCoordinateUnits.ObjectBoundingBox)
                {
                    var skBoundsScaleTransform = SKMatrix.MakeScale(skBounds.Width, skBounds.Height);
                    SKMatrix.PreConcat(ref skPictureTransform, ref skBoundsScaleTransform);
                }
            }

            var skPicture = RecordPicture(firstChildren.Children, skRectTransformed.Width, skRectTransformed.Height, skPictureTransform, opacity, ignoreAttributes);
            disposable.Add(skPicture);

            return SKShader.CreatePicture(skPicture, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, skMatrix, skPicture.CullRect);
        }

        public static bool SetColorOrShader(SvgVisualElement svgVisualElement, SvgPaintServer server, float opacity, SKRect skBounds, SKPaint skPaint, bool forStroke, Attributes ignoreAttributes, CompositeDisposable disposable)
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
#if USE_COLORSPACE
                        var colorInterpolation = GetColorInterpolation(svgVisualElement);
                        var isLinearRGB = colorInterpolation == SvgColourInterpolation.LinearRGB;
                        var skColorSpace = isLinearRGB ? SrgbLinear : Srgb;
                        var skColorShader = SKShader.CreateColor(skColor, skColorSpace);
#else
                        var skColorShader = SKShader.CreateColor(skColor);
#endif
                        if (skColorShader != null)
                        {
                            disposable.Add(skColorShader);
                            skPaint.Shader = skColorShader;
                            return true;
                        }
                    }
                    break;
                case SvgPatternServer svgPatternServer:
                    {
                        var skPatternShader = CreatePicture(svgPatternServer, skBounds, svgVisualElement, opacity, ignoreAttributes, disposable);
                        if (skPatternShader != null)
                        {
                            disposable.Add(skPatternShader);
                            skPaint.Shader = skPatternShader;
                            return true;
                        }
                        else
                        {
                            if (fallbackServer is SvgColourServer svgColourServerFallback)
                            {
                                var skColor = GetColor(svgColourServerFallback, opacity, ignoreAttributes);
#if USE_COLORSPACE
                                var colorInterpolation = GetColorInterpolation(svgVisualElement);
                                var isLinearRGB = colorInterpolation == SvgColourInterpolation.LinearRGB;
                                var skColorSpace = isLinearRGB ? SrgbLinear : Srgb;
                                var skColorShader = SKShader.CreateColor(skColor, skColorSpace);
#else
                                var skColorShader = SKShader.CreateColor(skColor);
#endif
                                if (skColorShader != null)
                                {
                                    disposable.Add(skColorShader);
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
#if USE_COLORSPACE
                        var colorInterpolation = GetColorInterpolation(svgLinearGradientServer);
                        var isLinearRGB = colorInterpolation == SvgColourInterpolation.LinearRGB;
                        var skColorSpace = isLinearRGB ? SrgbLinear : Srgb;
#endif
                        if (svgLinearGradientServer.GradientUnits == SvgCoordinateUnits.ObjectBoundingBox && (skBounds.Width == 0f || skBounds.Height == 0f))
                        {
                            if (fallbackServer is SvgColourServer svgColourServerFallback)
                            {
                                var skColor = GetColor(svgColourServerFallback, opacity, ignoreAttributes);
#if USE_COLORSPACE
                                var skColorShader = SKShader.CreateColor(skColor, skColorSpace);
#else
                                var skColorShader = SKShader.CreateColor(skColor);
#endif
                                if (skColorShader != null)
                                {
                                    disposable.Add(skColorShader);
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
#if USE_COLORSPACE
                            var skLinearGradientShader = CreateLinearGradient(svgLinearGradientServer, skBounds, svgVisualElement, opacity, ignoreAttributes, skColorSpace);
#else
                            var skLinearGradientShader = CreateLinearGradient(svgLinearGradientServer, skBounds, svgVisualElement, opacity, ignoreAttributes);
#endif
                            if (skLinearGradientShader != null)
                            {
//#if USE_COLORSPACE
//                                if (!isLinearRGB)
//                                {
//                                    var skColorFilter = SKColorFilter.CreateTable(null, s_SRGBtoLinearRGB, s_SRGBtoLinearRGB, s_SRGBtoLinearRGB);
//                                    disposable.Add(skColorFilter);
//                                    skPaint.ColorFilter = skColorFilter;
//                                }
//#endif
                                disposable.Add(skLinearGradientShader);
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
#if USE_COLORSPACE
                        var colorInterpolation = GetColorInterpolation(svgRadialGradientServer);
                        var isLinearRGB = colorInterpolation == SvgColourInterpolation.LinearRGB;
                        var skColorSpace = isLinearRGB ? SrgbLinear : Srgb;
#endif
                        if (svgRadialGradientServer.GradientUnits == SvgCoordinateUnits.ObjectBoundingBox && (skBounds.Width == 0f || skBounds.Height == 0f))
                        {
                            if (fallbackServer is SvgColourServer svgColourServerFallback)
                            {
                                var skColor = GetColor(svgColourServerFallback, opacity, ignoreAttributes);
#if USE_COLORSPACE
                                var skColorShader = SKShader.CreateColor(skColor, skColorSpace);
#else
                                var skColorShader = SKShader.CreateColor(skColor);
#endif
                                if (skColorShader != null)
                                {
                                    disposable.Add(skColorShader);
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
#if USE_COLORSPACE
                            var skRadialGradientShader = CreateTwoPointConicalGradient(svgRadialGradientServer, skBounds, svgVisualElement, opacity, ignoreAttributes, skColorSpace);
#else
                            var skRadialGradientShader = CreateTwoPointConicalGradient(svgRadialGradientServer, skBounds, svgVisualElement, opacity, ignoreAttributes);
#endif
                            if (skRadialGradientShader != null)
                            {
//#if USE_COLORSPACE
//                                if (!isLinearRGB)
//                                {
//                                    var skColorFilter = SKColorFilter.CreateTable(null, s_SRGBtoLinearRGB, s_SRGBtoLinearRGB, s_SRGBtoLinearRGB);
//                                    disposable.Add(skColorFilter);
//                                    skPaint.ColorFilter = skColorFilter;
//                                }
//#endif
                                disposable.Add(skRadialGradientShader);
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
                    return SetColorOrShader(svgVisualElement, svgDeferredPaintServer, opacity, skBounds, skPaint, forStroke, ignoreAttributes, disposable);
                default:
                    // Do not draw element.
                    return false;
            }
            return true;
        }

        public static void SetDash(SvgVisualElement svgVisualElement, SKPaint skPaint, SKRect skBounds, CompositeDisposable disposable)
        {
            var skPathEffect = CreateDash(svgVisualElement, skBounds);
            if (skPathEffect != null)
            {
                disposable.Add(skPathEffect);
                skPaint.PathEffect = skPathEffect;
            }
        }

        public static bool IsAntialias(SvgElement svgElement)
        {
            switch (svgElement.ShapeRendering)
            {
                case SvgShapeRendering.Inherit:
                case SvgShapeRendering.Auto:
                case SvgShapeRendering.GeometricPrecision:
                default:
                    return true;
                case SvgShapeRendering.OptimizeSpeed:
                case SvgShapeRendering.CrispEdges:
                    return false;
            }
        }

        public static bool IsValidFill(SvgElement svgElement)
        {
            var fill = svgElement.Fill;
            return fill != null
                && fill != SvgPaintServer.None;
        }

        public static bool IsValidStroke(SvgElement svgElement, SKRect skBounds)
        {
            var stroke = svgElement.Stroke;
            var strokeWidth = svgElement.StrokeWidth;
            return stroke != null
                && stroke != SvgPaintServer.None
                && strokeWidth.ToDeviceValue(UnitRenderingType.Other, svgElement, skBounds) > 0f;
        }

        public static SKPaint? GetFillSKPaint(SvgVisualElement svgVisualElement, SKRect skBounds, Attributes ignoreAttributes, CompositeDisposable disposable)
        {
            var skPaint = new SKPaint()
            {
                IsAntialias = IsAntialias(svgVisualElement),
                Style = SKPaintStyle.Fill
            };

            var server = svgVisualElement.Fill;
            var opacity = AdjustSvgOpacity(svgVisualElement.FillOpacity);
            if (SetColorOrShader(svgVisualElement, server, opacity, skBounds, skPaint, forStroke: false, ignoreAttributes, disposable) == false)
            {
                return null;
            }

            disposable.Add(skPaint);
            return skPaint;
        }

        public static SKPaint? GetStrokeSKPaint(SvgVisualElement svgVisualElement, SKRect skBounds, Attributes ignoreAttributes, CompositeDisposable disposable)
        {
            var skPaint = new SKPaint()
            {
                IsAntialias = IsAntialias(svgVisualElement),
                Style = SKPaintStyle.Stroke
            };

            var server = svgVisualElement.Stroke;
            var opacity = AdjustSvgOpacity(svgVisualElement.StrokeOpacity);
            if (SetColorOrShader(svgVisualElement, server, opacity, skBounds, skPaint, forStroke: true, ignoreAttributes, disposable) == false)
            {
                return null;
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
            if (strokeDashArray != null)
            {
                SetDash(svgVisualElement, skPaint, skBounds, disposable);
            }

            disposable.Add(skPaint);
            return skPaint;
        }

        public static SKPaint? GetOpacitySKPaint(float opacity)
        {
            if (opacity < 1f)
            {
                var skPaint = new SKPaint()
                {
                    IsAntialias = true,
                    Color = new SKColor(255, 255, 255, (byte)Math.Round(opacity * 255)),
                    Style = SKPaintStyle.StrokeAndFill
                };
                return skPaint;
            }
            return null;
        }

        public static SKPaint? GetOpacitySKPaint(SvgElement svgElement, CompositeDisposable disposable)
        {
            float opacity = AdjustSvgOpacity(svgElement.Opacity);
            var skPaint = GetOpacitySKPaint(opacity);
            if (skPaint != null)
            {
                disposable.Add(skPaint);
                return skPaint;
            }
            return null;
        }
    }
}
