// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

        internal void SetTransform(SKMatrix skMatrix)
        {
            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);
        }

        internal void SetClipPath(SvgVisualElement svgVisualElement, SKRect sKRectBounds)
        {
            var skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgVisualElement, sKRectBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SkiaUtil.IsAntialias(svgVisualElement);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }
        }

        internal void SetClip(SvgVisualElement svgVisualElement, SKRect sKRectBounds)
        {
            var clip = svgVisualElement.Clip;
            if (!string.IsNullOrEmpty(clip) && clip.StartsWith("rect("))
            {
                clip = clip.Trim();
                var offsets = (from o in clip.Substring(5, clip.Length - 6).Split(',')
                               select float.Parse(o.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture)).ToList();
                var skClipRect = SKRect.Create(
                    sKRectBounds.Left + offsets[3],
                    sKRectBounds.Top + offsets[0],
                    sKRectBounds.Width - (offsets[3] + offsets[1]),
                    sKRectBounds.Height - (offsets[2] + offsets[0]));
                _skCanvas.ClipRect(skClipRect, SKClipOperation.Intersect);
            }
        }

        internal SKPaint? SetOpacity(SvgElement svgElement)
        {
            float opacity = SkiaUtil.AdjustSvgOpacity(svgElement.Opacity);
            if (opacity < 1f)
            {
                var skPaint = new SKPaint()
                {
                    IsAntialias = true,
                };
                skPaint.Color = new SKColor(255, 255, 255, (byte)Math.Round(opacity * 255));
                skPaint.Style = SKPaintStyle.StrokeAndFill;
                _skCanvas.SaveLayer(skPaint);
                _disposable.Add(skPaint);
                return skPaint;
            }
            return null;
        }

        internal SKPaint? SetFilter(SvgVisualElement svgVisualElement)
        {
            if (svgVisualElement.Filter != null)
            {
                var skPaint = new SKPaint();
                skPaint.Style = SKPaintStyle.StrokeAndFill;
                SkiaUtil.SetFilter(svgVisualElement, skPaint, _disposable);
                _skCanvas.SaveLayer(skPaint);
                _disposable.Add(skPaint);
                return skPaint;
            }
            return null;
        }

        internal void Draw(SvgElement svgElement, bool ignoreDisplay)
        {
            switch (svgElement)
            {
#if SVG_ANCHOR
                case SvgAnchor svgAnchor:
                    DrawAnchor(svgAnchor, ignoreDisplay);
                    break;
#endif
                case SvgFragment svgFragment:
                    DrawFragment(svgFragment, ignoreDisplay);
                    break;
                case SvgImage svgImage:
                    DrawImage(svgImage, ignoreDisplay);
                    break;
                case SvgSwitch svgSwitch:
                    DrawSwitch(svgSwitch, ignoreDisplay);
                    break;
                case SvgUse svgUse:
                    DrawUse(svgUse, ignoreDisplay);
                    break;
                case SvgForeignObject svgForeignObject:
                    DrawForeignObject(svgForeignObject, ignoreDisplay);
                    break;
                case SvgCircle svgCircle:
                    DrawCircle(svgCircle, ignoreDisplay);
                    break;
                case SvgEllipse svgEllipse:
                    DrawEllipse(svgEllipse, ignoreDisplay);
                    break;
                case SvgRectangle svgRectangle:
                    DrawRectangle(svgRectangle, ignoreDisplay);
                    break;
                case SvgGlyph svgGlyph:
                    DrawGlyph(svgGlyph, ignoreDisplay);
                    break;
                case SvgGroup svgGroup:
                    DrawGroup(svgGroup, ignoreDisplay);
                    break;
                case SvgLine svgLine:
                    DrawLine(svgLine, ignoreDisplay);
                    break;
                case SvgPath svgPath:
                    DrawPath(svgPath, ignoreDisplay);
                    break;
                case SvgPolyline svgPolyline:
                    DrawPolyline(svgPolyline, ignoreDisplay);
                    break;
                case SvgPolygon svgPolygon:
                    DrawPolygon(svgPolygon, ignoreDisplay);
                    break;
                case SvgText svgText:
                    DrawText(svgText, ignoreDisplay);
                    break;
                default:
                    break;
            }
        }

        internal void DrawSymbol(SvgSymbol svgSymbol, float x, float y, float width, float height, bool ignoreDisplay)
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
            SetTransform(skMatrix);
            SetClipPath(svgSymbol, skClipRect);

            var skPaintOpacity = SetOpacity(svgSymbol);

            var skPaintFilter = SetFilter(svgSymbol);

            foreach (var svgElement in svgSymbol.Children)
            {
                Draw(svgElement, ignoreDisplay);
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

        internal void DrawMarker(SvgMarker svgMarker, SvgVisualElement pOwner, SKPoint pMarkerPoint, float fAngle)
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
            SetTransform(skMatrix);
            SetClipPath(svgMarker, skClipRect);

            var skPaintOpacity = SetOpacity(svgMarker);

            var skPaintFilter = SetFilter(svgMarker);

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

            Draw(markerElement, true);

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

        internal void DrawMarker(SvgMarker svgMarker, SvgVisualElement pOwner, SKPoint pRefPoint, SKPoint pMarkerPoint1, SKPoint pMarkerPoint2, bool isStartMarker)
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
            DrawMarker(svgMarker, pOwner, pRefPoint, fAngle1);
        }

        internal void DrawMarker(SvgMarker svgMarker, SvgVisualElement pOwner, SKPoint pRefPoint, SKPoint pMarkerPoint1, SKPoint pMarkerPoint2, SKPoint pMarkerPoint3)
        {
            float xDiff = pMarkerPoint2.X - pMarkerPoint1.X;
            float yDiff = pMarkerPoint2.Y - pMarkerPoint1.Y;
            float fAngle1 = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);
            xDiff = pMarkerPoint3.X - pMarkerPoint2.X;
            yDiff = pMarkerPoint3.Y - pMarkerPoint2.Y;
            float fAngle2 = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);
            DrawMarker(svgMarker, pOwner, pRefPoint, (fAngle1 + fAngle2) / 2);
        }

        internal void DrawMarkers(SvgMarkerElement svgMarkerElement, SKPath sKPath)
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
                    DrawMarker(marker, svgMarkerElement, refPoint1, refPoint1, refPoint2, true);
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
                            DrawMarker(marker, svgMarkerElement, pathTypes[i].Point, pathTypes[i - 1].Point, pathTypes[i].Point, pathTypes[i + 1].Point);
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
                    DrawMarker(marker, svgMarkerElement, refPoint1, refPoint2, pathTypes[pathLength - 1].Point, false);
                }
            }
        }

        internal void AddMarkers(SvgGroup svgGroup)
        {
            Uri? marker = null;
            // TODO: marker can not be set as presentation attribute
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

        internal void DrawTextPath(SvgTextPath svgTextPath, bool ignoreDisplay)
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
            SetTransform(skMatrix);
            SetClipPath(svgTextPath, skBounds);

            var skPaintOpacity = SetOpacity(svgTextPath);

            var skPaintFilter = SetFilter(svgTextPath);

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
                        var skPaint = SkiaUtil.GetFillSKPaint(svgTextPath, _skSize, skBounds, _disposable);
                        SkiaUtil.SetSKPaintText(svgTextPath, _skSize, skBounds, skPaint, _disposable);
                        _skCanvas.DrawTextOnPath(text, skPath, startOffset, 0f, skPaint);
                    }

                    if (SkiaUtil.IsValidStroke(svgTextPath))
                    {
                        var skPaint = SkiaUtil.GetStrokeSKPaint(svgTextPath, _skSize, skBounds, _disposable);
                        SkiaUtil.SetSKPaintText(svgTextPath, _skSize, skBounds, skPaint, _disposable);
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

        internal void DrawTextRef(SvgTextRef svgTextRef, bool ignoreDisplay)
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
            SetTransform(skMatrix);
            SetClipPath(svgTextRef, skBounds);

            var skPaintOpacity = SetOpacity(svgTextRef);

            var skPaintFilter = SetFilter(svgTextRef);

            // TODO: svgReferencedText

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

        internal void DrawTextSpan(SvgTextSpan svgTextSpan, bool ignoreDisplay)
        {
            if (!CanDraw(svgTextSpan, ignoreDisplay))
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = SKRect.Create(0f, 0f, _skSize.Width, _skSize.Height);

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgTextSpan.Transforms);
            SetTransform(skMatrix);
            SetClipPath(svgTextSpan, skBounds);

            var skPaintOpacity = SetOpacity(svgTextSpan);

            var skPaintFilter = SetFilter(svgTextSpan);

            // TODO:
            DrawTextBase(svgTextSpan);

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

        internal void DrawTextString(SvgTextBase svgTextBase, string text, float x, float y)
        {
            // TODO: Calculate correct bounds.
            var skBounds = SKRect.Create(0f, 0f, _skSize.Width, _skSize.Height);

            if (SkiaUtil.IsValidFill(svgTextBase))
            {
                var skPaint = SkiaUtil.GetFillSKPaint(svgTextBase, _skSize, skBounds, _disposable);
                SkiaUtil.SetSKPaintText(svgTextBase, _skSize, skBounds, skPaint, _disposable);
                _skCanvas.DrawText(text, x, y, skPaint);
            }

            if (SkiaUtil.IsValidStroke(svgTextBase))
            {
                var skPaint = SkiaUtil.GetStrokeSKPaint(svgTextBase, _skSize, skBounds, _disposable);
                SkiaUtil.SetSKPaintText(svgTextBase, _skSize, skBounds, skPaint, _disposable);
                _skCanvas.DrawText(text, x, y, skPaint);
            }
        }

        internal void DrawTextBase(SvgTextBase svgTextBase)
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
                        var skPaint = SkiaUtil.GetFillSKPaint(svgTextBase, _skSize, skBounds, _disposable);
                        SkiaUtil.SetSKPaintText(svgTextBase, _skSize, skBounds, skPaint, _disposable);
                        _skCanvas.DrawPositionedText(text, points, skPaint);
                    }

                    if (SkiaUtil.IsValidStroke(svgTextBase))
                    {
                        var skPaint = SkiaUtil.GetStrokeSKPaint(svgTextBase, _skSize, skBounds, _disposable);
                        SkiaUtil.SetSKPaintText(svgTextBase, _skSize, skBounds, skPaint, _disposable);
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

                    DrawTextString(svgTextBase, text, x + dx, y + dy);
                }
            }
        }

        internal bool CanDraw(SvgVisualElement svgVisualElement, bool ignoreDisplay)
        {
            bool visible = svgVisualElement.Visible == true;
            bool display = ignoreDisplay ? true : !string.Equals(svgVisualElement.Display, "none", StringComparison.OrdinalIgnoreCase);
            return visible && display;
        }

#if SVG_ANCHOR
        public void DrawAnchor(SvgAnchor svgAnchor, bool ignoreDisplay)
        {
            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgAnchor.Transforms);
            SetTransform(skMatrix);

            var skPaintOpacity = SetOpacity(svgAnchor);

            foreach (var svgElement in svgAnchor.Children)
            {
                Draw(svgElement, ignoreDisplay);
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }
#endif

        public void DrawFragment(SvgFragment svgFragment, bool ignoreDisplay)
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
            SetTransform(skMatrix);

            var skPaintOpacity = SetOpacity(svgFragment);

            foreach (var svgElement in svgFragment.Children)
            {
                Draw(svgElement, ignoreDisplay);
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }

        public void DrawImage(SvgImage svgImage, bool ignoreDisplay)
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
            SetTransform(skMatrix);
            SetClipPath(svgImage, destRect);

            var skPaintOpacity = SetOpacity(svgImage);

            var skPaintFilter = SetFilter(svgImage);

            _skCanvas.ClipRect(destClip, SKClipOperation.Intersect);

            SetClip(svgImage, destClip);

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
                SetTransform(skTranslationMatrix);

                DrawFragment(svgFragment, ignoreDisplay);

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

        public void DrawSwitch(SvgSwitch svgSwitch, bool ignoreDisplay)
        {
            if (!CanDraw(svgSwitch, ignoreDisplay))
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = SKRect.Create(0f, 0f, _skSize.Width, _skSize.Height);

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgSwitch.Transforms);
            SetTransform(skMatrix);
            SetClipPath(svgSwitch, skBounds);

            var skPaintOpacity = SetOpacity(svgSwitch);

            var skPaintFilter = SetFilter(svgSwitch);

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

        public void DrawUse(SvgUse svgUse, bool ignoreDisplay)
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

            // TODO:
            var skBounds = SKRect.Create(x, y, width, height);

            _skCanvas.Save();

            SetTransform(skMatrix);
            SetClipPath(svgUse, skBounds);

            var skPaintOpacity = SetOpacity(svgUse);

            var skPaintFilter = SetFilter(svgUse);

            if (svgVisualElement is SvgSymbol svgSymbol)
            {
                DrawSymbol(svgSymbol, x, y, width, height, ignoreDisplay);
            }
            else
            {
                Draw(svgVisualElement, ignoreDisplay);
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

        public void DrawForeignObject(SvgForeignObject svgForeignObject, bool ignoreDisplay)
        {
            if (!CanDraw(svgForeignObject, ignoreDisplay))
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = SKRect.Create(0f, 0f, _skSize.Width, _skSize.Height);

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgForeignObject.Transforms);
            SetTransform(skMatrix);
            SetClipPath(svgForeignObject, skBounds);

            var skPaintOpacity = SetOpacity(svgForeignObject);

            var skPaintFilter = SetFilter(svgForeignObject);

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

        public void DrawCircle(SvgCircle svgCircle, bool ignoreDisplay)
        {
            if (!CanDraw(svgCircle, ignoreDisplay))
            {
                return;
            }

            var skPath = SkiaUtil.ToSKPath(svgCircle, svgCircle.FillRule, _disposable);
            if (skPath == null || skPath.IsEmpty)
            {
                return;
            }

            var skBounds = skPath.Bounds;

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgCircle.Transforms);
            SetTransform(skMatrix);
            SetClipPath(svgCircle, skBounds);

            var skPaintOpacity = SetOpacity(svgCircle);

            var skPaintFilter = SetFilter(svgCircle);

            if (SkiaUtil.IsValidFill(svgCircle))
            {
                var skPaintFill = SkiaUtil.GetFillSKPaint(svgCircle, _skSize, skBounds, _disposable);
                _skCanvas.DrawPath(skPath, skPaintFill);
            }

            if (SkiaUtil.IsValidStroke(svgCircle))
            {
                var skPaintStroke = SkiaUtil.GetStrokeSKPaint(svgCircle, _skSize, skBounds, _disposable);
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

        public void DrawEllipse(SvgEllipse svgEllipse, bool ignoreDisplay)
        {
            if (!CanDraw(svgEllipse, ignoreDisplay))
            {
                return;
            }

            var skPath = SkiaUtil.ToSKPath(svgEllipse, svgEllipse.FillRule, _disposable);
            if (skPath == null || skPath.IsEmpty)
            {
                return;
            }

            var skBounds = skPath.Bounds;

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgEllipse.Transforms);
            SetTransform(skMatrix);
            SetClipPath(svgEllipse, skBounds);

            var skPaintOpacity = SetOpacity(svgEllipse);

            var skPaintFilter = SetFilter(svgEllipse);

            if (SkiaUtil.IsValidFill(svgEllipse))
            {
                var skPaintFill = SkiaUtil.GetFillSKPaint(svgEllipse, _skSize, skBounds, _disposable);
                _skCanvas.DrawPath(skPath, skPaintFill);
            }

            if (SkiaUtil.IsValidStroke(svgEllipse))
            {
                var skPaintStroke = SkiaUtil.GetStrokeSKPaint(svgEllipse, _skSize, skBounds, _disposable);
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

        public void DrawRectangle(SvgRectangle svgRectangle, bool ignoreDisplay)
        {
            if (!CanDraw(svgRectangle, ignoreDisplay))
            {
                return;
            }

            var skPath = SkiaUtil.ToSKPath(svgRectangle, svgRectangle.FillRule, _disposable);
            if (skPath == null || skPath.IsEmpty)
            {
                return;
            }

            var skBounds = skPath.Bounds;

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgRectangle.Transforms);
            SetTransform(skMatrix);
            SetClipPath(svgRectangle, skBounds);

            var skPaintOpacity = SetOpacity(svgRectangle);

            var skPaintFilter = SetFilter(svgRectangle);

            if (SkiaUtil.IsValidFill(svgRectangle))
            {
                var skPaintFill = SkiaUtil.GetFillSKPaint(svgRectangle, _skSize, skBounds, _disposable);
                _skCanvas.DrawPath(skPath, skPaintFill);
            }

            if (SkiaUtil.IsValidStroke(svgRectangle))
            {
                var skPaintStroke = SkiaUtil.GetStrokeSKPaint(svgRectangle, _skSize, skBounds, _disposable);
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

        public void DrawGlyph(SvgGlyph svgGlyph, bool ignoreDisplay)
        {
            if (!CanDraw(svgGlyph, ignoreDisplay))
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = SKRect.Create(0f, 0f, _skSize.Width, _skSize.Height);

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgGlyph.Transforms);
            SetTransform(skMatrix);
            SetClipPath(svgGlyph, skBounds);

            var skPaintOpacity = SetOpacity(svgGlyph);

            var skPaintFilter = SetFilter(svgGlyph);

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

        public void DrawGroup(SvgGroup svgGroup, bool ignoreDisplay)
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
            SetTransform(skMatrix);
            SetClipPath(svgGroup, skBounds);

            var skPaintOpacity = SetOpacity(svgGroup);

            var skPaintFilter = SetFilter(svgGroup);

            foreach (var svgElement in svgGroup.Children)
            {
                Draw(svgElement, ignoreDisplay);
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

        public void DrawLine(SvgLine svgLine, bool ignoreDisplay)
        {
            if (!CanDraw(svgLine, ignoreDisplay))
            {
                return;
            }

            var skPath = SkiaUtil.ToSKPath(svgLine, svgLine.FillRule, _disposable);
            if (skPath == null || skPath.IsEmpty)
            {
                return;
            }

            var skBounds = skPath.Bounds;

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgLine.Transforms);
            SetTransform(skMatrix);
            SetClipPath(svgLine, skBounds);

            var skPaintOpacity = SetOpacity(svgLine);

            var skPaintFilter = SetFilter(svgLine);

            if (SkiaUtil.IsValidStroke(svgLine))
            {
                var skPaint = SkiaUtil.GetStrokeSKPaint(svgLine, _skSize, skBounds, _disposable);
                _skCanvas.DrawPath(skPath, skPaint);
            }

            DrawMarkers(svgLine, skPath);

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

        public void DrawPath(SvgPath svgPath, bool ignoreDisplay)
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
            SetTransform(skMatrix);
            SetClipPath(svgPath, skBounds);

            var skPaintOpacity = SetOpacity(svgPath);

            var skPaintFilter = SetFilter(svgPath);

            if (SkiaUtil.IsValidFill(svgPath))
            {
                var skPaint = SkiaUtil.GetFillSKPaint(svgPath, _skSize, skBounds, _disposable);
                _skCanvas.DrawPath(skPath, skPaint);
            }

            if (SkiaUtil.IsValidStroke(svgPath))
            {
                var skPaint = SkiaUtil.GetStrokeSKPaint(svgPath, _skSize, skBounds, _disposable);
                _skCanvas.DrawPath(skPath, skPaint);
            }

            DrawMarkers(svgPath, skPath);

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

        public void DrawPolyline(SvgPolyline svgPolyline, bool ignoreDisplay)
        {
            if (!CanDraw(svgPolyline, ignoreDisplay))
            {
                return;
            }

            var skPath = SkiaUtil.ToSKPath(svgPolyline.Points, svgPolyline.FillRule, false, _disposable);
            if (skPath == null || skPath.IsEmpty)
            {
                return;
            }

            var skBounds = skPath.Bounds;

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgPolyline.Transforms);
            SetTransform(skMatrix);
            SetClipPath(svgPolyline, skBounds);

            var skPaintOpacity = SetOpacity(svgPolyline);

            var skPaintFilter = SetFilter(svgPolyline);

            if (SkiaUtil.IsValidFill(svgPolyline))
            {
                var skPaint = SkiaUtil.GetFillSKPaint(svgPolyline, _skSize, skBounds, _disposable);
                _skCanvas.DrawPath(skPath, skPaint);
            }

            if (SkiaUtil.IsValidStroke(svgPolyline))
            {
                var skPaint = SkiaUtil.GetStrokeSKPaint(svgPolyline, _skSize, skBounds, _disposable);
                _skCanvas.DrawPath(skPath, skPaint);
            }

            DrawMarkers(svgPolyline, skPath);

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

        public void DrawPolygon(SvgPolygon svgPolygon, bool ignoreDisplay)
        {
            if (!CanDraw(svgPolygon, ignoreDisplay))
            {
                return;
            }

            var skPath = SkiaUtil.ToSKPath(svgPolygon.Points, svgPolygon.FillRule, true, _disposable);
            if (skPath == null || skPath.IsEmpty)
            {
                return;
            }

            var skBounds = skPath.Bounds;

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgPolygon.Transforms);
            SetTransform(skMatrix);
            SetClipPath(svgPolygon, skBounds);

            var skPaintOpacity = SetOpacity(svgPolygon);

            var skPaintFilter = SetFilter(svgPolygon);

            if (SkiaUtil.IsValidFill(svgPolygon))
            {
                var skPaint = SkiaUtil.GetFillSKPaint(svgPolygon, _skSize, skBounds, _disposable);
                _skCanvas.DrawPath(skPath, skPaint);
            }

            if (SkiaUtil.IsValidStroke(svgPolygon))
            {
                var skPaint = SkiaUtil.GetStrokeSKPaint(svgPolygon, _skSize, skBounds, _disposable);
                _skCanvas.DrawPath(skPath, skPaint);
            }

            DrawMarkers(svgPolygon, skPath);

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

        public void DrawText(SvgText svgText, bool ignoreDisplay)
        {
            if (!CanDraw(svgText, ignoreDisplay))
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = SKRect.Create(0f, 0f, _skSize.Width, _skSize.Height);

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgText.Transforms);
            SetTransform(skMatrix);
            SetClipPath(svgText, skBounds);

            var skPaintOpacity = SetOpacity(svgText);

            var skPaintFilter = SetFilter(svgText);

            DrawTextBase(svgText);

            foreach (var svgElement in svgText.Children)
            {
                switch (svgElement)
                {
                    case SvgTextPath svgTextPath:
                        DrawTextPath(svgTextPath, ignoreDisplay);
                        break;
                    case SvgTextRef svgTextRef:
                        DrawTextRef(svgTextRef, ignoreDisplay);
                        break;
                    case SvgTextSpan svgTextSpan:
                        DrawTextSpan(svgTextSpan, ignoreDisplay);
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
