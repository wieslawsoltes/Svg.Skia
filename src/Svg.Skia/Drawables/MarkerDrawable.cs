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
    internal class MarkerDrawable : Drawable
    {
        internal Drawable? _markerDrawable;

        public MarkerDrawable(SvgMarker svgMarker, SvgVisualElement pOwner, SKPoint pMarkerPoint, float fAngle, SKRect skOwnerBounds)
        {
            _ignoreDisplay = true;
            _canDraw = true;

            if (!_canDraw)
            {
                return;
            }

            var markerElement = GetMarkerElement(svgMarker);
            if (markerElement == null)
            {
                _canDraw = false;
                return;
            }

            var skMarkerMatrix = SKMatrix.MakeIdentity();

            var skMatrixMarkerPoint = SKMatrix.MakeTranslation(pMarkerPoint.X, pMarkerPoint.Y);
            SKMatrix.PreConcat(ref skMarkerMatrix, ref skMatrixMarkerPoint);

            var skMatrixAngle = SKMatrix.MakeRotationDegrees(svgMarker.Orient.IsAuto ? fAngle : svgMarker.Orient.Angle);
            SKMatrix.PreConcat(ref skMarkerMatrix, ref skMatrixAngle);

            var strokeWidth = pOwner.StrokeWidth.ToDeviceValue(UnitRenderingType.Other, svgMarker, skOwnerBounds);

            var refX = svgMarker.RefX.ToDeviceValue(UnitRenderingType.Horizontal, svgMarker, skOwnerBounds);
            var refY = svgMarker.RefY.ToDeviceValue(UnitRenderingType.Vertical, svgMarker, skOwnerBounds);
            float markerWidth = svgMarker.MarkerWidth.ToDeviceValue(UnitRenderingType.Other, svgMarker, skOwnerBounds);
            float markerHeight = svgMarker.MarkerHeight.ToDeviceValue(UnitRenderingType.Other, svgMarker, skOwnerBounds);
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

            _skClipRect = SKRect.Create(
                svgMarker.ViewBox.MinX,
                svgMarker.ViewBox.MinY,
                markerWidth / viewBoxToMarkerUnitsScaleX,
                markerHeight / viewBoxToMarkerUnitsScaleY);

            var drawable = DrawableFactory.Create(markerElement, skOwnerBounds, true);
            if (drawable != null)
            {
                _markerDrawable = drawable;
                _disposable.Add(_markerDrawable);
            }
            else
            {
                _canDraw = false;
                return;
            }

            _antialias = SkiaUtil.IsAntialias(svgMarker);

            _skBounds = _markerDrawable._skBounds;

            _skMatrix = SkiaUtil.GetSKMatrix(svgMarker.Transforms);
            SKMatrix.PreConcat(ref _skMatrix, ref skMarkerMatrix);

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref _skMatrix, out _skBounds, ref _skBounds);

            _skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgMarker, _skBounds, new HashSet<Uri>(), _disposable);
            _skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgMarker, _disposable);
            _skPaintFilter = SkiaUtil.GetFilterSKPaint(svgMarker, _disposable);

            _skPaintFill = null;
            _skPaintStroke = null;
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

        protected override void OnDraw(SKCanvas canvas)
        {
            if (!_canDraw)
            {
                return;
            }

            canvas.Save();

            var skMatrixTotal = canvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref _skMatrix);
            canvas.SetMatrix(skMatrixTotal);

            if (_skPathClip != null && !_skPathClip.IsEmpty)
            {
                canvas.ClipPath(_skPathClip, SKClipOperation.Intersect, _antialias);
            }

            if (_skPaintOpacity != null)
            {
                canvas.SaveLayer(_skPaintOpacity);
            }

            if (_skPaintFilter != null)
            {
                canvas.SaveLayer(_skPaintFilter);
            }

            if (_skClipRect != null)
            {
                canvas.ClipRect(_skClipRect.Value, SKClipOperation.Intersect);
            }

            _markerDrawable?.Draw(canvas, 0f, 0f);

            if (_skPaintFilter != null)
            {
                canvas.Restore();
            }

            if (_skPaintOpacity != null)
            {
                canvas.Restore();
            }

            canvas.Restore();
        }
    }
}
