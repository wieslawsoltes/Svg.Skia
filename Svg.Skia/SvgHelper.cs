// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using SkiaSharp;
using Svg.Document_Structure;
using Svg.FilterEffects;
using Svg.Pathing;
using Svg.Transforms;

namespace Svg.Skia
{
    internal static class SvgHelper
    {
        internal static T GetReference<T>(SvgElement svgElement, Uri uri) where T : SvgElement
        {
            if (uri == null)
            {
                return null;
            }

            var svgElementById = svgElement.OwnerDocument?.GetElementById(uri.ToString());
            if (svgElementById != null)
            {
                return svgElementById as T;
            }

            return null;
        }

        internal static SKColor GetColor(SvgColourServer svgColourServer, float opacity, bool forStroke = false)
        {
            if (svgColourServer == SvgPaintServer.None)
            {
                return SKColors.Transparent;
            }

            if (svgColourServer == SvgColourServer.NotSet && forStroke)
            {
                return SKColors.Transparent;
            }

            var colour = svgColourServer.Colour;
            byte alpha = (byte)Math.Round((opacity * (svgColourServer.Colour.A / 255.0)) * 255);

            return new SKColor(colour.R, colour.G, colour.B, alpha);
        }

        internal static string ToSvgPathData(SvgPathSegmentList svgPathSegmentList)
        {
            var sb = new StringBuilder();
            foreach (var svgSegment in svgPathSegmentList)
            {
                sb.AppendLine(svgSegment.ToString());
            }
            return sb.ToString();
        }

        internal static SKPath ToSKPath(string svgPath)
        {
            return SKPath.ParseSvgPathData(svgPath);
        }

        internal static SKPath ToSKPath(SvgPathSegmentList svgPathSegmentList, SvgFillRule svgFillRule)
        {
            var skPath = new SKPath()
            {
                FillType = (svgFillRule == SvgFillRule.EvenOdd) ? SKPathFillType.EvenOdd : SKPathFillType.Winding
            };

            foreach (var svgSegment in svgPathSegmentList)
            {
                switch (svgSegment)
                {
                    case SvgMoveToSegment svgMoveToSegment:
                        {
                            float x = (float)svgMoveToSegment.Start.X;
                            float y = (float)svgMoveToSegment.Start.Y;
                            skPath.MoveTo(x, y);
                        }
                        break;
                    case SvgLineSegment svgLineSegment:
                        {
                            float x = (float)svgLineSegment.End.X;
                            float y = (float)svgLineSegment.End.Y;
                            skPath.LineTo(x, y);
                        }
                        break;
                    case SvgCubicCurveSegment svgCubicCurveSegment:
                        {
                            float x0 = (float)svgCubicCurveSegment.FirstControlPoint.X;
                            float y0 = (float)svgCubicCurveSegment.FirstControlPoint.Y;
                            float x1 = (float)svgCubicCurveSegment.SecondControlPoint.X;
                            float y1 = (float)svgCubicCurveSegment.SecondControlPoint.Y;
                            float x2 = (float)svgCubicCurveSegment.End.X;
                            float y2 = (float)svgCubicCurveSegment.End.Y;
                            skPath.CubicTo(x0, y0, x1, y1, x2, y2);
                        }
                        break;
                    case SvgQuadraticCurveSegment svgQuadraticCurveSegment:
                        {
                            float x0 = (float)svgQuadraticCurveSegment.ControlPoint.X;
                            float y0 = (float)svgQuadraticCurveSegment.ControlPoint.Y;
                            float x1 = (float)svgQuadraticCurveSegment.End.X;
                            float y1 = (float)svgQuadraticCurveSegment.End.Y;
                            skPath.QuadTo(x0, y0, x1, y1);
                        }
                        break;
                    case SvgArcSegment svgArcSegment:
                        {
                            float rx = svgArcSegment.RadiusX;
                            float ry = svgArcSegment.RadiusY;
                            float xAxisRotate = (float)(svgArcSegment.Angle * Math.PI / 180.0);
                            var largeArc = svgArcSegment.Size == SvgArcSize.Small ? SKPathArcSize.Small : SKPathArcSize.Large;
                            var sweep = svgArcSegment.Sweep == SvgArcSweep.Negative ? SKPathDirection.CounterClockwise : SKPathDirection.Clockwise;
                            float x = (float)svgArcSegment.End.X;
                            float y = (float)svgArcSegment.End.Y;
                            skPath.ArcTo(rx, ry, xAxisRotate, largeArc, sweep, x, y);
                        }
                        break;
                    case SvgClosePathSegment svgClosePathSegment:
                        {
                            skPath.Close();
                        }
                        break;
                }
            }

            return skPath;
        }

        internal static SKPath ToSKPath(SvgPointCollection svgPointCollection, SvgFillRule svgFillRule, bool isClosed)
        {
            var skPath = new SKPath()
            {
                FillType = (svgFillRule == SvgFillRule.EvenOdd) ? SKPathFillType.EvenOdd : SKPathFillType.Winding
            };

            var skPoints = new SKPoint[svgPointCollection.Count / 2];

            for (int i = 0; (i + 1) < svgPointCollection.Count; i += 2)
            {
                float x = (float)svgPointCollection[i];
                float y = (float)svgPointCollection[i + 1];
                skPoints[i / 2] = new SKPoint(x, y);
            }

            skPath.AddPoly(skPoints, false);

            if (isClosed)
            {
                skPath.Close();
            }

            return skPath;
        }

        internal static float AdjustSvgOpacity(float opacity)
        {
            return Math.Min(Math.Max(opacity, 0), 1);
        }

        internal static SvgUnit NormalizeSvgUnit(SvgUnit svgUnit, SvgCoordinateUnits svgCoordinateUnits)
        {
            return svgUnit.Type == SvgUnitType.Percentage && svgCoordinateUnits == SvgCoordinateUnits.ObjectBoundingBox ?
                    new SvgUnit(SvgUnitType.User, svgUnit.Value / 100) : svgUnit;
        }

        internal static SKPathEffect CreateDash(SvgElement svgElement, float strokeWidth)
        {
            var strokeDashArray = svgElement.StrokeDashArray;
            var count = strokeDashArray.Count;

            if (strokeDashArray != null && count > 0)
            {
                bool isOdd = count % 2 != 0;

                strokeWidth = strokeWidth <= 0 ? 1 : strokeWidth;

                float[] intervals = new float[isOdd ? count * 2 : count];
                for (int i = 0; i < count; i++)
                {
                    var dash = strokeDashArray[i].ToDeviceValue(null, UnitRenderingType.Other, svgElement);
                    var interval = (dash <= 0) ? 1 : dash;
                    intervals[i] = interval / strokeWidth;
                }

                if (isOdd)
                {
                    for (int i = 0; i < count; i++)
                    {
                        intervals[i + count] = intervals[i];
                    }
                }

                var dashOffset = svgElement.StrokeDashOffset != null ? svgElement.StrokeDashOffset : 0;
                var phase = 0f;
                if (dashOffset != 0)
                {
                    var dashOffsetValue = dashOffset.ToDeviceValue(null, UnitRenderingType.Other, svgElement);
                    phase = (dashOffsetValue <= 0) ? 1 : dashOffsetValue / strokeWidth;
                }

                return SKPathEffect.CreateDash(intervals, phase);
            }

            return null;
        }

        internal static void GetStops(SvgGradientServer svgGradientServer, SKSize skSize, List<SKColor> colors, List<float> colorPos, SvgVisualElement svgVisualElement)
        {
            foreach (var child in svgGradientServer.Children)
            {
                if (child is SvgGradientStop svgGradientStop)
                {
                    if (svgGradientStop.StopColor is SvgColourServer stopColorSvgColourServer)
                    {
                        var stopColor = GetColor(stopColorSvgColourServer, AdjustSvgOpacity(svgGradientStop.Opacity), false);
                        float offset = svgGradientStop.Offset.ToDeviceValue(null, UnitRenderingType.Horizontal, svgGradientServer);
                        offset /= skSize.Width;
                        colors.Add(stopColor);
                        colorPos.Add(offset);
                    }
                }
            }


            var inheritGradient = SvgDeferredPaintServer.TryGet<SvgGradientServer>(svgGradientServer.InheritGradient, svgVisualElement);
            if (colors.Count == 0 && inheritGradient != null)
            {
                GetStops(inheritGradient, skSize, colors, colorPos, svgVisualElement);
            }

        }

        internal static SKShader CreateLinearGradient(SvgLinearGradientServer svgLinearGradientServer, SKSize skSize, SKRect skBounds, SvgVisualElement svgVisualElement)
        {
            var start = SvgUnit.GetDevicePoint(
                NormalizeSvgUnit(svgLinearGradientServer.X1, svgLinearGradientServer.GradientUnits),
                NormalizeSvgUnit(svgLinearGradientServer.Y1, svgLinearGradientServer.GradientUnits),
                null,
                svgLinearGradientServer);
            var end = SvgUnit.GetDevicePoint(
                NormalizeSvgUnit(svgLinearGradientServer.X2, svgLinearGradientServer.GradientUnits),
                NormalizeSvgUnit(svgLinearGradientServer.Y2, svgLinearGradientServer.GradientUnits),
                null,
                svgLinearGradientServer);

            var colors = new List<SKColor>();
            var colorPos = new List<float>();

            GetStops(svgLinearGradientServer, skSize, colors, colorPos, svgVisualElement);

            SKShaderTileMode shaderTileMode;
            switch (svgLinearGradientServer.SpreadMethod)
            {
                default:
                case SvgGradientSpreadMethod.Pad:
                    shaderTileMode = SKShaderTileMode.Clamp;
                    break;
                case SvgGradientSpreadMethod.Reflect:
                    shaderTileMode = SKShaderTileMode.Mirror;
                    break;
                case SvgGradientSpreadMethod.Repeat:
                    shaderTileMode = SKShaderTileMode.Repeat;
                    break;
            }

            SKPoint skStart = new SKPoint(start.X, start.Y);
            SKPoint skEnd = new SKPoint(end.X, end.Y);
            var skColors = colors.ToArray();
            float[] skColorPos = colorPos.ToArray();

            SKShader sKShader;

            if (svgLinearGradientServer.GradientUnits == SvgCoordinateUnits.ObjectBoundingBox)
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

                if (svgLinearGradientServer.GradientTransform != null && svgLinearGradientServer.GradientTransform.Count > 0)
                {
                    var gradientTransform = GetSKMatrix(svgLinearGradientServer.GradientTransform);
                    SKMatrix.Concat(ref skBoundingBoxTransform, ref skBoundingBoxTransform, ref gradientTransform);
                }

                sKShader = SKShader.CreateLinearGradient(skStart, skEnd, skColors, skColorPos, shaderTileMode, skBoundingBoxTransform);
            }
            else
            {
                if (svgLinearGradientServer.GradientTransform != null && svgLinearGradientServer.GradientTransform.Count > 0)
                {
                    var gradientTransform = GetSKMatrix(svgLinearGradientServer.GradientTransform);
                    sKShader = SKShader.CreateLinearGradient(skStart, skEnd, skColors, skColorPos, shaderTileMode, gradientTransform);
                }
                else
                {
                    sKShader = SKShader.CreateLinearGradient(skStart, skEnd, skColors, skColorPos, shaderTileMode);
                }
            }

            return sKShader;
        }

        internal static SKShader CreateTwoPointConicalGradient(SvgRadialGradientServer svgRadialGradientServer, SKSize skSize, SKRect skBounds, SvgVisualElement svgVisualElement)
        {
            var skStart = new SKPoint(
                NormalizeSvgUnit(svgRadialGradientServer.CenterX, svgRadialGradientServer.GradientUnits)
                    .ToDeviceValue(null, UnitRenderingType.Horizontal, svgRadialGradientServer),
                NormalizeSvgUnit(svgRadialGradientServer.CenterY, svgRadialGradientServer.GradientUnits)
                    .ToDeviceValue(null, UnitRenderingType.Vertical, svgRadialGradientServer));
            var skEnd = new SKPoint(
                NormalizeSvgUnit(svgRadialGradientServer.FocalX, svgRadialGradientServer.GradientUnits)
                    .ToDeviceValue(null, UnitRenderingType.Horizontal, svgRadialGradientServer),
                NormalizeSvgUnit(svgRadialGradientServer.FocalY, svgRadialGradientServer.GradientUnits)
                    .ToDeviceValue(null, UnitRenderingType.Vertical, svgRadialGradientServer));

            var startRadius = 0f;
            var endRadius =
                NormalizeSvgUnit(svgRadialGradientServer.Radius, svgRadialGradientServer.GradientUnits)
                    .ToDeviceValue(null, UnitRenderingType.Other, svgRadialGradientServer);

            var colors = new List<SKColor>();
            var colorPos = new List<float>();

            GetStops(svgRadialGradientServer, skSize, colors, colorPos, svgVisualElement);

            SKShaderTileMode shaderTileMode;
            switch (svgRadialGradientServer.SpreadMethod)
            {
                default:
                case SvgGradientSpreadMethod.Pad:
                    shaderTileMode = SKShaderTileMode.Clamp;
                    break;
                case SvgGradientSpreadMethod.Reflect:
                    shaderTileMode = SKShaderTileMode.Mirror;
                    break;
                case SvgGradientSpreadMethod.Repeat:
                    shaderTileMode = SKShaderTileMode.Repeat;
                    break;
            }

            var skColors = colors.ToArray();
            float[] skColorPos = colorPos.ToArray();

            SKShader sKShader;

            if (svgRadialGradientServer.GradientUnits == SvgCoordinateUnits.ObjectBoundingBox)
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

                if (svgRadialGradientServer.GradientTransform != null && svgRadialGradientServer.GradientTransform.Count > 0)
                {
                    var gradientTransform = GetSKMatrix(svgRadialGradientServer.GradientTransform);
                    SKMatrix.Concat(ref skBoundingBoxTransform, ref skBoundingBoxTransform, ref gradientTransform);
                }

                sKShader = SKShader.CreateTwoPointConicalGradient(skStart, startRadius, skEnd, endRadius, skColors, skColorPos, shaderTileMode, skBoundingBoxTransform);
            }
            else
            {
                if (svgRadialGradientServer.GradientTransform != null && svgRadialGradientServer.GradientTransform.Count > 0)
                {
                    var gradientTransform = GetSKMatrix(svgRadialGradientServer.GradientTransform);
                    sKShader = SKShader.CreateTwoPointConicalGradient(skStart, startRadius, skEnd, endRadius, skColors, skColorPos, shaderTileMode, gradientTransform);
                }
                else
                {
                    sKShader = SKShader.CreateTwoPointConicalGradient(skStart, startRadius, skEnd, endRadius, skColors, skColorPos, shaderTileMode);
                }
            }

            return sKShader;
        }

        internal static void SetFill(SvgVisualElement svgVisualElement, SKSize skSize, SKRect skBounds, SKPaint skPaint, CompositeDisposable disposable)
        {
            switch (svgVisualElement.Fill)
            {
                case SvgColourServer svgColourServer:
                    {
                        skPaint.Color = GetColor(svgColourServer, AdjustSvgOpacity(svgVisualElement.FillOpacity), false);
                    }
                    break;
                case SvgPatternServer svgPatternServer:
                    {
                        // TODO:
                    }
                    break;
                case SvgLinearGradientServer svgLinearGradientServer:
                    {
                        var skShader = CreateLinearGradient(svgLinearGradientServer, skSize, skBounds, svgVisualElement);
                        if (skShader != null)
                        {
                            disposable.Add(skShader);
                            skPaint.Shader = skShader;
                        }
                    }
                    break;
                case SvgRadialGradientServer svgRadialGradientServer:
                    {
                        var skShader = CreateTwoPointConicalGradient(svgRadialGradientServer, skSize, skBounds, svgVisualElement);
                        if (skShader != null)
                        {
                            disposable.Add(skShader);
                            skPaint.Shader = skShader;
                        }
                    }
                    break;
                case SvgDeferredPaintServer svgDeferredPaintServer:
                    // Not used.
                    break;
                case SvgFallbackPaintServer svgFallbackPaintServer:
                    // Not used.
                    break;
                default:
                    break;
            }
        }

        internal static void SetStroke(SvgVisualElement svgVisualElement, SKSize skSize, SKRect skBounds, SKPaint skPaint, CompositeDisposable disposable)
        {
            switch (svgVisualElement.Stroke)
            {
                case SvgColourServer svgColourServer:
                    {
                        skPaint.Color = GetColor(svgColourServer, AdjustSvgOpacity(svgVisualElement.StrokeOpacity), true);
                    }
                    break;
                case SvgPatternServer svgPatternServer:
                    {
                        // TODO:
                    }
                    break;
                case SvgLinearGradientServer svgLinearGradientServer:
                    {
                        var skShader = CreateLinearGradient(svgLinearGradientServer, skSize, skBounds, svgVisualElement);
                        if (skShader != null)
                        {
                            disposable.Add(skShader);
                            skPaint.Shader = skShader;
                        }
                    }
                    break;
                case SvgRadialGradientServer svgRadialGradientServer:
                    {
                        var skShader = CreateTwoPointConicalGradient(svgRadialGradientServer, skSize, skBounds, svgVisualElement);
                        if (skShader != null)
                        {
                            disposable.Add(skShader);
                            skPaint.Shader = skShader;
                        }
                    }
                    break;
                case SvgDeferredPaintServer svgDeferredPaintServer:
                    {
                        // Not used.
                    }
                    break;
                case SvgFallbackPaintServer svgFallbackPaintServer:
                    {
                        // Not used.
                    }
                    break;
                default:
                    break;
            }
        }

        internal static void SetDash(SvgVisualElement svgVisualElement, SKPaint skPaint, CompositeDisposable disposable)
        {
            var dash = CreateDash(svgVisualElement, skPaint.StrokeWidth);
            if (dash != null)
            {
                disposable.Add(dash);
                skPaint.PathEffect = dash;
            }
        }

        internal static SKColorFilter CreateColorMatrix(SvgColourMatrix svgColourMatrix, SvgVisualElement svgVisualElement)
        {
            float[] matrix;

            switch (svgColourMatrix.Type)
            {
                case SvgColourMatrixType.HueRotate:
                    {
                        float value = (string.IsNullOrEmpty(svgColourMatrix.Values) ? 0 : float.Parse(svgColourMatrix.Values, NumberStyles.Any, CultureInfo.InvariantCulture));
                        // TODO:
                        matrix = new float[]
                        {
                            (float)(0.213 + Math.Cos(value) * +0.787 + Math.Sin(value) * -0.213),
                            (float)(0.715 + Math.Cos(value) * -0.715 + Math.Sin(value) * -0.715),
                            (float)(0.072 + Math.Cos(value) * -0.072 + Math.Sin(value) * +0.928), 0, 0,
                            (float)(0.213 + Math.Cos(value) * -0.213 + Math.Sin(value) * +0.143),
                            (float)(0.715 + Math.Cos(value) * +0.285 + Math.Sin(value) * +0.140),
                            (float)(0.072 + Math.Cos(value) * -0.072 + Math.Sin(value) * -0.283), 0, 0,
                            (float)(0.213 + Math.Cos(value) * -0.213 + Math.Sin(value) * -0.787),
                            (float)(0.715 + Math.Cos(value) * -0.715 + Math.Sin(value) * +0.715),
                            (float)(0.072 + Math.Cos(value) * +0.928 + Math.Sin(value) * +0.072), 0, 0,
                            0, 0, 0, 1, 0
                        };
                    }
                    break;
                case SvgColourMatrixType.LuminanceToAlpha:
                    {
                        // TODO:
                        matrix = new float[]
                        {
                            0, 0, 0, 0, 0,
                            0, 0, 0, 0, 0,
                            0, 0, 0, 0, 0,
                            0.2125f, 0.7154f, 0.0721f, 0, 0
                        };
                    }
                    break;
                case SvgColourMatrixType.Saturate:
                    {
                        float value = (string.IsNullOrEmpty(svgColourMatrix.Values) ? 1 : float.Parse(svgColourMatrix.Values, NumberStyles.Any, CultureInfo.InvariantCulture));
                        // TODO:
                        matrix = new float[]
                        {
                            (float)(0.213+0.787*value), (float)(0.715-0.715*value), (float)(0.072-0.072*value), 0, 0,
                            (float)(0.213-0.213*value), (float)(0.715+0.285*value), (float)(0.072-0.072*value), 0, 0,
                            (float)(0.213-0.213*value), (float)(0.715-0.715*value), (float)(0.072+0.928*value), 0, 0,
                            0, 0, 0, 1, 0
                        };
                    };
                    break;
                default:
                case SvgColourMatrixType.Matrix:
                    {
                        var parts = svgColourMatrix.Values.Split(new char[] { ' ', '\t', '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        matrix = new float[20];
                        for (int i = 0; i < 20; i++)
                        {
                            matrix[i] = float.Parse(parts[i], NumberStyles.Any, CultureInfo.InvariantCulture);
                        }
                    }
                    break;
            }

            return SKColorFilter.CreateColorMatrix(matrix);
        }

        internal static SKImageFilter CreateBlur(SvgGaussianBlur svgGaussianBlur, SvgVisualElement svgVisualElement)
        {
            // TODO:
            var sigma = svgGaussianBlur.StdDeviation;
            return SKImageFilter.CreateBlur(sigma, sigma);
        }

        internal static void SetFilter(SvgVisualElement svgVisualElement, SKPaint skPaint, CompositeDisposable disposable)
        {
            var svgFilter = GetReference<SvgFilter>(svgVisualElement, svgVisualElement.Filter);
            if (svgFilter == null)
            {
                return;
            }

            foreach (var child in svgFilter.Children)
            {
                if (child is SvgFilterPrimitive svgFilterPrimitive)
                {
                    switch (svgFilterPrimitive)
                    {
                        case SvgColourMatrix svgColourMatrix:
                            {
                                var skColorFilter = CreateColorMatrix(svgColourMatrix, svgVisualElement);
                                if (skColorFilter != null)
                                {
                                    disposable.Add(skColorFilter);
                                    skPaint.ColorFilter = skColorFilter;
                                }
                            }
                            break;
                        case SvgGaussianBlur svgGaussianBlur:
                            {
                                var skImageFilter = CreateBlur(svgGaussianBlur, svgVisualElement);
                                if (skImageFilter != null)
                                {
                                    disposable.Add(skImageFilter);
                                    skPaint.ImageFilter = skImageFilter;
                                }
                            }
                            break;
                        case SvgMerge svgMerge:
                            {
                                // TODO:
                            }
                            break;
                        case SvgOffset svgOffset:
                            {
                                // TODO:
                            }
                            break;
                        default:
                            {
                                // TODO:
                            }
                            break;
                    }
                }
            }
        }

        internal static SKPaint SetFilter(SKCanvas skCanvas, SvgVisualElement svgVisualElement, CompositeDisposable disposable)
        {
            if (svgVisualElement.Filter != null)
            {
                var paint = new SKPaint();
                paint.Style = SKPaintStyle.StrokeAndFill;
                SetFilter(svgVisualElement, paint, disposable);
                skCanvas.SaveLayer(paint);
                disposable.Add(paint);
                return paint;
            }
            return null;
        }

        internal static bool IsAntialias(SvgElement svgElement)
        {
            switch (svgElement.ShapeRendering)
            {
                case SvgShapeRendering.Inherit:
                case SvgShapeRendering.Auto:
                default:
                    return true;
                case SvgShapeRendering.OptimizeSpeed:
                case SvgShapeRendering.CrispEdges:
                case SvgShapeRendering.GeometricPrecision:
                    return false;
            }
        }

        internal static SKPaint GetFillSKPaint(SvgVisualElement svgVisualElement, SKSize skSize, SKRect skBounds, CompositeDisposable disposable)
        {
            var skPaint = new SKPaint()
            {
                IsAntialias = IsAntialias(svgVisualElement)
            };

            // TODO: SvgElement

            // TODO: SvgElementStyle

            if (svgVisualElement.Fill != null)
            {
                SetFill(svgVisualElement, skSize, skBounds, skPaint, disposable);
            }

            // TODO: SvgVisualElement

            if (svgVisualElement.Filter != null)
            {
                SetFilter(svgVisualElement, skPaint, disposable);
            }

            // TODO: SvgVisualElementStyle

            skPaint.Style = SKPaintStyle.Fill;

            return skPaint;
        }

        internal static SKPaint GetStrokeSKPaint(SvgVisualElement svgVisualElement, SKSize skSize, SKRect skBounds, CompositeDisposable disposable)
        {
            var skPaint = new SKPaint()
            {
                IsAntialias = IsAntialias(svgVisualElement)
            };

            // TODO: SvgElement

            // TODO: SvgElementStyle

            if (svgVisualElement.Stroke != null)
            {
                SetStroke(svgVisualElement, skSize, skBounds, skPaint, disposable);
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

            skPaint.StrokeWidth = svgVisualElement.StrokeWidth.ToDeviceValue(null, UnitRenderingType.Other, svgVisualElement);

            if (svgVisualElement.StrokeDashArray != null)
            {
                SetDash(svgVisualElement, skPaint, disposable);
            }

            // TODO: SvgVisualElement

            if (svgVisualElement.Filter != null)
            {
                SetFilter(svgVisualElement, skPaint, disposable);
            }

            // TODO: SvgVisualElementStyle

            skPaint.Style = SKPaintStyle.Stroke;

            return skPaint;
        }

        internal static SKMatrix GetSKMatrix(SvgMatrix svgMatrix)
        {
            return new SKMatrix()
            {
                ScaleX = svgMatrix.Points[0],
                SkewY = svgMatrix.Points[1],
                SkewX = svgMatrix.Points[2],
                ScaleY = svgMatrix.Points[3],
                TransX = svgMatrix.Points[4],
                TransY = svgMatrix.Points[5],
                Persp0 = 0,
                Persp1 = 0,
                Persp2 = 1
            };
        }

        internal static SKMatrix GetSKMatrix(SvgTransformCollection svgTransformCollection)
        {
            var skMatrixTotal = SKMatrix.MakeIdentity();

            foreach (var svgTransform in svgTransformCollection)
            {
                switch (svgTransform)
                {
                    case SvgMatrix svgMatrix:
                        {
                            var skMatrix = GetSKMatrix(svgMatrix);
                            SKMatrix.Concat(ref skMatrixTotal, ref skMatrixTotal, ref skMatrix);
                        }
                        break;
                    case SvgRotate svgRotate:
                        {
                            var skMatrixRotate = SKMatrix.MakeRotationDegrees(svgRotate.Angle, svgRotate.CenterX, svgRotate.CenterY);
                            SKMatrix.Concat(ref skMatrixTotal, ref skMatrixTotal, ref skMatrixRotate);
                        }
                        break;
                    case SvgScale svgScale:
                        {
                            var skMatrixScale = SKMatrix.MakeScale(svgScale.X, svgScale.Y);
                            SKMatrix.Concat(ref skMatrixTotal, ref skMatrixTotal, ref skMatrixScale);
                        }
                        break;
                    case SvgShear svgShear:
                        {
                            // Not in the svg specification.
                        }
                        break;
                    case SvgSkew svgSkew:
                        {
                            var skMatrixSkew = SKMatrix.MakeSkew(svgSkew.AngleX, svgSkew.AngleY);
                            SKMatrix.Concat(ref skMatrixTotal, ref skMatrixTotal, ref skMatrixSkew);
                        }
                        break;
                    case SvgTranslate svgTranslate:
                        {
                            var skMatrixTranslate = SKMatrix.MakeTranslation(svgTranslate.X, svgTranslate.Y);
                            SKMatrix.Concat(ref skMatrixTotal, ref skMatrixTotal, ref skMatrixTranslate);
                        }
                        break;
                }
            }

            return skMatrixTotal;
        }

        internal static SKMatrix GetSvgViewBoxTransform(SvgViewBox svgViewBox, SvgAspectRatio svgAspectRatio, float x, float y, float width, float height)
        {
            if (svgViewBox.Equals(SvgViewBox.Empty))
            {
                return SKMatrix.MakeTranslation(x, y);
            }

            float fScaleX = width / svgViewBox.Width;
            float fScaleY = height / svgViewBox.Height;
            float fMinX = -svgViewBox.MinX * fScaleX;
            float fMinY = -svgViewBox.MinY * fScaleY;

            if (svgAspectRatio == null)
            {
                svgAspectRatio = new SvgAspectRatio(SvgPreserveAspectRatio.xMidYMid, false);
            }

            if (svgAspectRatio.Align != SvgPreserveAspectRatio.none)
            {
                if (svgAspectRatio.Slice)
                {
                    fScaleX = Math.Max(fScaleX, fScaleY);
                    fScaleY = Math.Max(fScaleX, fScaleY);
                }
                else
                {
                    fScaleX = Math.Min(fScaleX, fScaleY);
                    fScaleY = Math.Min(fScaleX, fScaleY);
                }
                float fViewMidX = (svgViewBox.Width / 2) * fScaleX;
                float fViewMidY = (svgViewBox.Height / 2) * fScaleY;
                float fMidX = width / 2;
                float fMidY = height / 2;
                fMinX = -svgViewBox.MinX * fScaleX;
                fMinY = -svgViewBox.MinY * fScaleY;

                switch (svgAspectRatio.Align)
                {
                    case SvgPreserveAspectRatio.xMinYMin:
                        break;
                    case SvgPreserveAspectRatio.xMidYMin:
                        fMinX += fMidX - fViewMidX;
                        break;
                    case SvgPreserveAspectRatio.xMaxYMin:
                        fMinX += width - svgViewBox.Width * fScaleX;
                        break;
                    case SvgPreserveAspectRatio.xMinYMid:
                        fMinY += fMidY - fViewMidY;
                        break;
                    case SvgPreserveAspectRatio.xMidYMid:
                        fMinX += fMidX - fViewMidX;
                        fMinY += fMidY - fViewMidY;
                        break;
                    case SvgPreserveAspectRatio.xMaxYMid:
                        fMinX += width - svgViewBox.Width * fScaleX;
                        fMinY += fMidY - fViewMidY;
                        break;
                    case SvgPreserveAspectRatio.xMinYMax:
                        fMinY += height - svgViewBox.Height * fScaleY;
                        break;
                    case SvgPreserveAspectRatio.xMidYMax:
                        fMinX += fMidX - fViewMidX;
                        fMinY += height - svgViewBox.Height * fScaleY;
                        break;
                    case SvgPreserveAspectRatio.xMaxYMax:
                        fMinX += width - svgViewBox.Width * fScaleX;
                        fMinY += height - svgViewBox.Height * fScaleY;
                        break;
                    default:
                        break;
                }
            }

            var skMatrixTotal = SKMatrix.MakeIdentity();

            var skMatrixXY = SKMatrix.MakeTranslation(x, y);
            SKMatrix.Concat(ref skMatrixTotal, ref skMatrixTotal, ref skMatrixXY);

            var skMatrixMinXY = SKMatrix.MakeTranslation(fMinX, fMinY);
            SKMatrix.Concat(ref skMatrixTotal, ref skMatrixTotal, ref skMatrixMinXY);

            var skMatrixScale = SKMatrix.MakeScale(fScaleX, fScaleY);
            SKMatrix.Concat(ref skMatrixTotal, ref skMatrixTotal, ref skMatrixScale);

            return skMatrixTotal;
        }

        internal static void SetTransform(SKCanvas skCanvas, SKMatrix skMatrix)
        {
            var skMatrixTotal = skCanvas.TotalMatrix;
            SKMatrix.Concat(ref skMatrixTotal, ref skMatrixTotal, ref skMatrix);
            skCanvas.SetMatrix(skMatrixTotal);
        }

        internal static SKPaint SetOpacity(SKCanvas skCanvas, SvgElement svgElement, CompositeDisposable disposable)
        {
            float opacity = AdjustSvgOpacity(svgElement.Opacity);
            if (opacity < 1f)
            {
                var paint = new SKPaint()
                {
                    IsAntialias = true,
                };
                paint.Color = new SKColor(255, 255, 255, (byte)Math.Round(opacity * 255));
                paint.Style = SKPaintStyle.StrokeAndFill;
                skCanvas.SaveLayer(paint);
                disposable.Add(paint);
                return paint;
            }
            return null;
        }

        internal static bool ElementReferencesUri(SvgUse svgUse, SvgElement element, List<Uri> elementUris)
        {
            if (element is SvgUse useElement)
            {
                if (elementUris.Contains(useElement.ReferencedElement))
                {
                    return true;
                }
                if (svgUse.OwnerDocument.GetElementById(useElement.ReferencedElement.ToString()) is SvgUse refElement)
                {
                    elementUris.Add(useElement.ReferencedElement);
                }
                return ReferencedElementReferencesUri(useElement, elementUris);
            }
            if (element is SvgGroup groupElement)
            {
                foreach (var child in groupElement.Children)
                {
                    if (ElementReferencesUri(svgUse, child, elementUris))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        internal static bool ReferencedElementReferencesUri(SvgUse svgUse, List<Uri> elementUris)
        {
            var refElement = svgUse.OwnerDocument.GetElementById(svgUse.ReferencedElement.ToString());
            return ElementReferencesUri(svgUse, refElement, elementUris);
        }

        internal static bool HasRecursiveReference(SvgUse svgUse)
        {
            var refElement = svgUse.OwnerDocument.GetElementById(svgUse.ReferencedElement.ToString());
            var uris = new List<Uri>() { svgUse.ReferencedElement };
            return ElementReferencesUri(svgUse, refElement, uris);
        }

        internal static void DrawSvgFragment(SKCanvas skCanvas, SKSize skSize, SvgFragment svgFragment, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var fragment = new Fragment(svgFragment);

            SetOpacity(skCanvas, svgFragment, disposable);
            SetTransform(skCanvas, fragment.matrix);

            DrawSvgElementCollection(skCanvas, skSize, svgFragment.Children, disposable);

            skCanvas.Restore();
        }

        internal static void DrawSvgSymbol(SKCanvas skCanvas, SKSize skSize, SvgSymbol svgSymbol, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var symbol = new Symbol(svgSymbol);

            SetOpacity(skCanvas, svgSymbol, disposable);
            SetTransform(skCanvas, symbol.matrix);

            DrawSvgElementCollection(skCanvas, skSize, svgSymbol.Children, disposable);

            skCanvas.Restore();
        }

        internal static void DrawSvgImage(SKCanvas skCanvas, SKSize skSize, SvgImage svgImage, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var image = new Image(svgImage);

            SetOpacity(skCanvas, svgImage, disposable);
            SetTransform(skCanvas, image.matrix);

            // TODO:

            skCanvas.Restore();
        }

        internal static void DrawSvgSwitch(SKCanvas skCanvas, SKSize skSize, SvgSwitch svgSwitch, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var @switch = new Switch(svgSwitch);

            SetOpacity(skCanvas, svgSwitch, disposable);
            SetTransform(skCanvas, @switch.matrix);

            // TODO:

            skCanvas.Restore();
        }

        internal static void DrawSvgUse(SKCanvas skCanvas, SKSize skSize, SvgUse svgUse, CompositeDisposable disposable)
        {
            var svgVisualElement = GetReference<SvgVisualElement>(svgUse, svgUse.ReferencedElement);
            if (svgVisualElement != null && !HasRecursiveReference(svgUse))
            {
                skCanvas.Save();

                var parent = svgUse.Parent;
                //svgVisualElement.Parent = svgUse;
                var _parent = svgUse.GetType().GetField("_parent", BindingFlags.NonPublic | BindingFlags.Instance);
                if (_parent != null)
                {
                    _parent.SetValue(svgVisualElement, svgUse);
                }
                //else
                //{
                //    throw new Exception("Can not set 'use' referenced element parent.");
                //}

                svgVisualElement.InvalidateChildPaths();

                var use = new Use(svgUse, svgVisualElement);

                SetOpacity(skCanvas, svgUse, disposable);
                SetFilter(skCanvas, svgUse, disposable);
                SetTransform(skCanvas, use.matrix);

                // TODO:
                //if (svgUse.ClipPath != null)
                //{
                //    var svgClipPath = svgVisualElement.OwnerDocument.GetElementById<SvgClipPath>(svgUse.ClipPath.ToString());
                //    if (svgClipPath != null && svgClipPath.Children != null)
                //    {
                //        foreach (var child in svgClipPath.Children)
                //        {
                //            var skPath = new SKPath();
                //        }
                //        // TODO:
                //        Console.WriteLine($"clip-path: {svgClipPath}");
                //    }
                //}

                if (svgVisualElement is SvgSymbol svgSymbol)
                {
                    DrawSvgSymbol(skCanvas, skSize, svgSymbol, disposable);
                }
                else
                {
                    DrawSvgElement(skCanvas, skSize, svgVisualElement, disposable);
                }

                //svgVisualElement.Parent = parent;
                if (_parent != null)
                {
                    _parent.SetValue(svgVisualElement, parent);
                }
                //else
                //{
                //    throw new Exception("Can not set 'use' referenced element parent.");
                //}

                skCanvas.Restore();
            }
        }

        internal static void DrawSvgForeignObject(SKCanvas skCanvas, SKSize skSize, SvgForeignObject svgForeignObject, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var foreignObject = new ForeignObject(svgForeignObject);

            SetOpacity(skCanvas, svgForeignObject, disposable);
            SetTransform(skCanvas, foreignObject.matrix);

            // TODO:

            skCanvas.Restore();
        }

        internal static void DrawSvgCircle(SKCanvas skCanvas, SKSize skSize, SvgCircle svgCircle, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var circle = new Circle(svgCircle);

            SetOpacity(skCanvas, svgCircle, disposable);
            SetTransform(skCanvas, circle.matrix);

            if (svgCircle.Fill != null)
            {
                using (var skPaint = GetFillSKPaint(svgCircle, skSize, circle.bounds, disposable))
                {
                    skCanvas.DrawCircle(circle.cx, circle.cy, circle.radius, skPaint);
                }
            }

            if (svgCircle.Stroke != null)
            {
                using (var skPaint = GetStrokeSKPaint(svgCircle, skSize, circle.bounds, disposable))
                {
                    skCanvas.DrawCircle(circle.cx, circle.cy, circle.radius, skPaint);
                }
            }

            skCanvas.Restore();
        }

        internal static void DrawSvgEllipse(SKCanvas skCanvas, SKSize skSize, SvgEllipse svgEllipse, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var ellipse = new Ellipse(svgEllipse);

            SetOpacity(skCanvas, svgEllipse, disposable);
            SetTransform(skCanvas, ellipse.matrix);

            if (svgEllipse.Fill != null)
            {
                using (var skPaint = GetFillSKPaint(svgEllipse, skSize, ellipse.bounds, disposable))
                {
                    skCanvas.DrawOval(ellipse.cx, ellipse.cy, ellipse.rx, ellipse.ry, skPaint);
                }
            }

            if (svgEllipse.Stroke != null)
            {
                using (var skPaint = GetStrokeSKPaint(svgEllipse, skSize, ellipse.bounds, disposable))
                {
                    skCanvas.DrawOval(ellipse.cx, ellipse.cy, ellipse.rx, ellipse.ry, skPaint);
                }
            }

            skCanvas.Restore();
        }

        internal static void DrawSvgRectangle(SKCanvas skCanvas, SKSize skSize, SvgRectangle svgRectangle, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var rectangle = new Rectangle(svgRectangle);

            SetOpacity(skCanvas, svgRectangle, disposable);
            SetTransform(skCanvas, rectangle.matrix);

            if (svgRectangle.Fill != null)
            {
                using (var skPaint = GetFillSKPaint(svgRectangle, skSize, rectangle.bounds, disposable))
                {
                    if (rectangle.isRound)
                    {
                        skCanvas.DrawRoundRect(rectangle.x, rectangle.y, rectangle.width, rectangle.height, rectangle.rx, rectangle.ry, skPaint);
                    }
                    else
                    {
                        skCanvas.DrawRect(rectangle.x, rectangle.y, rectangle.width, rectangle.height, skPaint);
                    }
                }
            }

            if (svgRectangle.Stroke != null)
            {
                using (var skPaint = GetStrokeSKPaint(svgRectangle, skSize, rectangle.bounds, disposable))
                {
                    if (rectangle.isRound)
                    {
                        skCanvas.DrawRoundRect(rectangle.bounds, rectangle.rx, rectangle.ry, skPaint);
                    }
                    else
                    {
                        skCanvas.DrawRect(rectangle.bounds, skPaint);
                    }
                }
            }

            skCanvas.Restore();
        }

        internal static void DrawSvgMarker(SKCanvas skCanvas, SKSize skSize, SvgMarker svgMarker, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var marker = new Marker(svgMarker);

            SetOpacity(skCanvas, svgMarker, disposable);
            SetTransform(skCanvas, marker.matrix);

            // TODO:

            skCanvas.Restore();
        }

        internal static void DrawSvgGlyph(SKCanvas skCanvas, SKSize skSize, SvgGlyph svgGlyph, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var glyph = new Glyph(svgGlyph);

            SetOpacity(skCanvas, svgGlyph, disposable);
            SetTransform(skCanvas, glyph.matrix);

            // TODO:

            skCanvas.Restore();
        }

        internal static void DrawSvgGroup(SKCanvas skCanvas, SKSize skSize, SvgGroup svgGroup, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var group = new Group(svgGroup);

            SetOpacity(skCanvas, svgGroup, disposable);
            SetFilter(skCanvas, svgGroup, disposable);
            SetTransform(skCanvas, group.matrix);

            DrawSvgElementCollection(skCanvas, skSize, svgGroup.Children, disposable);

            skCanvas.Restore();
        }

        internal static void DrawSvgLine(SKCanvas skCanvas, SKSize skSize, SvgLine svgLine, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var line = new Line(svgLine);

            SetOpacity(skCanvas, svgLine, disposable);
            SetTransform(skCanvas, line.matrix);

            if (svgLine.Stroke != null)
            {
                using (var skPaint = GetStrokeSKPaint(svgLine, skSize, line.bounds, disposable))
                {
                    skCanvas.DrawLine(line.x0, line.y0, line.x1, line.y1, skPaint);
                }
            }

            skCanvas.Restore();
        }

        internal static void DrawSvgPath(SKCanvas skCanvas, SKSize skSize, SvgPath svgPath, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var path = new Path(svgPath);

            SetOpacity(skCanvas, svgPath, disposable);
            SetTransform(skCanvas, path.matrix);

            using (var skPath = ToSKPath(svgPath.PathData, svgPath.FillRule))
            {
                if (skPath != null && !skPath.IsEmpty)
                {
                    var skBounds = skPath.Bounds;

                    if (svgPath.Fill != null)
                    {
                        using (var skPaint = GetFillSKPaint(svgPath, skSize, skBounds, disposable))
                        {
                            skCanvas.DrawPath(skPath, skPaint);
                        }
                    }

                    if (svgPath.Stroke != null)
                    {
                        using (var skPaint = GetStrokeSKPaint(svgPath, skSize, skBounds, disposable))
                        {
                            skCanvas.DrawPath(skPath, skPaint);
                        }
                    }
                }
            }

            skCanvas.Restore();
        }

        internal static void DrawSvgPolyline(SKCanvas skCanvas, SKSize skSize, SvgPolyline svgPolyline, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var polyline = new Polyline(svgPolyline);

            SetOpacity(skCanvas, svgPolyline, disposable);
            SetTransform(skCanvas, polyline.matrix);

            using (var skPath = ToSKPath(svgPolyline.Points, svgPolyline.FillRule, false))
            {
                if (skPath != null && !skPath.IsEmpty)
                {
                    var skBounds = skPath.Bounds;

                    if (svgPolyline.Fill != null)
                    {
                        using (var skPaint = GetFillSKPaint(svgPolyline, skSize, skBounds, disposable))
                        {
                            skCanvas.DrawPath(skPath, skPaint);
                        }
                    }

                    if (svgPolyline.Stroke != null)
                    {
                        using (var skPaint = GetStrokeSKPaint(svgPolyline, skSize, skBounds, disposable))
                        {
                            skCanvas.DrawPath(skPath, skPaint);
                        }
                    }
                }
            }

            skCanvas.Restore();
        }

        internal static void DrawSvgPolygon(SKCanvas skCanvas, SKSize skSize, SvgPolygon svgPolygon, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var polygon = new Polygon(svgPolygon);

            SetOpacity(skCanvas, svgPolygon, disposable);
            SetTransform(skCanvas, polygon.matrix);

            using (var skPath = ToSKPath(svgPolygon.Points, svgPolygon.FillRule, true))
            {
                if (skPath != null && !skPath.IsEmpty)
                {
                    var skBounds = skPath.Bounds;

                    if (svgPolygon.Fill != null)
                    {
                        using (var skPaint = GetFillSKPaint(svgPolygon, skSize, skBounds, disposable))
                        {
                            skCanvas.DrawPath(skPath, skPaint);
                        }
                    }

                    if (svgPolygon.Stroke != null)
                    {
                        using (var skPaint = GetStrokeSKPaint(svgPolygon, skSize, skBounds, disposable))
                        {
                            skCanvas.DrawPath(skPath, skPaint);
                        }
                    }
                }
            }

            skCanvas.Restore();
        }

        internal static void DrawSvgText(SKCanvas skCanvas, SKSize skSize, SvgText svgText, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var text = new Text(svgText);

            SetOpacity(skCanvas, svgText, disposable);
            SetTransform(skCanvas, text.matrix);

            // TODO:

            skCanvas.Restore();
        }

        internal static void DrawSvgTextPath(SKCanvas skCanvas, SKSize skSize, SvgTextPath svgTextPath, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var textPath = new TextPath(svgTextPath);

            SetOpacity(skCanvas, svgTextPath, disposable);
            SetTransform(skCanvas, textPath.matrix);

            // TODO:

            skCanvas.Restore();
        }

        internal static void DrawSvgTextRef(SKCanvas skCanvas, SKSize skSize, SvgTextRef svgTextRef, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var textRef = new TextRef(svgTextRef);

            SetOpacity(skCanvas, svgTextRef, disposable);
            SetTransform(skCanvas, textRef.matrix);

            // TODO:

            skCanvas.Restore();
        }

        internal static void DrawSvgTextSpan(SKCanvas skCanvas, SKSize skSize, SvgTextSpan svgTextSpan, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var textSpan = new TextSpan(svgTextSpan);

            SetOpacity(skCanvas, svgTextSpan, disposable);
            SetTransform(skCanvas, textSpan.matrix);

            // TODO:

            skCanvas.Restore();
        }

        internal static void DrawSvgElement(SKCanvas skCanvas, SKSize skSize, SvgElement svgElement, CompositeDisposable disposable)
        {
            switch (svgElement)
            {
                case SvgFragment svgFragment:
                    {
                        DrawSvgFragment(skCanvas, skSize, svgFragment, disposable);
                    }
                    break;
                case SvgImage svgImage:
                    {
                        DrawSvgImage(skCanvas, skSize, svgImage, disposable);
                    }
                    break;
                case SvgSwitch svgSwitch:
                    {
                        DrawSvgSwitch(skCanvas, skSize, svgSwitch, disposable);
                    }
                    break;
                case SvgUse svgUse:
                    {
                        DrawSvgUse(skCanvas, skSize, svgUse, disposable);
                    }
                    break;
                case SvgForeignObject svgForeignObject:
                    {
                        DrawSvgForeignObject(skCanvas, skSize, svgForeignObject, disposable);
                    }
                    break;
                case SvgCircle svgCircle:
                    {
                        DrawSvgCircle(skCanvas, skSize, svgCircle, disposable);
                    }
                    break;
                case SvgEllipse svgEllipse:
                    {
                        DrawSvgEllipse(skCanvas, skSize, svgEllipse, disposable);
                    }
                    break;
                case SvgRectangle svgRectangle:
                    {
                        DrawSvgRectangle(skCanvas, skSize, svgRectangle, disposable);
                    }
                    break;
                case SvgMarker svgMarker:
                    {
                        DrawSvgMarker(skCanvas, skSize, svgMarker, disposable);
                    }
                    break;
                case SvgGlyph svgGlyph:
                    {
                        DrawSvgGlyph(skCanvas, skSize, svgGlyph, disposable);
                    }
                    break;
                case SvgGroup svgGroup:
                    {
                        DrawSvgGroup(skCanvas, skSize, svgGroup, disposable);
                    }
                    break;
                case SvgLine svgLine:
                    {
                        DrawSvgLine(skCanvas, skSize, svgLine, disposable);
                    }
                    break;
                case SvgPath svgPath:
                    {
                        DrawSvgPath(skCanvas, skSize, svgPath, disposable);
                    }
                    break;
                case SvgPolyline svgPolyline:
                    {
                        DrawSvgPolyline(skCanvas, skSize, svgPolyline, disposable);
                    }
                    break;
                case SvgPolygon svgPolygon:
                    {
                        DrawSvgPolygon(skCanvas, skSize, svgPolygon, disposable);
                    }
                    break;
                case SvgText svgText:
                    {
                        DrawSvgText(skCanvas, skSize, svgText, disposable);
                    }
                    break;
                case SvgTextPath svgTextPath:
                    {
                        DrawSvgTextPath(skCanvas, skSize, svgTextPath, disposable);
                    }
                    break;
                case SvgTextRef svgTextRef:
                    {
                        DrawSvgTextRef(skCanvas, skSize, svgTextRef, disposable);
                    }
                    break;
                case SvgTextSpan svgTextSpan:
                    {
                        DrawSvgTextSpan(skCanvas, skSize, svgTextSpan, disposable);
                    }
                    break;
                default:
                    break;
            }
        }

        internal static void DrawSvgElementCollection(SKCanvas canvas, SKSize skSize, SvgElementCollection svgElementCollection, CompositeDisposable disposable)
        {
            foreach (var svgElement in svgElementCollection)
            {
                DrawSvgElement(canvas, skSize, svgElement, disposable);
            }
        }
    }
}
