// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using SkiaSharp;
using Svg.DataTypes;

namespace Svg.Skia
{
    internal class MarkerDrawable : BaseDrawable
    {
        // TODO: Implement drawable.

        internal SvgVisualElement? GetMarkerElement(SvgMarker svgMarker)
        {
            SvgVisualElement? markerElement = null;

            foreach (var child in svgMarker.Children)
            {
                if (child is SvgVisualElement svgVisualElement)
                {
                    markerElement = svgVisualElement;
                    break;
                }
            }

            return markerElement;
        }

        internal void DrawMarker(SKCanvas _skCanvas, SvgMarker svgMarker, SvgVisualElement pOwner, SKPoint pMarkerPoint, float fAngle)
        {
            var markerElement = GetMarkerElement(svgMarker);
            if (markerElement == null)
            {
                return;
            }

            var skMarkerMatrix = SKMatrix.MakeIdentity();

            var skMatrixMarkerPoint = SKMatrix.MakeTranslation(pMarkerPoint.X, pMarkerPoint.Y);
            SKMatrix.PreConcat(ref skMarkerMatrix, ref skMatrixMarkerPoint);

            var skMatrixAngle = SKMatrix.MakeRotationDegrees(svgMarker.Orient.IsAuto ? fAngle : svgMarker.Orient.Angle);
            SKMatrix.PreConcat(ref skMarkerMatrix, ref skMatrixAngle);

            var strokeWidth = pOwner.StrokeWidth.ToDeviceValue(null, UnitRenderingType.Other, svgMarker);

            var refX = svgMarker.RefX.ToDeviceValue(null, UnitRenderingType.Horizontal, svgMarker);
            var refY = svgMarker.RefY.ToDeviceValue(null, UnitRenderingType.Horizontal, svgMarker);
            float markerWidth = svgMarker.MarkerWidth;
            float markerHeight = svgMarker.MarkerHeight;
            float viewBoxToMarkerUnitsScaleX = 1f;
            float viewBoxToMarkerUnitsScaleY = 1f;

            switch (svgMarker.MarkerUnits)
            {
                case SvgMarkerUnits.StrokeWidth:
                    {
                        var skMatrixStrokeWidth = SKMatrix.MakeScale(strokeWidth, strokeWidth);
                        SKMatrix.PreConcat(ref skMarkerMatrix, ref skMatrixStrokeWidth);

                        var viewBoxWidth = svgMarker.ViewBox.Width;
                        var viewBoxHeight = svgMarker.ViewBox.Height;

                        var scaleFactorWidth = (viewBoxWidth <= 0) ? 1 : (markerWidth / viewBoxWidth);
                        var scaleFactorHeight = (viewBoxHeight <= 0) ? 1 : (markerHeight / viewBoxHeight);

                        viewBoxToMarkerUnitsScaleX = Math.Min(scaleFactorWidth, scaleFactorHeight);
                        viewBoxToMarkerUnitsScaleY = Math.Min(scaleFactorWidth, scaleFactorHeight);

                        var skMatrixTranslateRefXY = SKMatrix.MakeTranslation(-refX * viewBoxToMarkerUnitsScaleX, -refY * viewBoxToMarkerUnitsScaleY);
                        SKMatrix.PreConcat(ref skMarkerMatrix, ref skMatrixTranslateRefXY);

                        var skMatrixScaleXY = SKMatrix.MakeScale(viewBoxToMarkerUnitsScaleX, viewBoxToMarkerUnitsScaleY);
                        SKMatrix.PreConcat(ref skMarkerMatrix, ref skMatrixScaleXY);
                    }
                    break;
                case SvgMarkerUnits.UserSpaceOnUse:
                    {
                        var skMatrixTranslateRefXY = SKMatrix.MakeTranslation(-refX, -refY);
                        SKMatrix.PreConcat(ref skMarkerMatrix, ref skMatrixTranslateRefXY);
                    }
                    break;
            }

            var skClipRect = SKRect.Create(
                svgMarker.ViewBox.MinX,
                svgMarker.ViewBox.MinY,
                markerWidth / viewBoxToMarkerUnitsScaleX,
                markerHeight / viewBoxToMarkerUnitsScaleY);

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgMarker.Transforms);
            SKMatrix.PreConcat(ref skMatrix, ref skMarkerMatrix);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgMarker, skClipRect, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SkiaUtil.IsAntialias(svgMarker);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgMarker, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SkiaUtil.GetFilterSKPaint(svgMarker, _disposable);
            if (skPaintFilter != null)
            {
                _skCanvas.SaveLayer(skPaintFilter);
            }

            switch (svgMarker.Overflow)
            {
                case SvgOverflow.Auto:
                case SvgOverflow.Visible:
                case SvgOverflow.Inherit:
                    break;
                default:
                    _skCanvas.ClipRect(skClipRect, SKClipOperation.Intersect);
                    break;
            }

            // TODO: Draw(markerElement, true);

            if (skPaintFilter != null)
            {
                _skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }

        internal void DrawMarker(SKCanvas _skCanvas, SvgMarker svgMarker, SvgVisualElement pOwner, SKPoint pRefPoint, SKPoint pMarkerPoint1, SKPoint pMarkerPoint2, bool isStartMarker)
        {
            float fAngle1 = 0f;
            if (svgMarker.Orient.IsAuto)
            {
                float xDiff = pMarkerPoint2.X - pMarkerPoint1.X;
                float yDiff = pMarkerPoint2.Y - pMarkerPoint1.Y;
                fAngle1 = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);
                if (isStartMarker && svgMarker.Orient.IsAutoStartReverse)
                {
                    fAngle1 += 180;
                }
            }
            DrawMarker(_skCanvas, svgMarker, pOwner, pRefPoint, fAngle1);
        }

        internal void DrawMarker(SKCanvas _skCanvas, SvgMarker svgMarker, SvgVisualElement pOwner, SKPoint pRefPoint, SKPoint pMarkerPoint1, SKPoint pMarkerPoint2, SKPoint pMarkerPoint3)
        {
            float xDiff = pMarkerPoint2.X - pMarkerPoint1.X;
            float yDiff = pMarkerPoint2.Y - pMarkerPoint1.Y;
            float fAngle1 = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);
            xDiff = pMarkerPoint3.X - pMarkerPoint2.X;
            yDiff = pMarkerPoint3.Y - pMarkerPoint2.Y;
            float fAngle2 = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);
            DrawMarker(_skCanvas, svgMarker, pOwner, pRefPoint, (fAngle1 + fAngle2) / 2);
        }

        internal void DrawMarkers(SKCanvas _skCanvas, SvgMarkerElement svgMarkerElement, SKPath sKPath)
        {
            var pathTypes = SkiaUtil.GetPathTypes(sKPath);
            var pathLength = pathTypes.Count;

            if (svgMarkerElement.MarkerStart != null && !SkiaUtil.HasRecursiveReference(svgMarkerElement, (e) => e.MarkerStart, new HashSet<Uri>()))
            {
                var marker = SkiaUtil.GetReference<SvgMarker>(svgMarkerElement, svgMarkerElement.MarkerStart);
                if (marker != null)
                {
                    var refPoint1 = pathTypes[0].Point;
                    var index = 1;
                    while (index < pathLength && pathTypes[index].Point == refPoint1)
                    {
                        ++index;
                    }
                    var refPoint2 = pathTypes[index].Point;
                    DrawMarker(_skCanvas, marker, svgMarkerElement, refPoint1, refPoint1, refPoint2, true);
                }
            }

            if (svgMarkerElement.MarkerMid != null && !SkiaUtil.HasRecursiveReference(svgMarkerElement, (e) => e.MarkerMid, new HashSet<Uri>()))
            {
                var marker = SkiaUtil.GetReference<SvgMarker>(svgMarkerElement, svgMarkerElement.MarkerMid);
                if (marker != null)
                {
                    int bezierIndex = -1;
                    for (int i = 1; i <= pathLength - 2; i++)
                    {
                        // for Bezier curves, the marker shall only been shown at the last point
                        if ((pathTypes[i].Type & (byte)PathPointType.PathTypeMask) == (byte)PathPointType.Bezier)
                            bezierIndex = (bezierIndex + 1) % 3;
                        else
                            bezierIndex = -1;

                        if (bezierIndex == -1 || bezierIndex == 2)
                        {
                            DrawMarker(_skCanvas, marker, svgMarkerElement, pathTypes[i].Point, pathTypes[i - 1].Point, pathTypes[i].Point, pathTypes[i + 1].Point);
                        }
                    }
                }
            }

            if (svgMarkerElement.MarkerEnd != null && !SkiaUtil.HasRecursiveReference(svgMarkerElement, (e) => e.MarkerEnd, new HashSet<Uri>()))
            {
                var marker = SkiaUtil.GetReference<SvgMarker>(svgMarkerElement, svgMarkerElement.MarkerEnd);
                if (marker != null)
                {
                    var index = pathLength - 1;
                    var refPoint1 = pathTypes[index].Point;
                    --index;
                    while (index > 0 && pathTypes[index].Point == refPoint1)
                    {
                        --index;
                    }
                    var refPoint2 = pathTypes[index].Point;
                    DrawMarker(_skCanvas, marker, svgMarkerElement, refPoint1, refPoint2, pathTypes[pathLength - 1].Point, false);
                }
            }
        }
    }
}
