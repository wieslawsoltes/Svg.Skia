// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    internal class VisualElementDrawable : Drawable
    {
        public VisualElementDrawable(SvgVisualElement svgVisualElement, SKSize sKSize, bool ignoreDisplay)
        {
            _svgVisualElement = svgVisualElement;
            _ignoreDisplay = ignoreDisplay;

            _antialias = SkiaUtil.IsAntialias(svgVisualElement);

            _canDraw = CanDraw(svgVisualElement, _ignoreDisplay);
            if (!_canDraw)
            {
                return;
            }

            switch (svgVisualElement)
            {
                case SvgCircle svgCircle:
                    skPath = SkiaUtil.ToSKPath(svgCircle, svgCircle.FillRule, _disposable);
                    break;
                case SvgEllipse svgEllipse:
                    skPath = SkiaUtil.ToSKPath(svgEllipse, svgEllipse.FillRule, _disposable);
                    break;
                case SvgRectangle svgRectangle:
                    skPath = SkiaUtil.ToSKPath(svgRectangle, svgRectangle.FillRule, _disposable);
                    break;
                case SvgLine svgLine:
                    skPath = SkiaUtil.ToSKPath(svgLine, svgLine.FillRule, _disposable);
                    break;
                case SvgPath svgPath:
                    skPath = SkiaUtil.ToSKPath(svgPath.PathData, svgPath.FillRule, _disposable);
                    break;
                case SvgPolyline svgPolyline:
                    skPath = SkiaUtil.ToSKPath(svgPolyline.Points, svgPolyline.FillRule, false, _disposable);
                    break;
                case SvgPolygon svgPolygon:
                    skPath = SkiaUtil.ToSKPath(svgPolygon.Points, svgPolygon.FillRule, true, _disposable);
                    break;
                default:
                    break;
            }

            if (skPath == null || skPath.IsEmpty)
            {
                _canDraw = false;
                return;
            }

            _skBounds = skPath.Bounds;

            _skMatrix = SkiaUtil.GetSKMatrix(svgVisualElement.Transforms);
            skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgVisualElement, _skBounds, new HashSet<Uri>(), _disposable);
            skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgVisualElement, _disposable);
            skPaintFilter = SkiaUtil.GetFilterSKPaint(svgVisualElement, _disposable);

            if (SkiaUtil.IsValidFill(svgVisualElement))
            {
                skPaintFill = SkiaUtil.GetFillSKPaint(svgVisualElement, sKSize, _skBounds, _disposable);
            }

            if (SkiaUtil.IsValidStroke(svgVisualElement))
            {
                skPaintStroke = SkiaUtil.GetStrokeSKPaint(svgVisualElement, sKSize, _skBounds, _disposable);
            }
        }
    }
}
