// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using SkiaSharp;
using Svg.FilterEffects;
using Svg.Pathing;
using Svg.Transforms;

namespace Svg.Skia
{
    [Flags]
    internal enum PathPointType : byte
    {
        Start = 0,
        Line = 1,
        Bezier = 3,
        Bezier3 = 3,
        PathTypeMask = 0x7,
        DashMode = 0x10,
        PathMarker = 0x20,
        CloseSubpath = 0x80
    }

    internal static class SkiaUtil
    {
        internal static SKSize GetDimensions(SvgFragment svgFragment)
        {
            float w, h;
            var isWidthperc = svgFragment.Width.Type == SvgUnitType.Percentage;
            var isHeightperc = svgFragment.Height.Type == SvgUnitType.Percentage;

            var bounds = new SKRect();
            if (isWidthperc || isHeightperc)
            {
                if (svgFragment.ViewBox.Width > 0 && svgFragment.ViewBox.Height > 0)
                {
                    bounds = new SKRect(svgFragment.ViewBox.MinX, svgFragment.ViewBox.MinY, svgFragment.ViewBox.Width, svgFragment.ViewBox.Height);
                }
                else
                {
                    // TODO: Calculate `bounds` from `Children` bounds.
                }
            }

            if (isWidthperc)
            {
                w = (bounds.Width + bounds.Left) * (svgFragment.Width.Value * 0.01f);
            }
            else
            {
                w = svgFragment.Width.ToDeviceValue(null, UnitRenderingType.Horizontal, svgFragment);
            }
            if (isHeightperc)
            {
                h = (bounds.Height + bounds.Top) * (svgFragment.Height.Value * 0.01f);
            }
            else
            {
                h = svgFragment.Height.ToDeviceValue(null, UnitRenderingType.Vertical, svgFragment);
            }

            return new SKSize(w, h);
        }

        internal static T? GetReference<T>(SvgElement svgElement, Uri uri) where T : SvgElement
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

        internal static SKPath ToSKPath(SvgPathSegmentList svgPathSegmentList, SvgFillRule svgFillRule, CompositeDisposable disposable)
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
                            float xAxisRotate = svgArcSegment.Angle;
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

            disposable.Add(skPath);
            return skPath;
        }

        internal static SKPath ToSKPath(SvgPointCollection svgPointCollection, SvgFillRule svgFillRule, bool isClosed, CompositeDisposable disposable)
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

            disposable.Add(skPath);
            return skPath;
        }

        internal static SKPath ToSKPath(SvgRectangle svgRectangle, SvgFillRule svgFillRule, CompositeDisposable disposable)
        {
            var skPath = new SKPath()
            {
                FillType = (svgFillRule == SvgFillRule.EvenOdd) ? SKPathFillType.EvenOdd : SKPathFillType.Winding
            };

            float x = svgRectangle.X.ToDeviceValue(null, UnitRenderingType.Horizontal, svgRectangle);
            float y = svgRectangle.Y.ToDeviceValue(null, UnitRenderingType.Vertical, svgRectangle);
            float width = svgRectangle.Width.ToDeviceValue(null, UnitRenderingType.Horizontal, svgRectangle);
            float height = svgRectangle.Height.ToDeviceValue(null, UnitRenderingType.Vertical, svgRectangle);
            float rx = svgRectangle.CornerRadiusX.ToDeviceValue(null, UnitRenderingType.Horizontal, svgRectangle);
            float ry = svgRectangle.CornerRadiusY.ToDeviceValue(null, UnitRenderingType.Vertical, svgRectangle);

            if (width <= 0f || height <= 0f || rx < 0f || ry < 0f)
            {
                disposable.Add(skPath);
                return skPath;
            }

            if (rx > 0f)
            {
                float halfWidth = width / 2f;
                if (rx > halfWidth)
                {
                    rx = halfWidth;
                }
            }

            if (ry > 0f)
            {
                float halfHeight = height / 2f;
                if (ry > halfHeight)
                {
                    ry = halfHeight;
                }
            }

            bool isRound = rx > 0f && ry > 0f;
            var skRectBounds = SKRect.Create(x, y, width, height);

            if (isRound)
            {
                skPath.AddRoundRect(skRectBounds, rx, ry);
            }
            else
            {
                skPath.AddRect(skRectBounds);
            }

            disposable.Add(skPath);
            return skPath;
        }

        internal static SKPath ToSKPath(SvgCircle svgCircle, SvgFillRule svgFillRule, CompositeDisposable disposable)
        {
            var skPath = new SKPath()
            {
                FillType = (svgFillRule == SvgFillRule.EvenOdd) ? SKPathFillType.EvenOdd : SKPathFillType.Winding
            };

            float cx = svgCircle.CenterX.ToDeviceValue(null, UnitRenderingType.Horizontal, svgCircle);
            float cy = svgCircle.CenterY.ToDeviceValue(null, UnitRenderingType.Vertical, svgCircle);
            float radius = svgCircle.Radius.ToDeviceValue(null, UnitRenderingType.Other, svgCircle);

            if (radius <= 0f)
            {
                disposable.Add(skPath);
                return skPath;
            }

            skPath.AddCircle(cx, cy, radius);

            disposable.Add(skPath);
            return skPath;
        }

        internal static SKPath ToSKPath(SvgEllipse svgEllipse, SvgFillRule svgFillRule, CompositeDisposable disposable)
        {
            var skPath = new SKPath()
            {
                FillType = (svgFillRule == SvgFillRule.EvenOdd) ? SKPathFillType.EvenOdd : SKPathFillType.Winding
            };

            float cx = svgEllipse.CenterX.ToDeviceValue(null, UnitRenderingType.Horizontal, svgEllipse);
            float cy = svgEllipse.CenterY.ToDeviceValue(null, UnitRenderingType.Vertical, svgEllipse);
            float rx = svgEllipse.RadiusX.ToDeviceValue(null, UnitRenderingType.Other, svgEllipse);
            float ry = svgEllipse.RadiusY.ToDeviceValue(null, UnitRenderingType.Other, svgEllipse);

            if (rx <= 0f || ry <= 0f)
            {
                disposable.Add(skPath);
                return skPath;
            }

            var skRectBounds = SKRect.Create(cx - rx, cy - ry, rx + rx, ry + ry);

            skPath.AddOval(skRectBounds);

            disposable.Add(skPath);
            return skPath;
        }

        internal static SKPath ToSKPath(SvgLine svgLine, SvgFillRule svgFillRule, CompositeDisposable disposable)
        {
            var skPath = new SKPath()
            {
                FillType = (svgFillRule == SvgFillRule.EvenOdd) ? SKPathFillType.EvenOdd : SKPathFillType.Winding
            };

            float x0 = svgLine.StartX.ToDeviceValue(null, UnitRenderingType.Horizontal, svgLine);
            float y0 = svgLine.StartY.ToDeviceValue(null, UnitRenderingType.Vertical, svgLine);
            float x1 = svgLine.EndX.ToDeviceValue(null, UnitRenderingType.Horizontal, svgLine);
            float y1 = svgLine.EndY.ToDeviceValue(null, UnitRenderingType.Vertical, svgLine);

            skPath.MoveTo(x0, y0);
            skPath.LineTo(x1, y1);

            disposable.Add(skPath);
            return skPath;
        }

        internal static List<(SKPoint Point, byte Type)> GetPathTypes(SKPath path)
        {
            // System.Drawing.Drawing2D.GraphicsPath.PathTypes
            // System.Drawing.Drawing2D.PathPointType
            // byte -> PathPointType
            var pathTypes = new List<(SKPoint Point, byte Type)>();
            using (var iterator = path.CreateRawIterator())
            {
                var points = new SKPoint[4];
                var pathVerb = SKPathVerb.Move;
                (SKPoint Point, byte Type) lastPoint = (default, 0);
                while ((pathVerb = iterator.Next(points)) != SKPathVerb.Done)
                {
                    switch (pathVerb)
                    {
                        case SKPathVerb.Move:
                            {
                                pathTypes.Add((points[0], (byte)PathPointType.Start));
                                lastPoint = (points[0], (byte)PathPointType.Start);
                            }
                            break;
                        case SKPathVerb.Line:
                            {
                                pathTypes.Add((points[1], (byte)PathPointType.Line));
                                lastPoint = (points[1], (byte)PathPointType.Line);
                            }
                            break;
                        case SKPathVerb.Cubic:
                            {
                                pathTypes.Add((points[1], (byte)PathPointType.Bezier));
                                pathTypes.Add((points[2], (byte)PathPointType.Bezier));
                                pathTypes.Add((points[3], (byte)PathPointType.Bezier));
                                lastPoint = (points[3], (byte)PathPointType.Bezier);
                            }
                            break;
                        case SKPathVerb.Quad:
                            {
                                pathTypes.Add((points[1], (byte)PathPointType.Bezier));
                                pathTypes.Add((points[2], (byte)PathPointType.Bezier));
                                lastPoint = (points[2], (byte)PathPointType.Bezier);
                            }
                            break;
                        case SKPathVerb.Conic:
                            {
                                pathTypes.Add((points[1], (byte)PathPointType.Bezier));
                                pathTypes.Add((points[2], (byte)PathPointType.Bezier));
                                lastPoint = (points[2], (byte)PathPointType.Bezier);
                            }
                            break;
                        case SKPathVerb.Close:
                            {
                                lastPoint = (lastPoint.Point, (byte)((lastPoint.Type | (byte)PathPointType.CloseSubpath)));
                                pathTypes[pathTypes.Count - 1] = lastPoint;
                            }
                            break;
                    }
                }
            }
            return pathTypes;
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

        internal static SKPathEffect? CreateDash(SvgElement svgElement)
        {
            var strokeDashArray = svgElement.StrokeDashArray;
            var count = strokeDashArray.Count;

            if (strokeDashArray != null && count > 0)
            {
                bool isOdd = count % 2 != 0;
                float sum = 0f;
                float[] intervals = new float[isOdd ? count * 2 : count];
                for (int i = 0; i < count; i++)
                {
                    var dash = strokeDashArray[i].ToDeviceValue(null, UnitRenderingType.Other, svgElement);
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

                float phase = svgElement.StrokeDashOffset != null ? svgElement.StrokeDashOffset.ToDeviceValue(null, UnitRenderingType.Other, svgElement) : 0f;

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
                        var stopColor = GetColor(stopColorSvgColourServer, AdjustSvgOpacity(svgGradientStop.StopOpacity), false);
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

            if (skColors.Length == 0)
            {
                return SKShader.CreateColor(SKColors.Transparent);
            }
            else if (skColors.Length == 1)
            {
                return SKShader.CreateColor(skColors[0]);
            }

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

                return SKShader.CreateLinearGradient(skStart, skEnd, skColors, skColorPos, shaderTileMode, skBoundingBoxTransform);
            }
            else
            {
                if (svgLinearGradientServer.GradientTransform != null && svgLinearGradientServer.GradientTransform.Count > 0)
                {
                    var gradientTransform = GetSKMatrix(svgLinearGradientServer.GradientTransform);
                    return SKShader.CreateLinearGradient(skStart, skEnd, skColors, skColorPos, shaderTileMode, gradientTransform);
                }
                else
                {
                    return SKShader.CreateLinearGradient(skStart, skEnd, skColors, skColorPos, shaderTileMode);
                }
            }
        }

        internal static SKShader CreateTwoPointConicalGradient(SvgRadialGradientServer svgRadialGradientServer, SKSize skSize, SKRect skBounds, SvgVisualElement svgVisualElement)
        {
            var skStart = new SKPoint(
                NormalizeSvgUnit(svgRadialGradientServer.CenterX, svgRadialGradientServer.GradientUnits)
                    .ToDeviceValue(null, UnitRenderingType.Horizontal, svgRadialGradientServer),
                NormalizeSvgUnit(svgRadialGradientServer.CenterY, svgRadialGradientServer.GradientUnits)
                    .ToDeviceValue(null, UnitRenderingType.Vertical, svgRadialGradientServer));
            var startRadius = 0f;

            var skEnd = new SKPoint(
                NormalizeSvgUnit(svgRadialGradientServer.FocalX, svgRadialGradientServer.GradientUnits)
                    .ToDeviceValue(null, UnitRenderingType.Horizontal, svgRadialGradientServer),
                NormalizeSvgUnit(svgRadialGradientServer.FocalY, svgRadialGradientServer.GradientUnits)
                    .ToDeviceValue(null, UnitRenderingType.Vertical, svgRadialGradientServer));
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

            if (skColors.Length == 0)
            {
                return SKShader.CreateColor(SKColors.Transparent);
            }
            else if (skColors.Length == 1)
            {
                return SKShader.CreateColor(skColors[0]);
            }

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

                if (svgRadialGradientServer.GradientTransform != null 
                    && svgRadialGradientServer.GradientTransform.Count > 0)
                {
                    var gradientTransform = GetSKMatrix(svgRadialGradientServer.GradientTransform);
                    SKMatrix.Concat(ref skBoundingBoxTransform, ref skBoundingBoxTransform, ref gradientTransform);
                }

                return SKShader.CreateTwoPointConicalGradient(
                    skStart, startRadius, 
                    skEnd, endRadius, 
                    skColors, skColorPos, 
                    shaderTileMode, 
                    skBoundingBoxTransform);
            }
            else
            {
                if (svgRadialGradientServer.GradientTransform != null 
                    && svgRadialGradientServer.GradientTransform.Count > 0)
                {
                    var gradientTransform = GetSKMatrix(svgRadialGradientServer.GradientTransform);
                    return SKShader.CreateTwoPointConicalGradient(
                        skStart, startRadius, 
                        skEnd, endRadius, 
                        skColors, skColorPos, 
                        shaderTileMode, gradientTransform);
                }
                else
                {
                    return SKShader.CreateTwoPointConicalGradient(
                        skStart, startRadius, 
                        skEnd, endRadius, 
                        skColors, skColorPos, 
                        shaderTileMode);
                }
            }
        }

        internal static SKPicture CreatePicture(SvgElementCollection svgElementCollection, float width, float height, SKMatrix sKMatrix)
        {
            var skSize = new SKSize(width, height);
            var cullRect = SKRect.Create(skSize);
            using (var skPictureRecorder = new SKPictureRecorder())
            using (var skCanvas = skPictureRecorder.BeginRecording(cullRect))
            using (var renderer = new SKSvgRenderer(skCanvas, skSize))
            {
                skCanvas.SetMatrix(sKMatrix);
                foreach (var svgElement in svgElementCollection)
                {
                    renderer.Draw(svgElement, false);
                }
                return skPictureRecorder.EndRecording();
            }
        }

        internal static SKShader? CreatePicture(SvgPatternServer svgPatternServer, SKSize skSize, SKRect skBounds, SvgVisualElement svgVisualElement, CompositeDisposable disposable)
        {
            var svgPatternServers = new List<SvgPatternServer>();
            var currentPatternServer = svgPatternServer;
            do
            {
                svgPatternServers.Add(currentPatternServer);
                currentPatternServer = SvgDeferredPaintServer.TryGet<SvgPatternServer>(currentPatternServer.InheritGradient, svgVisualElement);
            } while (currentPatternServer != null);

            SvgPatternServer? firstChildren = null;
            SvgPatternServer? firstX = null;
            SvgPatternServer? firstY = null;
            SvgPatternServer? firstWidth = null;
            SvgPatternServer? firstHeight = null;
            SvgPatternServer? firstPatternUnit = null;
            SvgPatternServer? firstPatternContentUnit = null;
            SvgPatternServer? firstViewBox = null;

            foreach (var p in svgPatternServers)
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
                    if (p.X != null && p.X != SvgUnit.None)
                    {
                        firstX = p;
                    }
                }
                if (firstY == null)
                {
                    if (p.Y != null && p.Y != SvgUnit.None)
                    {
                        firstY = p;
                    }
                }
                if (firstWidth == null)
                {
                    if (p.Width != null && p.Width != SvgUnit.None)
                    {
                        firstWidth = p;
                    }
                }
                if (firstHeight == null)
                {
                    if (p.Height != null && p.Height != SvgUnit.None)
                    {
                        firstHeight = p;
                    }
                }
                if (firstPatternUnit == null)
                {
                    if (p.PatternUnits != SvgCoordinateUnits.Inherit)
                    {
                        firstPatternUnit = p;
                    }
                }
                if (firstPatternContentUnit == null)
                {
                    if (p.PatternContentUnits != SvgCoordinateUnits.Inherit)
                    {
                        firstPatternContentUnit = p;
                    }
                }
                if (firstViewBox == null)
                {
                    if (p.ViewBox != null && p.ViewBox != SvgViewBox.Empty)
                    {
                        firstViewBox = p;
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

            float x = xUnit.ToDeviceValue(null, UnitRenderingType.Horizontal, svgPatternServer);
            float y = yUnit.ToDeviceValue(null, UnitRenderingType.Vertical, svgPatternServer);
            float width = widthUnit.ToDeviceValue(null, UnitRenderingType.Horizontal, svgPatternServer);
            float height = heightUnit.ToDeviceValue(null, UnitRenderingType.Vertical, svgPatternServer);

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

            var skLocalMatrix = SKMatrix.MakeIdentity();
            if (svgPatternServer.PatternTransform != null && svgPatternServer.PatternTransform.Count > 0)
            {
                var patternTransform = GetSKMatrix(svgPatternServer.PatternTransform);
                SKMatrix.Concat(ref skLocalMatrix, ref skLocalMatrix, ref patternTransform);
            }
            var translateTransform = SKMatrix.MakeTranslation(skRectTransformed.Left, skRectTransformed.Top);
            SKMatrix.Concat(ref skLocalMatrix, ref skLocalMatrix, ref translateTransform);

            SKMatrix skPictureTransform = SKMatrix.MakeIdentity();
            if (!viewBox.Equals(SvgViewBox.Empty))
            {
                var viewBoxTransform = GetSvgViewBoxTransform(
                    viewBox,
                    svgPatternServer.AspectRatio,
                    skRectTransformed.Left,
                    skRectTransformed.Top,
                    skRectTransformed.Width,
                    skRectTransformed.Height);
                SKMatrix.Concat(ref skPictureTransform, ref skPictureTransform, ref viewBoxTransform);
            }
            else
            {
                if (patternContentUnits == SvgCoordinateUnits.ObjectBoundingBox)
                {
                    var scaleTransform = SKMatrix.MakeScale(skBounds.Width, skBounds.Height);
                    SKMatrix.Concat(ref skPictureTransform, ref skPictureTransform, ref scaleTransform);
                }
            }

            SKPicture sKPicture = CreatePicture(firstChildren.Children, skRectTransformed.Width, skRectTransformed.Height, skPictureTransform);
            disposable.Add(sKPicture);

            SKShader sKShader = SKShader.CreatePicture(sKPicture, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, skLocalMatrix, sKPicture.CullRect);
            return sKShader;
        }

        internal static void SetFill(SvgVisualElement svgVisualElement, SKSize skSize, SKRect skBounds, SKPaint skPaint, CompositeDisposable disposable)
        {
            var server = svgVisualElement.Fill;
            var fallbackServer = SvgPaintServer.None;

            if (server is SvgDeferredPaintServer svgDeferredPaintServer)
            {
                server = SvgDeferredPaintServer.TryGet<SvgPaintServer>(svgDeferredPaintServer, svgVisualElement);
                fallbackServer = svgDeferredPaintServer.FallbackServer;
            }

            switch (server)
            {
                case SvgColourServer svgColourServer:
                    {
                        skPaint.Color = GetColor(svgColourServer, AdjustSvgOpacity(svgVisualElement.FillOpacity), false);
                    }
                    break;
                case SvgPatternServer svgPatternServer:
                    {
                        var skShader = CreatePicture(svgPatternServer, skSize, skBounds, svgVisualElement, disposable);
                        if (skShader != null)
                        {
                            disposable.Add(skShader);
                            skPaint.Shader = skShader;
                        }
                        else
                        {
                            if (fallbackServer is SvgColourServer svgColourServerFallback)
                            {
                                skPaint.Color = GetColor(svgColourServerFallback, AdjustSvgOpacity(svgVisualElement.StrokeOpacity), true);
                            }
                            else
                            {
                                // TODO: Do not draw element.
                                skPaint.Color = SKColors.Transparent;
                            }
                        }
                    }
                    break;
                case SvgLinearGradientServer svgLinearGradientServer:
                    {
                        if (svgLinearGradientServer.GradientUnits == SvgCoordinateUnits.ObjectBoundingBox && (skBounds.Width == 0f || skBounds.Height == 0f))
                        {
                            if (fallbackServer is SvgColourServer svgColourServerFallback)
                            {
                                skPaint.Color = GetColor(svgColourServerFallback, AdjustSvgOpacity(svgVisualElement.StrokeOpacity), true);
                            }
                            else
                            {
                                // TODO: Do not draw element.
                                skPaint.Color = SKColors.Transparent;
                            }
                        }
                        else
                        {
                            var skShader = CreateLinearGradient(svgLinearGradientServer, skSize, skBounds, svgVisualElement);
                            if (skShader != null)
                            {
                                disposable.Add(skShader);
                                skPaint.Shader = skShader;
                            }
                        }
                    }
                    break;
                case SvgRadialGradientServer svgRadialGradientServer:
                    {
                        if (svgRadialGradientServer.GradientUnits == SvgCoordinateUnits.ObjectBoundingBox && (skBounds.Width == 0f || skBounds.Height == 0f))
                        {
                            if (fallbackServer is SvgColourServer svgColourServerFallback)
                            {
                                skPaint.Color = GetColor(svgColourServerFallback, AdjustSvgOpacity(svgVisualElement.StrokeOpacity), true);
                            }
                            else
                            {
                                // TODO: Do not draw element.
                                skPaint.Color = SKColors.Transparent;
                            }
                        }
                        else
                        {
                            var skShader = CreateTwoPointConicalGradient(svgRadialGradientServer, skSize, skBounds, svgVisualElement);
                            if (skShader != null)
                            {
                                disposable.Add(skShader);
                                skPaint.Shader = skShader;
                            }
                        }
                    }
                    break;
                default:
                    // TODO: Do not draw element.
                    break;
            }
        }

        internal static void SetStroke(SvgVisualElement svgVisualElement, SKSize skSize, SKRect skBounds, SKPaint skPaint, CompositeDisposable disposable)
        {
            var server = svgVisualElement.Stroke;
            var fallbackServer = SvgPaintServer.None;

            if (server is SvgDeferredPaintServer svgDeferredPaintServer)
            {
                server = SvgDeferredPaintServer.TryGet<SvgPaintServer>(svgDeferredPaintServer, svgVisualElement);
                fallbackServer = svgDeferredPaintServer.FallbackServer;
            }

            switch (server)
            {
                case SvgColourServer svgColourServer:
                    {
                        skPaint.Color = GetColor(svgColourServer, AdjustSvgOpacity(svgVisualElement.StrokeOpacity), true);
                    }
                    break;
                case SvgPatternServer svgPatternServer:
                    {
                        var skShader = CreatePicture(svgPatternServer, skSize, skBounds, svgVisualElement, disposable);
                        if (skShader != null)
                        {
                            disposable.Add(skShader);
                            skPaint.Shader = skShader;
                        }
                        else
                        {
                            if (fallbackServer is SvgColourServer svgColourServerFallback)
                            {
                                skPaint.Color = GetColor(svgColourServerFallback, AdjustSvgOpacity(svgVisualElement.StrokeOpacity), true);
                            }
                            else
                            {
                                // TODO: Do not draw element.
                                skPaint.Color = SKColors.Transparent;
                            }
                        }
                    }
                    break;
                case SvgLinearGradientServer svgLinearGradientServer:
                    {
                        if (svgLinearGradientServer.GradientUnits == SvgCoordinateUnits.ObjectBoundingBox && (skBounds.Width == 0f || skBounds.Height == 0f))
                        {
                            if (fallbackServer is SvgColourServer svgColourServerFallback)
                            {
                                skPaint.Color = GetColor(svgColourServerFallback, AdjustSvgOpacity(svgVisualElement.StrokeOpacity), true);
                            }
                            else
                            {
                                // TODO: Do not draw element.
                                skPaint.Color = SKColors.Transparent;
                            }
                        }
                        else
                        {
                            var skShader = CreateLinearGradient(svgLinearGradientServer, skSize, skBounds, svgVisualElement);
                            if (skShader != null)
                            {
                                disposable.Add(skShader);
                                skPaint.Shader = skShader;
                            }
                        }
                    }
                    break;
                case SvgRadialGradientServer svgRadialGradientServer:
                    {
                        if (svgRadialGradientServer.GradientUnits == SvgCoordinateUnits.ObjectBoundingBox && (skBounds.Width == 0f || skBounds.Height == 0f))
                        {
                            if (fallbackServer is SvgColourServer svgColourServerFallback)
                            {
                                skPaint.Color = GetColor(svgColourServerFallback, AdjustSvgOpacity(svgVisualElement.StrokeOpacity), true);
                            }
                            else
                            {
                                // TODO: Do not draw element.
                                skPaint.Color = SKColors.Transparent;
                            }
                        }
                        else
                        {
                            var skShader = CreateTwoPointConicalGradient(svgRadialGradientServer, skSize, skBounds, svgVisualElement);
                            if (skShader != null)
                            {
                                disposable.Add(skShader);
                                skPaint.Shader = skShader;
                            }
                        }
                    }
                    break;
                default:
                    // TODO: Do not draw element.
                    break;
            }
        }

        internal static void SetDash(SvgVisualElement svgVisualElement, SKPaint skPaint, CompositeDisposable disposable)
        {
            var skPathEffect = CreateDash(svgVisualElement);
            if (skPathEffect != null)
            {
                disposable.Add(skPathEffect);
                skPaint.PathEffect = skPathEffect;
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

        internal static SKPaint? SetFilter(SKCanvas skCanvas, SvgVisualElement svgVisualElement, CompositeDisposable disposable)
        {
            if (svgVisualElement.Filter != null)
            {
                var skPaint = new SKPaint();
                skPaint.Style = SKPaintStyle.StrokeAndFill;
                SetFilter(svgVisualElement, skPaint, disposable);
                skCanvas.SaveLayer(skPaint);
                disposable.Add(skPaint);
                return skPaint;
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

        internal static bool IsValidFill(SvgElement svgElement)
        {
            return svgElement.Fill != null;
        }

        internal static bool IsValidStroke(SvgElement svgElement)
        {
            return svgElement.Stroke != null
                && svgElement.Stroke != SvgPaintServer.None
                && svgElement.StrokeWidth > 0f;
        }

        internal static SKPaint GetFillSKPaint(SvgVisualElement svgVisualElement, SKSize skSize, SKRect skBounds, CompositeDisposable disposable)
        {
            var skPaint = new SKPaint()
            {
                IsAntialias = IsAntialias(svgVisualElement)
            };

            // TODO: SvgElement

            // TODO: SvgElementStyle

            SetFill(svgVisualElement, skSize, skBounds, skPaint, disposable);

            // TODO: SvgVisualElement

            // TODO: SvgVisualElementStyle

            if (svgVisualElement.Filter != null)
            {
                SetFilter(svgVisualElement, skPaint, disposable);
            }

            skPaint.Style = SKPaintStyle.Fill;

            disposable.Add(skPaint);
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

            SetStroke(svgVisualElement, skSize, skBounds, skPaint, disposable);

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

            // TODO: SvgVisualElementStyle

            if (svgVisualElement.Filter != null)
            {
                SetFilter(svgVisualElement, skPaint, disposable);
            }

            skPaint.Style = SKPaintStyle.Stroke;

            disposable.Add(skPaint);
            return skPaint;
        }

        internal static void SetSKPaintText(SvgText svgText, SKSize skSize, SKRect skBounds, SKPaint skPaint, CompositeDisposable disposable)
        {
            skPaint.LcdRenderText = true;
            skPaint.SubpixelText = true;
            skPaint.TextEncoding = SKTextEncoding.Utf16;

            // TODO:
            var fontFamily = svgText.FontFamily;

            var fontWeight = SKFontStyleWeight.Normal;
            switch (svgText.FontWeight)
            {
                case SvgFontWeight.Inherit:
                    // TODO:
                    break;
                case SvgFontWeight.Bolder:
                    // TODO:
                    break;
                case SvgFontWeight.Lighter:
                    // TODO:
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
                case SvgFontWeight.W400:
                //case SvgFontWeight.Normal:
                    fontWeight = SKFontStyleWeight.Normal;
                    break;
                case SvgFontWeight.W500:
                    fontWeight = SKFontStyleWeight.Medium;
                    break;
                case SvgFontWeight.W600:
                    fontWeight = SKFontStyleWeight.SemiBold;
                    break;
                case SvgFontWeight.W700:
                //case SvgFontWeight.Bold:
                    fontWeight = SKFontStyleWeight.Bold;
                    break;
                case SvgFontWeight.W800:
                    fontWeight = SKFontStyleWeight.ExtraBold;
                    break;
                case SvgFontWeight.W900:
                    fontWeight = SKFontStyleWeight.Black;
                    break;
            }

            var fontWidth = SKFontStyleWidth.Normal;
            // TODO:
            if (svgText.TryGetAttribute("font-stretch", out string attributeFontStretch))
            {
                switch (attributeFontStretch.ToLower())
                {
                    case "inherit":
                        // TODO:
                        break;
                    case "ultra-condensed":
                        fontWidth = SKFontStyleWidth.UltraCondensed;
                        break;
                    case "extra-condensed":
                        fontWidth = SKFontStyleWidth.ExtraCondensed;
                        break;
                    case "condensed":
                        fontWidth = SKFontStyleWidth.Condensed;
                        break;
                    case "semi-condensed":
                        fontWidth = SKFontStyleWidth.SemiCondensed;
                        break;
                    case "normal":
                        fontWidth = SKFontStyleWidth.Normal;
                        break;
                    case "semi-expanded":
                        fontWidth = SKFontStyleWidth.SemiExpanded;
                        break;
                    case "expanded":
                        fontWidth = SKFontStyleWidth.Expanded;
                        break;
                    case "extra-expanded":
                        fontWidth = SKFontStyleWidth.ExtraExpanded;
                        break;
                    case "ultra-expanded":
                        fontWidth = SKFontStyleWidth.UltraExpanded;
                        break;
                }
            }

            var fontStyle = SkiaUtil.ToSKFontStyleSlant(svgText.FontStyle);

            float fontSize;
            var fontSizeUnit = svgText.FontSize;
            if (fontSizeUnit == SvgUnit.None || fontSizeUnit == SvgUnit.Empty)
            {
                fontSize = new SvgUnit(SvgUnitType.Em, 1.0f);
            }
            else
            {
                fontSize = fontSizeUnit.ToDeviceValue(null, UnitRenderingType.Vertical, svgText);
            }
            skPaint.TextSize = fontSize;

            var skTypeface = SKTypeface.FromFamilyName(fontFamily, fontWeight, fontWidth, fontStyle);
            disposable.Add(skTypeface);

            skPaint.Typeface = skTypeface;
        }

        internal static SKFontStyleSlant ToSKFontStyleSlant(SvgFontStyle fontStyle)
        {
            switch (fontStyle)
            {
                default:
                case SvgFontStyle.Normal:
                    return SKFontStyleSlant.Upright;
                case SvgFontStyle.Oblique:
                    return SKFontStyleSlant.Oblique;
                case SvgFontStyle.Italic:
                    return SKFontStyleSlant.Italic;
            }
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

            if (svgTransformCollection == null)
            {
                return skMatrixTotal;
            }

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
                            float sx = (float)Math.Tan(Math.PI * svgSkew.AngleX / 180);
                            float sy = (float)Math.Tan(Math.PI * svgSkew.AngleY / 180);
                            var skMatrixSkew = SKMatrix.MakeSkew(sx, sy);
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

        internal static SKPath? GetClipPath(SvgVisualElement svgVisualElement, CompositeDisposable disposable)
        {
            switch (svgVisualElement)
            {
                case SvgPath svgPath:
                    {
                        var fillRule = (svgPath.ClipRule == SvgClipRule.EvenOdd) ? SvgFillRule.EvenOdd : SvgFillRule.NonZero;
                        var skPath = SkiaUtil.ToSKPath(svgPath.PathData, fillRule, disposable);
                        if (skPath != null && !skPath.IsEmpty)
                        {
                            var skMatrix = SkiaUtil.GetSKMatrix(svgPath.Transforms);
                            skPath.Transform(skMatrix);

                            var skPathClip = GetSvgVisualElementClipPath(svgPath, disposable);
                            if (skPathClip != null && !skPathClip.IsEmpty)
                            {
                                var result = skPath.Op(skPathClip, SKPathOp.Intersect);
                                disposable.Add(result);
                                return result;
                            }

                            return skPath;
                        }
                    }
                    break;
                case SvgRectangle svgRectangle:
                    {
                        var fillRule = (svgRectangle.ClipRule == SvgClipRule.EvenOdd) ? SvgFillRule.EvenOdd : SvgFillRule.NonZero;
                        var skPath = ToSKPath(svgRectangle, fillRule, disposable);
                        if (skPath != null && !skPath.IsEmpty)
                        {
                            var skMatrix = SkiaUtil.GetSKMatrix(svgRectangle.Transforms);
                            skPath.Transform(skMatrix);

                            var skPathClip = GetSvgVisualElementClipPath(svgRectangle, disposable);
                            if (skPathClip != null && !skPathClip.IsEmpty)
                            {
                                var result = skPath.Op(skPathClip, SKPathOp.Intersect);
                                disposable.Add(result);
                                return result;
                            }

                            return skPath;
                        }
                    }
                    break;
                case SvgCircle svgCircle:
                    {
                        var fillRule = (svgCircle.ClipRule == SvgClipRule.EvenOdd) ? SvgFillRule.EvenOdd : SvgFillRule.NonZero;
                        var skPath = ToSKPath(svgCircle, fillRule, disposable);
                        if (skPath != null && !skPath.IsEmpty)
                        {
                            var skMatrix = SkiaUtil.GetSKMatrix(svgCircle.Transforms);
                            skPath.Transform(skMatrix);

                            var skPathClip = GetSvgVisualElementClipPath(svgCircle, disposable);
                            if (skPathClip != null && !skPathClip.IsEmpty)
                            {
                                var result = skPath.Op(skPathClip, SKPathOp.Intersect);
                                disposable.Add(result);
                                return result;
                            }

                            return skPath;
                        }
                    }
                    break;
                case SvgEllipse svgEllipse:
                    {
                        var fillRule = (svgEllipse.ClipRule == SvgClipRule.EvenOdd) ? SvgFillRule.EvenOdd : SvgFillRule.NonZero;
                        var skPath = ToSKPath(svgEllipse, fillRule, disposable);
                        if (skPath != null && !skPath.IsEmpty)
                        {
                            var skMatrix = SkiaUtil.GetSKMatrix(svgEllipse.Transforms);
                            skPath.Transform(skMatrix);

                            var skPathClip = GetSvgVisualElementClipPath(svgEllipse, disposable);
                            if (skPathClip != null && !skPathClip.IsEmpty)
                            {
                                var result = skPath.Op(skPathClip, SKPathOp.Intersect);
                                disposable.Add(result);
                                return result;
                            }

                            return skPath;
                        }
                    }
                    break;
                case SvgLine svgLine:
                    {
                        var fillRule = (svgLine.ClipRule == SvgClipRule.EvenOdd) ? SvgFillRule.EvenOdd : SvgFillRule.NonZero;
                        var skPath = ToSKPath(svgLine, fillRule, disposable);
                        if (skPath != null && !skPath.IsEmpty)
                        {
                            var skMatrix = SkiaUtil.GetSKMatrix(svgLine.Transforms);
                            skPath.Transform(skMatrix);

                            var skPathClip = GetSvgVisualElementClipPath(svgLine, disposable);
                            if (skPathClip != null && !skPathClip.IsEmpty)
                            {
                                var result = skPath.Op(skPathClip, SKPathOp.Intersect);
                                disposable.Add(result);
                                return result;
                            }

                            return skPath;
                        }
                    }
                    break;
                case SvgPolyline svgPolyline:
                    {
                        var fillRule = (svgPolyline.ClipRule == SvgClipRule.EvenOdd) ? SvgFillRule.EvenOdd : SvgFillRule.NonZero;
                        var skPath = ToSKPath(svgPolyline.Points, fillRule, false, disposable);
                        if (skPath != null && !skPath.IsEmpty)
                        {
                            var skMatrix = SkiaUtil.GetSKMatrix(svgPolyline.Transforms);
                            skPath.Transform(skMatrix);

                            var skPathClip = GetSvgVisualElementClipPath(svgPolyline, disposable);
                            if (skPathClip != null && !skPathClip.IsEmpty)
                            {
                                var result = skPath.Op(skPathClip, SKPathOp.Intersect);
                                disposable.Add(result);
                                return result;
                            }

                            return skPath;
                        }
                    }
                    break;
                case SvgPolygon svgPolygon:
                    {
                        var fillRule = (svgPolygon.ClipRule == SvgClipRule.EvenOdd) ? SvgFillRule.EvenOdd : SvgFillRule.NonZero;
                        var skPath = ToSKPath(svgPolygon.Points, fillRule, true, disposable);
                        if (skPath != null && !skPath.IsEmpty)
                        {
                            var skMatrix = SkiaUtil.GetSKMatrix(svgPolygon.Transforms);
                            skPath.Transform(skMatrix);

                            var skPathClip = GetSvgVisualElementClipPath(svgPolygon, disposable);
                            if (skPathClip != null && !skPathClip.IsEmpty)
                            {
                                var result = skPath.Op(skPathClip, SKPathOp.Intersect);
                                disposable.Add(result);
                                return result;
                            }

                            return skPath;
                        }
                    }
                    break;
                case SvgGroup svgGroup:
                    {
                        var skPath = GetClipPath(svgGroup.Children, disposable);
                        if (skPath != null && !skPath.IsEmpty)
                        {
                            var skMatrix = SkiaUtil.GetSKMatrix(svgGroup.Transforms);
                            skPath.Transform(skMatrix);

                            var skPathClip = GetSvgVisualElementClipPath(svgGroup, disposable);
                            if (skPathClip != null && !skPathClip.IsEmpty)
                            {
                                var result = skPath.Op(skPathClip, SKPathOp.Intersect);
                                disposable.Add(result);
                                return result;
                            }

                            return skPath;
                        }
                    }
                    break;
                case SvgUse svgUse:
                    {
                        // TODO:
                    }
                    break;
                default:
                    break;
            }
            return null;
        }

        internal static SKPath? GetClipPath(SvgElementCollection svgElementCollection, CompositeDisposable disposable)
        {
            var skPathClip = default(SKPath);

            foreach (var child in svgElementCollection)
            {
                if (child is SvgVisualElement visualChild)
                {
                    var skPath = GetClipPath(visualChild, disposable);
                    if (skPath != null && !skPath.IsEmpty)
                    {
                        if (skPathClip == null)
                        {
                            skPathClip = skPath;
                        }
                        else
                        {
                            var result = skPathClip.Op(skPath, SKPathOp.Union);
                            disposable.Add(result);
                            skPathClip = result;
                        }
                    }
                }
            }

            return skPathClip;
        }

        internal static SKPath? GetSvgVisualElementClipPath(SvgVisualElement svgVisualElement, CompositeDisposable disposable)
        {
            if (svgVisualElement == null || svgVisualElement.ClipPath == null)
            {
                return null;
            }

            var svgClipPath = svgVisualElement.OwnerDocument.GetElementById<SvgClipPath>(svgVisualElement.ClipPath.ToString());
            if (svgClipPath == null || svgClipPath.Children == null)
            {
                return null;
            }

            // TODO: svgClipPath.ClipPathUnits

            return GetClipPath(svgClipPath.Children, disposable);
        }

        internal static void SetClipPath(SKCanvas skCanvas, SvgVisualElement svgVisualElement, CompositeDisposable disposable)
        {
            var skPathClip = GetSvgVisualElementClipPath(svgVisualElement, disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = IsAntialias(svgVisualElement);
                skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }
        }

        internal static SKPaint? SetOpacity(SKCanvas skCanvas, SvgElement svgElement, CompositeDisposable disposable)
        {
            float opacity = AdjustSvgOpacity(svgElement.Opacity);
            if (opacity < 1f)
            {
                var skPaint = new SKPaint()
                {
                    IsAntialias = true,
                };
                skPaint.Color = new SKColor(255, 255, 255, (byte)Math.Round(opacity * 255));
                skPaint.Style = SKPaintStyle.StrokeAndFill;
                skCanvas.SaveLayer(skPaint);
                disposable.Add(skPaint);
                return skPaint;
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

        private const string MimeTypeSvg = "image/svg+xml";

        internal static object? GetImage(SvgImage svgImage, string uriString)
        {
            // Uri MaxLength is 65519 (https://msdn.microsoft.com/en-us/library/z6c2z492.aspx)
            // if using data URI scheme, very long URI may happen.
            var safeUriString = uriString.Length > 65519 ? uriString.Substring(0, 65519) : uriString;

            try
            {
                var uri = new Uri(safeUriString, UriKind.RelativeOrAbsolute);

                // handle data/uri embedded images (http://en.wikipedia.org/wiki/Data_URI_scheme)
                if (uri.IsAbsoluteUri && uri.Scheme == "data")
                    return GetImageFromDataUri(svgImage, uriString);

                if (!uri.IsAbsoluteUri)
                    uri = new Uri(svgImage.OwnerDocument.BaseUri, uri);

                // should work with http: and file: protocol urls
                var httpRequest = WebRequest.Create(uri);

                using (var webResponse = httpRequest.GetResponse())
                {
                    using (var stream = webResponse.GetResponseStream())
                    {
                        if (stream.CanSeek)
                            stream.Position = 0;

                        if (webResponse.ContentType.StartsWith(MimeTypeSvg, StringComparison.InvariantCultureIgnoreCase) ||
                            uri.LocalPath.EndsWith(".svg", StringComparison.InvariantCultureIgnoreCase))
                            return LoadSvg(stream, uri);
                        else
                            return SKImage.FromEncodedData(stream);
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static object? GetImageFromDataUri(SvgImage svgImage, string uriString)
        {
            var headerStartIndex = 5;
            var headerEndIndex = uriString.IndexOf(",", headerStartIndex);
            if (headerEndIndex < 0 || headerEndIndex + 1 >= uriString.Length)
                throw new Exception("Invalid data URI");

            var mimeType = "text/plain";
            var charset = "US-ASCII";
            var base64 = false;

            var headers = new List<string>(uriString.Substring(headerStartIndex, headerEndIndex - headerStartIndex).Split(';'));
            if (headers[0].Contains("/"))
            {
                mimeType = headers[0].Trim();
                headers.RemoveAt(0);
                charset = string.Empty;
            }

            if (headers.Count > 0 && headers[headers.Count - 1].Trim().Equals("base64", StringComparison.InvariantCultureIgnoreCase))
            {
                base64 = true;
                headers.RemoveAt(headers.Count - 1);
            }

            foreach (var param in headers)
            {
                var p = param.Split('=');
                if (p.Length < 2)
                    continue;

                var attribute = p[0].Trim();
                if (attribute.Equals("charset", StringComparison.InvariantCultureIgnoreCase))
                    charset = p[1].Trim();
            }

            var data = uriString.Substring(headerEndIndex + 1);
            if (mimeType.Equals(MimeTypeSvg, StringComparison.InvariantCultureIgnoreCase))
            {
                if (base64)
                {
                    var encoding = string.IsNullOrEmpty(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset);
                    data = encoding.GetString(Convert.FromBase64String(data));
                }
                using (var stream = new MemoryStream(Encoding.Default.GetBytes(data)))
                {
                    return LoadSvg(stream, svgImage.OwnerDocument.BaseUri);
                }
            }
            // support nonstandard "img" spelling of mimetype
            else if (mimeType.StartsWith("image/") || mimeType.StartsWith("img/"))
            {
                var dataBytes = base64 ? Convert.FromBase64String(data) : Encoding.Default.GetBytes(data);
                using (var stream = new MemoryStream(dataBytes))
                {
                    return SKImage.FromEncodedData(stream);
                }
            }
            else
                return null;
        }

        internal static SvgDocument LoadSvg(Stream stream, Uri baseUri)
        {
            var document = SvgDocument.Open<SvgDocument>(stream);
            document.BaseUri = baseUri;
            return document;
        }
    }
}
