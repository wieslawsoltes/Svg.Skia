// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
#define USE_DRAWABLES
using System;
using System.Collections.Generic;
using System.Reflection;
using SkiaSharp;
using Svg.DataTypes;
using Svg.Document_Structure;

namespace Svg.Skia
{
    public class SKSvgRenderer : ISvgRenderer
    {
        private readonly SKCanvas _skCanvas;
        private readonly SKSize _skSize;
        private readonly CompositeDisposable _disposable;

        public SKSvgRenderer(SKCanvas skCanvas, SKSize skSize)
        {
            _skCanvas = skCanvas;
            _skSize = skSize;
            _disposable = new CompositeDisposable();
        }

        public void Dispose()
        {
            _disposable?.Dispose();
        }

        internal bool CanDraw(SvgVisualElement svgVisualElement, bool ignoreDisplay)
        {
            bool visible = svgVisualElement.Visible == true;
            bool display = ignoreDisplay ? true : !string.Equals(svgVisualElement.Display, "none", StringComparison.OrdinalIgnoreCase);
            return visible && display;
        }

        internal void Draw(SvgElement svgElement, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            switch (svgElement)
            {
#if SVG_ANCHOR
                case SvgAnchor svgAnchor:
                    DrawAnchor(svgAnchor, ignoreDisplay);
                    break;
#endif
                case SvgFragment svgFragment:
                    DrawFragment(svgFragment, skOwnerBounds, ignoreDisplay);
                    break;
                case SvgImage svgImage:
                    DrawImage(svgImage, skOwnerBounds, ignoreDisplay);
                    break;
                case SvgSwitch svgSwitch:
                    DrawSwitch(svgSwitch, skOwnerBounds, ignoreDisplay);
                    break;
                case SvgUse svgUse:
                    DrawUse(svgUse, skOwnerBounds, ignoreDisplay);
                    break;
                case SvgForeignObject svgForeignObject:
                    DrawForeignObject(svgForeignObject, skOwnerBounds, ignoreDisplay);
                    break;
                case SvgCircle svgCircle:
#if USE_DRAWABLES
                    {
                        var drawable = new CircleDrawable(svgCircle, skOwnerBounds, ignoreDisplay);
                        drawable.Draw(_skCanvas, 0f, 0f);
                    }
#else
                    DrawCircle(svgCircle, skOwnerBounds, ignoreDisplay);
#endif
                    break;
                case SvgEllipse svgEllipse:
#if USE_DRAWABLES
                    {
                        var drawable = new EllipseDrawable(svgEllipse, skOwnerBounds, ignoreDisplay);
                        drawable.Draw(_skCanvas, 0f, 0f);
                    }
#else
                    DrawEllipse(svgEllipse, skOwnerBounds, ignoreDisplay);
#endif
                    break;
                case SvgRectangle svgRectangle:
#if USE_DRAWABLES
                    {
                        var drawable = new RectangleDrawable(svgRectangle, skOwnerBounds, ignoreDisplay);
                        drawable.Draw(_skCanvas, 0f, 0f);
                    }
#else
                    DrawRectangle(svgRectangle, skOwnerBounds, ignoreDisplay);
#endif
                    break;
                case SvgGlyph svgGlyph:
                    DrawGlyph(svgGlyph, skOwnerBounds, ignoreDisplay);
                    break;
                case SvgGroup svgGroup:
#if USE_DRAWABLES
                    {
                        var drawable = new GroupDrawable(svgGroup, skOwnerBounds, ignoreDisplay);
                        drawable.Draw(_skCanvas, 0f, 0f);
                    }
#else
                    DrawGroup(svgGroup, skOwnerBounds, ignoreDisplay);
#endif
                    break;
                case SvgLine svgLine:
#if USE_DRAWABLES
                    {
                        var drawable = new LineDrawable(svgLine, skOwnerBounds, ignoreDisplay);
                        drawable.Draw(_skCanvas, 0f, 0f);
                    }
#else
                    DrawLine(svgLine, ignoreDisplay);
#endif
                    break;
                case SvgPath svgPath:
#if USE_DRAWABLES
                    {
                        var drawable = new PathDrawable(svgPath, skOwnerBounds, ignoreDisplay);
                        drawable.Draw(_skCanvas, 0f, 0f);
                    }
#else
                    DrawPath(svgPath, ignoreDisplay);
#endif
                    break;
                case SvgPolyline svgPolyline:
#if USE_DRAWABLES
                    {
                        var drawable = new PolylineDrawable(svgPolyline, skOwnerBounds, ignoreDisplay);
                        drawable.Draw(_skCanvas, 0f, 0f);
                    }
#else
                    DrawPolyline(svgPolyline, skOwnerBounds, ignoreDisplay);
#endif
                    break;
                case SvgPolygon svgPolygon:
#if USE_DRAWABLES
                    {
                        var drawable = new PolygonDrawable(svgPolygon, skOwnerBounds, ignoreDisplay);
                        drawable.Draw(_skCanvas, 0f, 0f);
                    }
#else
                    DrawPolygon(svgPolygon, skOwnerBounds, ignoreDisplay);
#endif
                    break;
                case SvgText svgText:
                    DrawText(svgText, skOwnerBounds, ignoreDisplay);
                    break;
                default:
                    break;
            }
        }

        internal void DrawSymbol(SvgSymbol svgSymbol, float x, float y, float width, float height, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            if (!CanDraw(svgSymbol, ignoreDisplay))
            {
                return;
            }

            if (svgSymbol.CustomAttributes.TryGetValue("width", out string? _widthString))
            {
                if (new SvgUnitConverter().ConvertFromString(_widthString) is SvgUnit _width)
                {
                    width = _width.ToDeviceValue(null, UnitRenderingType.Horizontal, svgSymbol);
                }
            }

            if (svgSymbol.CustomAttributes.TryGetValue("height", out string? heightString))
            {
                if (new SvgUnitConverter().ConvertFromString(heightString) is SvgUnit _height)
                {
                    height = _height.ToDeviceValue(null, UnitRenderingType.Vertical, svgSymbol);
                }
            }

            SvgOverflow svgOverflow = SvgOverflow.Hidden;
            if (svgSymbol.TryGetAttribute("overflow", out string overflowString))
            {
                if (new SvgOverflowConverter().ConvertFromString(overflowString) is SvgOverflow _svgOverflow)
                {
                    svgOverflow = _svgOverflow;
                }
            }

            _skCanvas.Save();

            var skClipRect = SKRect.Create(x, y, width, height);

            switch (svgOverflow)
            {
                case SvgOverflow.Auto:
                case SvgOverflow.Visible:
                case SvgOverflow.Inherit:
                    break;
                default:
                    _skCanvas.ClipRect(skClipRect, SKClipOperation.Intersect);
                    break;
            }

            var skMatrixViewBox = SkiaUtil.GetSvgViewBoxTransform(svgSymbol.ViewBox, svgSymbol.AspectRatio, x, y, width, height);
            var skMatrix = SkiaUtil.GetSKMatrix(svgSymbol.Transforms);
            SKMatrix.PreConcat(ref skMatrix, ref skMatrixViewBox);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgSymbol, skClipRect, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SkiaUtil.IsAntialias(svgSymbol);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgSymbol, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SkiaUtil.GetFilterSKPaint(svgSymbol, _disposable);
            if (skPaintFilter != null)
            {
                _skCanvas.SaveLayer(skPaintFilter);
            }

            foreach (var svgElement in svgSymbol.Children)
            {
                Draw(svgElement, skOwnerBounds, ignoreDisplay);
            }

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

        internal void DrawMarker(SvgMarker svgMarker, SvgVisualElement pOwner, SKPoint pMarkerPoint, float fAngle, SKRect skOwnerBounds)
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

            Draw(markerElement, skOwnerBounds, true);

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

        internal void DrawMarker(SvgMarker svgMarker, SvgVisualElement pOwner, SKPoint pRefPoint, SKPoint pMarkerPoint1, SKPoint pMarkerPoint2, bool isStartMarker, SKRect skOwnerBounds)
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
            DrawMarker(svgMarker, pOwner, pRefPoint, fAngle1, skOwnerBounds);
        }

        internal void DrawMarker(SvgMarker svgMarker, SvgVisualElement pOwner, SKPoint pRefPoint, SKPoint pMarkerPoint1, SKPoint pMarkerPoint2, SKPoint pMarkerPoint3, SKRect skOwnerBounds)
        {
            float xDiff = pMarkerPoint2.X - pMarkerPoint1.X;
            float yDiff = pMarkerPoint2.Y - pMarkerPoint1.Y;
            float fAngle1 = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);
            xDiff = pMarkerPoint3.X - pMarkerPoint2.X;
            yDiff = pMarkerPoint3.Y - pMarkerPoint2.Y;
            float fAngle2 = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);
            DrawMarker(svgMarker, pOwner, pRefPoint, (fAngle1 + fAngle2) / 2, skOwnerBounds);
        }

        internal void DrawMarkers(SvgMarkerElement svgMarkerElement, SKPath sKPath, SKRect skOwnerBounds)
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
                    DrawMarker(marker, svgMarkerElement, refPoint1, refPoint1, refPoint2, true, skOwnerBounds);
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
                            DrawMarker(marker, svgMarkerElement, pathTypes[i].Point, pathTypes[i - 1].Point, pathTypes[i].Point, pathTypes[i + 1].Point, skOwnerBounds);
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
                    DrawMarker(marker, svgMarkerElement, refPoint1, refPoint2, pathTypes[pathLength - 1].Point, false, skOwnerBounds);
                }
            }
        }

        internal void AddMarkers(SvgGroup svgGroup)
        {
            Uri? marker = null;
            // TODO: The marker can not be set as presentation attribute.
            //if (svgGroup.TryGetAttribute("marker", out string markerUrl))
            //{
            //    marker = new Uri(markerUrl, UriKind.RelativeOrAbsolute);
            //}

            if (svgGroup.MarkerStart == null && svgGroup.MarkerMid == null && svgGroup.MarkerEnd == null && marker == null)
            {
                return;
            }

            foreach (var svgElement in svgGroup.Children)
            {
                if (svgElement is SvgMarkerElement svgMarkerElement)
                {
                    if (svgMarkerElement.MarkerStart == null)
                    {
                        if (svgGroup.MarkerStart != null)
                        {
                            svgMarkerElement.MarkerStart = svgGroup.MarkerStart;
                        }
                        else if (marker != null)
                        {
                            svgMarkerElement.MarkerStart = marker;
                        }
                    }
                    if (svgMarkerElement.MarkerMid == null)
                    {
                        if (svgGroup.MarkerMid != null)
                        {
                            svgMarkerElement.MarkerMid = svgGroup.MarkerMid;
                        }
                        else if (marker != null)
                        {
                            svgMarkerElement.MarkerMid = marker;
                        }
                    }
                    if (svgMarkerElement.MarkerEnd == null)
                    {
                        if (svgGroup.MarkerEnd != null)
                        {
                            svgMarkerElement.MarkerEnd = svgGroup.MarkerEnd;
                        }
                        else if (marker != null)
                        {
                            svgMarkerElement.MarkerEnd = marker;
                        }
                    }
                }
            }
        }

        internal void DrawTextPath(SvgTextPath svgTextPath, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            if (!CanDraw(svgTextPath, ignoreDisplay))
            {
                return;
            }

            if (SkiaUtil.HasRecursiveReference(svgTextPath, (e) => e.ReferencedPath, new HashSet<Uri>()))
            {
                return;
            }

            var svgPath = SkiaUtil.GetReference<SvgPath>(svgTextPath, svgTextPath.ReferencedPath);
            if (svgPath == null)
            {
                return;
            }

            var skPath = SkiaUtil.ToSKPath(svgPath.PathData, svgPath.FillRule, _disposable);
            if (skPath == null || skPath.IsEmpty)
            {
                return;
            }

            var skMatrixPath = SkiaUtil.GetSKMatrix(svgPath.Transforms);
            skPath.Transform(skMatrixPath);

            // TODO: Implement StartOffset
            var startOffset = svgTextPath.StartOffset.ToDeviceValue(null, UnitRenderingType.Other, svgTextPath);

            // TODO: Calculate correct bounds.
            var skBounds = SKRect.Create(0f, 0f, _skSize.Width, _skSize.Height);

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgTextPath.Transforms);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgTextPath, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SkiaUtil.IsAntialias(svgTextPath);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgTextPath, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SkiaUtil.GetFilterSKPaint(svgTextPath, _disposable);
            if (skPaintFilter != null)
            {
                _skCanvas.SaveLayer(skPaintFilter);
            }

            // TODO: Fix SvgTextPath rendering.
            bool isValidFill = SkiaUtil.IsValidFill(svgTextPath);
            bool isValidStroke = SkiaUtil.IsValidStroke(svgTextPath);

            if (isValidFill || isValidStroke)
            {
                var text = svgTextPath.Text?.Trim();

                if (!string.IsNullOrEmpty(text))
                {
                    if (SkiaUtil.IsValidFill(svgTextPath))
                    {
                        var skPaint = SkiaUtil.GetFillSKPaint(svgTextPath, skBounds, _disposable);
                        SkiaUtil.SetSKPaintText(svgTextPath, skBounds, skPaint, _disposable);
                        _skCanvas.DrawTextOnPath(text, skPath, startOffset, 0f, skPaint);
                    }

                    if (SkiaUtil.IsValidStroke(svgTextPath))
                    {
                        var skPaint = SkiaUtil.GetStrokeSKPaint(svgTextPath, skBounds, _disposable);
                        SkiaUtil.SetSKPaintText(svgTextPath, skBounds, skPaint, _disposable);
                        _skCanvas.DrawTextOnPath(text, skPath, startOffset, 0f, skPaint);
                    }
                }
            }

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

        internal void DrawTextRef(SvgTextRef svgTextRef, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            if (!CanDraw(svgTextRef, ignoreDisplay))
            {
                return;
            }

            if (SkiaUtil.HasRecursiveReference(svgTextRef, (e) => e.ReferencedElement, new HashSet<Uri>()))
            {
                return;
            }

            var svgReferencedText = SkiaUtil.GetReference<SvgText>(svgTextRef, svgTextRef.ReferencedElement);
            if (svgReferencedText == null)
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = SKRect.Create(0f, 0f, _skSize.Width, _skSize.Height);

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgTextRef.Transforms);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgTextRef, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SkiaUtil.IsAntialias(svgTextRef);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgTextRef, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SkiaUtil.GetFilterSKPaint(svgTextRef, _disposable);
            if (skPaintFilter != null)
            {
                _skCanvas.SaveLayer(skPaintFilter);
            }

            // TODO: Draw svgReferencedText

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

        internal void DrawTextSpan(SvgTextSpan svgTextSpan, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            if (!CanDraw(svgTextSpan, ignoreDisplay))
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = SKRect.Create(0f, 0f, _skSize.Width, _skSize.Height);

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgTextSpan.Transforms);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgTextSpan, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SkiaUtil.IsAntialias(svgTextSpan);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgTextSpan, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SkiaUtil.GetFilterSKPaint(svgTextSpan, _disposable);
            if (skPaintFilter != null)
            {
                _skCanvas.SaveLayer(skPaintFilter);
            }

            // TODO: Implement SvgTextSpan drawing.
            DrawTextBase(svgTextSpan, skOwnerBounds);

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

        internal void DrawTextString(SvgTextBase svgTextBase, string text, float x, float y, SKRect skOwnerBounds)
        {
            // TODO: Calculate correct bounds.
            var skBounds = SKRect.Create(0f, 0f, _skSize.Width, _skSize.Height);

            if (SkiaUtil.IsValidFill(svgTextBase))
            {
                var skPaint = SkiaUtil.GetFillSKPaint(svgTextBase, skBounds, _disposable);
                SkiaUtil.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
                _skCanvas.DrawText(text, x, y, skPaint);
            }

            if (SkiaUtil.IsValidStroke(svgTextBase))
            {
                var skPaint = SkiaUtil.GetStrokeSKPaint(svgTextBase, skBounds, _disposable);
                SkiaUtil.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
                _skCanvas.DrawText(text, x, y, skPaint);
            }
        }

        internal void DrawTextBase(SvgTextBase svgTextBase, SKRect skOwnerBounds)
        {
            // TODO: Fix SvgTextBase rendering.
            bool isValidFill = SkiaUtil.IsValidFill(svgTextBase);
            bool isValidStroke = SkiaUtil.IsValidStroke(svgTextBase);
            string? text = svgTextBase.Text?.Trim();

            if ((isValidFill || isValidStroke) && text != null && !string.IsNullOrEmpty(text))
            {
                var xCount = svgTextBase.X.Count;
                var yCount = svgTextBase.Y.Count;
                var dxCount = svgTextBase.Dx.Count;
                var dyCount = svgTextBase.Dy.Count;

                if (xCount >= 1 && yCount >= 1 && xCount == yCount && xCount == text.Length)
                {
                    // TODO: Fix text position rendering.
                    var points = new SKPoint[xCount];

                    for (int i = 0; i < xCount; i++)
                    {
                        float x = svgTextBase.X[i].ToDeviceValue(null, UnitRenderingType.HorizontalOffset, svgTextBase);
                        float y = svgTextBase.Y[i].ToDeviceValue(null, UnitRenderingType.VerticalOffset, svgTextBase);
                        points[i] = new SKPoint(x, y);
                    }

                    // TODO: Calculate correct bounds.
                    var skBounds = SKRect.Create(0f, 0f, _skSize.Width, _skSize.Height);

                    if (SkiaUtil.IsValidFill(svgTextBase))
                    {
                        var skPaint = SkiaUtil.GetFillSKPaint(svgTextBase, skBounds, _disposable);
                        SkiaUtil.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
                        _skCanvas.DrawPositionedText(text, points, skPaint);
                    }

                    if (SkiaUtil.IsValidStroke(svgTextBase))
                    {
                        var skPaint = SkiaUtil.GetStrokeSKPaint(svgTextBase, skBounds, _disposable);
                        SkiaUtil.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
                        _skCanvas.DrawPositionedText(text, points, skPaint);
                    }
                }
                else
                {
                    float x = 0f;
                    float y = 0f;
                    float dx = 0f;
                    float dy = 0f;

                    if (xCount >= 1)
                    {
                        x = svgTextBase.X[0].ToDeviceValue(null, UnitRenderingType.HorizontalOffset, svgTextBase);
                    }

                    if (yCount >= 1)
                    {
                        y = svgTextBase.Y[0].ToDeviceValue(null, UnitRenderingType.VerticalOffset, svgTextBase);
                    }

                    if (dxCount >= 1)
                    {
                        dx = svgTextBase.Dx[0].ToDeviceValue(null, UnitRenderingType.HorizontalOffset, svgTextBase);
                    }

                    if (dyCount >= 1)
                    {
                        dy = svgTextBase.Dy[0].ToDeviceValue(null, UnitRenderingType.VerticalOffset, svgTextBase);
                    }

                    DrawTextString(svgTextBase, text, x + dx, y + dy, skOwnerBounds);
                }
            }
        }

#if SVG_ANCHOR
        public void DrawAnchor(SvgAnchor svgAnchor, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgAnchor.Transforms);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgAnchor, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            foreach (var svgElement in svgAnchor.Children)
            {
                Draw(svgElement, skOwnerBounds, ignoreDisplay);
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }
#endif

        public void DrawFragment(SvgFragment svgFragment, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            float x = svgFragment.X.ToDeviceValue(null, UnitRenderingType.Horizontal, svgFragment);
            float y = svgFragment.Y.ToDeviceValue(null, UnitRenderingType.Vertical, svgFragment);
            var skSize = SkiaUtil.GetDimensions(svgFragment);

            _skCanvas.Save();

            switch (svgFragment.Overflow)
            {
                case SvgOverflow.Auto:
                case SvgOverflow.Visible:
                case SvgOverflow.Inherit:
                    break;
                default:
                    var skClipRect = SKRect.Create(x, y, skSize.Width, skSize.Height);
                    _skCanvas.ClipRect(skClipRect, SKClipOperation.Intersect);
                    break;
            }

            var skMatrixViewBox = SkiaUtil.GetSvgViewBoxTransform(svgFragment.ViewBox, svgFragment.AspectRatio, x, y, skSize.Width, skSize.Height);
            var skMatrix = SkiaUtil.GetSKMatrix(svgFragment.Transforms);
            SKMatrix.PreConcat(ref skMatrix, ref skMatrixViewBox);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgFragment, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            foreach (var svgElement in svgFragment.Children)
            {
                Draw(svgElement, skOwnerBounds, ignoreDisplay);
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }

        public void DrawImage(SvgImage svgImage, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            if (!CanDraw(svgImage, ignoreDisplay))
            {
                return;
            }

            float width = svgImage.Width.ToDeviceValue(null, UnitRenderingType.Horizontal, svgImage);
            float height = svgImage.Height.ToDeviceValue(null, UnitRenderingType.Vertical, svgImage);
            var location = svgImage.Location.ToDeviceValue(null, svgImage);

            if (width <= 0f || height <= 0f || svgImage.Href == null)
            {
                return;
            }

            // TODO: Check for image recursive references.
            //if (SkiaUtil.HasRecursiveReference(svgImage, (e) => e.Href))
            //{
            //    return;
            //}

            var image = SkiaUtil.GetImage(svgImage, svgImage.Href);
            var skImage = image as SKImage;
            var svgFragment = image as SvgFragment;
            if (skImage == null && svgFragment == null)
            {
                return;
            }

            if (skImage != null)
            {
                _disposable.Add(skImage);
            }

            SKRect srcRect = default;

            if (skImage != null)
            {
                srcRect = SKRect.Create(0f, 0f, skImage.Width, skImage.Height);
            }

            if (svgFragment != null)
            {
                var skSize = SkiaUtil.GetDimensions(svgFragment);
                srcRect = SKRect.Create(0f, 0f, skSize.Width, skSize.Height);
            }

            var destClip = SKRect.Create(location.X, location.Y, width, height);
            var destRect = destClip;

            var aspectRatio = svgImage.AspectRatio;
            if (aspectRatio.Align != SvgPreserveAspectRatio.none)
            {
                var fScaleX = destClip.Width / srcRect.Width;
                var fScaleY = destClip.Height / srcRect.Height;
                var xOffset = 0f;
                var yOffset = 0f;

                if (aspectRatio.Slice)
                {
                    fScaleX = Math.Max(fScaleX, fScaleY);
                    fScaleY = Math.Max(fScaleX, fScaleY);
                }
                else
                {
                    fScaleX = Math.Min(fScaleX, fScaleY);
                    fScaleY = Math.Min(fScaleX, fScaleY);
                }

                switch (aspectRatio.Align)
                {
                    case SvgPreserveAspectRatio.xMinYMin:
                        break;
                    case SvgPreserveAspectRatio.xMidYMin:
                        xOffset = (destClip.Width - srcRect.Width * fScaleX) / 2;
                        break;
                    case SvgPreserveAspectRatio.xMaxYMin:
                        xOffset = (destClip.Width - srcRect.Width * fScaleX);
                        break;
                    case SvgPreserveAspectRatio.xMinYMid:
                        yOffset = (destClip.Height - srcRect.Height * fScaleY) / 2;
                        break;
                    case SvgPreserveAspectRatio.xMidYMid:
                        xOffset = (destClip.Width - srcRect.Width * fScaleX) / 2;
                        yOffset = (destClip.Height - srcRect.Height * fScaleY) / 2;
                        break;
                    case SvgPreserveAspectRatio.xMaxYMid:
                        xOffset = (destClip.Width - srcRect.Width * fScaleX);
                        yOffset = (destClip.Height - srcRect.Height * fScaleY) / 2;
                        break;
                    case SvgPreserveAspectRatio.xMinYMax:
                        yOffset = (destClip.Height - srcRect.Height * fScaleY);
                        break;
                    case SvgPreserveAspectRatio.xMidYMax:
                        xOffset = (destClip.Width - srcRect.Width * fScaleX) / 2;
                        yOffset = (destClip.Height - srcRect.Height * fScaleY);
                        break;
                    case SvgPreserveAspectRatio.xMaxYMax:
                        xOffset = (destClip.Width - srcRect.Width * fScaleX);
                        yOffset = (destClip.Height - srcRect.Height * fScaleY);
                        break;
                }

                destRect = SKRect.Create(
                    destClip.Left + xOffset, destClip.Top + yOffset,
                    srcRect.Width * fScaleX, srcRect.Height * fScaleY);
            }

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgImage.Transforms);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgImage, destRect, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SkiaUtil.IsAntialias(svgImage);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgImage, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SkiaUtil.GetFilterSKPaint(svgImage, _disposable);
            if (skPaintFilter != null)
            {
                _skCanvas.SaveLayer(skPaintFilter);
            }

            _skCanvas.ClipRect(destClip, SKClipOperation.Intersect);

            var skClipRect = SkiaUtil.GetClipRect(svgImage, destClip);
            if (skClipRect != null)
            {
                _skCanvas.ClipRect(skClipRect.Value, SKClipOperation.Intersect);
            }

            if (skImage != null)
            {
                _skCanvas.DrawImage(skImage, srcRect, destRect);
            }

            if (svgFragment != null)
            {
                _skCanvas.Save();

                float dx = destRect.Left;
                float dy = destRect.Top;
                float sx = destRect.Width / srcRect.Width;
                float sy = destRect.Height / srcRect.Height;
                var skTranslationMatrix = SKMatrix.MakeTranslation(dx, dy);
                var skScaleMatrix = SKMatrix.MakeScale(sx, sy);
                SKMatrix.PreConcat(ref skTranslationMatrix, ref skScaleMatrix);

                skMatrixTotal = _skCanvas.TotalMatrix;
                SKMatrix.PreConcat(ref skMatrixTotal, ref skTranslationMatrix);
                _skCanvas.SetMatrix(skMatrixTotal);

                DrawFragment(svgFragment, skOwnerBounds, ignoreDisplay);

                _skCanvas.Restore();
            }

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

        public void DrawSwitch(SvgSwitch svgSwitch, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            if (!CanDraw(svgSwitch, ignoreDisplay))
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = SKRect.Create(0f, 0f, _skSize.Width, _skSize.Height);

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgSwitch.Transforms);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgSwitch, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SkiaUtil.IsAntialias(svgSwitch);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgSwitch, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SkiaUtil.GetFilterSKPaint(svgSwitch, _disposable);
            if (skPaintFilter != null)
            {
                _skCanvas.SaveLayer(skPaintFilter);
            }

            // TODO: Implement SvgSwitch drawing

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

        public void DrawUse(SvgUse svgUse, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            if (!CanDraw(svgUse, ignoreDisplay))
            {
                return;
            }

            if (SkiaUtil.HasRecursiveReference(svgUse, (e) => e.ReferencedElement, new HashSet<Uri>()))
            {
                return;
            }

            var svgVisualElement = SkiaUtil.GetReference<SvgVisualElement>(svgUse, svgUse.ReferencedElement);
            if (svgVisualElement == null)
            {
                return;
            }

            float x = svgUse.X.ToDeviceValue(null, UnitRenderingType.Horizontal, svgUse);
            float y = svgUse.Y.ToDeviceValue(null, UnitRenderingType.Vertical, svgUse);
            float width = svgUse.Width.ToDeviceValue(null, UnitRenderingType.Horizontal, svgUse);
            float height = svgUse.Height.ToDeviceValue(null, UnitRenderingType.Vertical, svgUse);

            if (width <= 0f)
            {
                width = new SvgUnit(SvgUnitType.Percentage, 100f).ToDeviceValue(null, UnitRenderingType.Horizontal, svgUse);
            }

            if (height <= 0f)
            {
                height = new SvgUnit(SvgUnitType.Percentage, 100f).ToDeviceValue(null, UnitRenderingType.Vertical, svgUse);
            }

            var skMatrix = SkiaUtil.GetSKMatrix(svgUse.Transforms);
            if (!(svgVisualElement is SvgSymbol))
            {
                var skMatrixTranslateXY = SKMatrix.MakeTranslation(x, y);
                SKMatrix.PreConcat(ref skMatrix, ref skMatrixTranslateXY);
            }

            var originalParent = svgUse.Parent;
            var useParent = svgUse.GetType().GetField("_parent", BindingFlags.NonPublic | BindingFlags.Instance);
            if (useParent != null)
            {
                useParent.SetValue(svgVisualElement, svgUse);
            }

            svgVisualElement.InvalidateChildPaths();

            // TODO: Calculate correct bounds from SvgSymbol or SvgVisualElement.
            var skBounds = SKRect.Create(x, y, width, height);

            _skCanvas.Save();

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgUse, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SkiaUtil.IsAntialias(svgUse);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgUse, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SkiaUtil.GetFilterSKPaint(svgUse, _disposable);
            if (skPaintFilter != null)
            {
                _skCanvas.SaveLayer(skPaintFilter);
            }

            if (svgVisualElement is SvgSymbol svgSymbol)
            {
                DrawSymbol(svgSymbol, x, y, width, height, skOwnerBounds, ignoreDisplay);
            }
            else
            {
                Draw(svgVisualElement, skOwnerBounds, ignoreDisplay);
            }

            if (skPaintFilter != null)
            {
                _skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();

            if (useParent != null)
            {
                useParent.SetValue(svgVisualElement, originalParent);
            }
        }

        public void DrawForeignObject(SvgForeignObject svgForeignObject, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            if (!CanDraw(svgForeignObject, ignoreDisplay))
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = SKRect.Create(0f, 0f, _skSize.Width, _skSize.Height);

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgForeignObject.Transforms);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgForeignObject, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SkiaUtil.IsAntialias(svgForeignObject);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgForeignObject, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SkiaUtil.GetFilterSKPaint(svgForeignObject, _disposable);
            if (skPaintFilter != null)
            {
                _skCanvas.SaveLayer(skPaintFilter);
            }

            // TODO: Draw SvgForeignObject

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

        public void DrawCircle(SvgCircle svgCircle, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            if (!CanDraw(svgCircle, ignoreDisplay))
            {
                return;
            }

            var skPath = SkiaUtil.ToSKPath(svgCircle, svgCircle.FillRule, skOwnerBounds, _disposable);
            if (skPath == null || skPath.IsEmpty)
            {
                return;
            }

            var skBounds = skPath.Bounds;

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgCircle.Transforms);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgCircle, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SkiaUtil.IsAntialias(svgCircle);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgCircle, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SkiaUtil.GetFilterSKPaint(svgCircle, _disposable);
            if (skPaintFilter != null)
            {
                _skCanvas.SaveLayer(skPaintFilter);
            }

            if (SkiaUtil.IsValidFill(svgCircle))
            {
                var skPaintFill = SkiaUtil.GetFillSKPaint(svgCircle, skBounds, _disposable);
                _skCanvas.DrawPath(skPath, skPaintFill);
            }

            if (SkiaUtil.IsValidStroke(svgCircle))
            {
                var skPaintStroke = SkiaUtil.GetStrokeSKPaint(svgCircle, skBounds, _disposable);
                _skCanvas.DrawPath(skPath, skPaintStroke);
            }

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

        public void DrawEllipse(SvgEllipse svgEllipse, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            if (!CanDraw(svgEllipse, ignoreDisplay))
            {
                return;
            }

            var skPath = SkiaUtil.ToSKPath(svgEllipse, svgEllipse.FillRule, skOwnerBounds, _disposable);
            if (skPath == null || skPath.IsEmpty)
            {
                return;
            }

            var skBounds = skPath.Bounds;

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgEllipse.Transforms);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgEllipse, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SkiaUtil.IsAntialias(svgEllipse);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgEllipse, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SkiaUtil.GetFilterSKPaint(svgEllipse, _disposable);
            if (skPaintFilter != null)
            {
                _skCanvas.SaveLayer(skPaintFilter);
            }

            if (SkiaUtil.IsValidFill(svgEllipse))
            {
                var skPaintFill = SkiaUtil.GetFillSKPaint(svgEllipse, skBounds, _disposable);
                _skCanvas.DrawPath(skPath, skPaintFill);
            }

            if (SkiaUtil.IsValidStroke(svgEllipse))
            {
                var skPaintStroke = SkiaUtil.GetStrokeSKPaint(svgEllipse, skBounds, _disposable);
                _skCanvas.DrawPath(skPath, skPaintStroke);
            }

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

        public void DrawRectangle(SvgRectangle svgRectangle, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            if (!CanDraw(svgRectangle, ignoreDisplay))
            {
                return;
            }

            var skPath = SkiaUtil.ToSKPath(svgRectangle, svgRectangle.FillRule, skOwnerBounds, _disposable);
            if (skPath == null || skPath.IsEmpty)
            {
                return;
            }

            var skBounds = skPath.Bounds;

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgRectangle.Transforms);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgRectangle, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SkiaUtil.IsAntialias(svgRectangle);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgRectangle, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SkiaUtil.GetFilterSKPaint(svgRectangle, _disposable);
            if (skPaintFilter != null)
            {
                _skCanvas.SaveLayer(skPaintFilter);
            }

            if (SkiaUtil.IsValidFill(svgRectangle))
            {
                var skPaintFill = SkiaUtil.GetFillSKPaint(svgRectangle, skBounds, _disposable);
                _skCanvas.DrawPath(skPath, skPaintFill);
            }

            if (SkiaUtil.IsValidStroke(svgRectangle))
            {
                var skPaintStroke = SkiaUtil.GetStrokeSKPaint(svgRectangle, skBounds, _disposable);
                _skCanvas.DrawPath(skPath, skPaintStroke);
            }

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

        public void DrawGlyph(SvgGlyph svgGlyph, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            if (!CanDraw(svgGlyph, ignoreDisplay))
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = SKRect.Create(0f, 0f, _skSize.Width, _skSize.Height);

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgGlyph.Transforms);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgGlyph, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SkiaUtil.IsAntialias(svgGlyph);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgGlyph, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SkiaUtil.GetFilterSKPaint(svgGlyph, _disposable);
            if (skPaintFilter != null)
            {
                _skCanvas.SaveLayer(skPaintFilter);
            }

            // TODO: Draw SvgGlyph

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

        public void DrawGroup(SvgGroup svgGroup, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            if (!CanDraw(svgGroup, ignoreDisplay))
            {
                return;
            }

            // TODO: Call AddMarkers only once.
            AddMarkers(svgGroup);

            // TODO: Calculate correct bounds using Children bounds.
            var skBounds = SKRect.Create(0f, 0f, _skSize.Width, _skSize.Height);

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgGroup.Transforms);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgGroup, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SkiaUtil.IsAntialias(svgGroup);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgGroup, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SkiaUtil.GetFilterSKPaint(svgGroup, _disposable);
            if (skPaintFilter != null)
            {
                _skCanvas.SaveLayer(skPaintFilter);
            }

            foreach (var svgElement in svgGroup.Children)
            {
                Draw(svgElement, skOwnerBounds, ignoreDisplay);
            }

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

        public void DrawLine(SvgLine svgLine, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            if (!CanDraw(svgLine, ignoreDisplay))
            {
                return;
            }

            var skPath = SkiaUtil.ToSKPath(svgLine, svgLine.FillRule, skOwnerBounds, _disposable);
            if (skPath == null || skPath.IsEmpty)
            {
                return;
            }

            var skBounds = skPath.Bounds;

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgLine.Transforms);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgLine, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SkiaUtil.IsAntialias(svgLine);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgLine, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SkiaUtil.GetFilterSKPaint(svgLine, _disposable);
            if (skPaintFilter != null)
            {
                _skCanvas.SaveLayer(skPaintFilter);
            }

            if (SkiaUtil.IsValidStroke(svgLine))
            {
                var skPaint = SkiaUtil.GetStrokeSKPaint(svgLine, skBounds, _disposable);
                _skCanvas.DrawPath(skPath, skPaint);
            }

            DrawMarkers(svgLine, skPath, skOwnerBounds);

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

        public void DrawPath(SvgPath svgPath, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            if (!CanDraw(svgPath, ignoreDisplay))
            {
                return;
            }

            var skPath = SkiaUtil.ToSKPath(svgPath.PathData, svgPath.FillRule, _disposable);
            if (skPath == null || skPath.IsEmpty)
            {
                return;
            }

            var skBounds = skPath.Bounds;

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgPath.Transforms);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgPath, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SkiaUtil.IsAntialias(svgPath);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgPath, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SkiaUtil.GetFilterSKPaint(svgPath, _disposable);
            if (skPaintFilter != null)
            {
                _skCanvas.SaveLayer(skPaintFilter);
            }

            if (SkiaUtil.IsValidFill(svgPath))
            {
                var skPaint = SkiaUtil.GetFillSKPaint(svgPath, skBounds, _disposable);
                _skCanvas.DrawPath(skPath, skPaint);
            }

            if (SkiaUtil.IsValidStroke(svgPath))
            {
                var skPaint = SkiaUtil.GetStrokeSKPaint(svgPath, skBounds, _disposable);
                _skCanvas.DrawPath(skPath, skPaint);
            }

            DrawMarkers(svgPath, skPath, skOwnerBounds);

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

        public void DrawPolyline(SvgPolyline svgPolyline, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            if (!CanDraw(svgPolyline, ignoreDisplay))
            {
                return;
            }

            var skPath = SkiaUtil.ToSKPath(svgPolyline.Points, svgPolyline.FillRule, false, skOwnerBounds, _disposable);
            if (skPath == null || skPath.IsEmpty)
            {
                return;
            }

            var skBounds = skPath.Bounds;

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgPolyline.Transforms);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgPolyline, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SkiaUtil.IsAntialias(svgPolyline);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgPolyline, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SkiaUtil.GetFilterSKPaint(svgPolyline, _disposable);
            if (skPaintFilter != null)
            {
                _skCanvas.SaveLayer(skPaintFilter);
            }

            if (SkiaUtil.IsValidFill(svgPolyline))
            {
                var skPaint = SkiaUtil.GetFillSKPaint(svgPolyline, skBounds, _disposable);
                _skCanvas.DrawPath(skPath, skPaint);
            }

            if (SkiaUtil.IsValidStroke(svgPolyline))
            {
                var skPaint = SkiaUtil.GetStrokeSKPaint(svgPolyline, skBounds, _disposable);
                _skCanvas.DrawPath(skPath, skPaint);
            }

            DrawMarkers(svgPolyline, skPath, skOwnerBounds);

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

        public void DrawPolygon(SvgPolygon svgPolygon, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            if (!CanDraw(svgPolygon, ignoreDisplay))
            {
                return;
            }

            var skPath = SkiaUtil.ToSKPath(svgPolygon.Points, svgPolygon.FillRule, true, skOwnerBounds, _disposable);
            if (skPath == null || skPath.IsEmpty)
            {
                return;
            }

            var skBounds = skPath.Bounds;

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgPolygon.Transforms);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgPolygon, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SkiaUtil.IsAntialias(svgPolygon);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgPolygon, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SkiaUtil.GetFilterSKPaint(svgPolygon, _disposable);
            if (skPaintFilter != null)
            {
                _skCanvas.SaveLayer(skPaintFilter);
            }

            if (SkiaUtil.IsValidFill(svgPolygon))
            {
                var skPaint = SkiaUtil.GetFillSKPaint(svgPolygon, skBounds, _disposable);
                _skCanvas.DrawPath(skPath, skPaint);
            }

            if (SkiaUtil.IsValidStroke(svgPolygon))
            {
                var skPaint = SkiaUtil.GetStrokeSKPaint(svgPolygon, skBounds, _disposable);
                _skCanvas.DrawPath(skPath, skPaint);
            }

            DrawMarkers(svgPolygon, skPath, skOwnerBounds);

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

        public void DrawText(SvgText svgText, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            if (!CanDraw(svgText, ignoreDisplay))
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = SKRect.Create(0f, 0f, _skSize.Width, _skSize.Height);

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgText.Transforms);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgText, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SkiaUtil.IsAntialias(svgText);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgText, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SkiaUtil.GetFilterSKPaint(svgText, _disposable);
            if (skPaintFilter != null)
            {
                _skCanvas.SaveLayer(skPaintFilter);
            }

            DrawTextBase(svgText, skOwnerBounds);

            foreach (var svgElement in svgText.Children)
            {
                switch (svgElement)
                {
                    case SvgTextPath svgTextPath:
                        DrawTextPath(svgTextPath, skOwnerBounds, ignoreDisplay);
                        break;
                    case SvgTextRef svgTextRef:
                        DrawTextRef(svgTextRef, skOwnerBounds, ignoreDisplay);
                        break;
                    case SvgTextSpan svgTextSpan:
                        DrawTextSpan(svgTextSpan, skOwnerBounds, ignoreDisplay);
                        break;
                    default:
                        break;
                }
            }

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
    }
}
