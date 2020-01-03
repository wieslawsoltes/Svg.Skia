// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using SkiaSharp;
using Svg.Document_Structure;

namespace Svg.Skia
{
    internal class SymbolDrawable : BaseDrawable
    {
        internal List<BaseDrawable> _childrenDrawable = new List<BaseDrawable>();

        public SymbolDrawable(SvgSymbol svgSymbol, float x, float y, float width, float height, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            _ignoreDisplay = ignoreDisplay;
            _canDraw = CanDraw(svgSymbol, _ignoreDisplay);

            if (!_canDraw)
            {
                return;
            }

            if (svgSymbol.CustomAttributes.TryGetValue("width", out string? _widthString))
            {
                if (new SvgUnitConverter().ConvertFromString(_widthString) is SvgUnit _width)
                {
                    width = _width.ToDeviceValue(UnitRenderingType.Horizontal, svgSymbol, skOwnerBounds);
                }
            }

            if (svgSymbol.CustomAttributes.TryGetValue("height", out string? heightString))
            {
                if (new SvgUnitConverter().ConvertFromString(heightString) is SvgUnit _height)
                {
                    height = _height.ToDeviceValue(UnitRenderingType.Vertical, svgSymbol, skOwnerBounds);
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

            switch (svgOverflow)
            {
                case SvgOverflow.Auto:
                case SvgOverflow.Visible:
                case SvgOverflow.Inherit:
                    break;
                default:
                    _skClipRect = SKRect.Create(x, y, width, height);
                    break;
            }

            foreach (var svgElement in svgSymbol.Children)
            {
                var drawable = DrawableFactory.Create(svgElement, skOwnerBounds, ignoreDisplay);
                if (drawable != null)
                {
                    _childrenDrawable.Add(drawable);
                    _disposable.Add(drawable);
                }
            }

            _antialias = SkiaUtil.IsAntialias(svgSymbol);

            _skBounds = SKRect.Empty;

            foreach (var drawable in _childrenDrawable)
            {
                if (_skBounds.IsEmpty)
                {
                    _skBounds = drawable._skBounds;
                }
                else
                {
                    if (!drawable._skBounds.IsEmpty)
                    {
                        _skBounds = SKRect.Union(_skBounds, drawable._skBounds);
                    }
                }
            }

            // TODO: Transform _skBounds using _skMatrix.

            _skMatrix = SkiaUtil.GetSKMatrix(svgSymbol.Transforms);
            var skMatrixViewBox = SkiaUtil.GetSvgViewBoxTransform(svgSymbol.ViewBox, svgSymbol.AspectRatio, x, y, width, height);
            SKMatrix.PreConcat(ref _skMatrix, ref skMatrixViewBox);

            _skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgSymbol, _skBounds, new HashSet<Uri>(), _disposable);
            _skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgSymbol, _disposable);
            _skPaintFilter = SkiaUtil.GetFilterSKPaint(svgSymbol, _disposable);

            if (SkiaUtil.IsValidFill(svgSymbol))
            {
                _skPaintFill = SkiaUtil.GetFillSKPaint(svgSymbol, _skBounds, _disposable);
            }

            if (SkiaUtil.IsValidStroke(svgSymbol, _skBounds))
            {
                _skPaintStroke = SkiaUtil.GetStrokeSKPaint(svgSymbol, _skBounds, _disposable);
            }
        }

        protected override void OnDraw(SKCanvas canvas)
        {
            if (!_canDraw)
            {
                return;
            }

            canvas.Save();

            if (_skClipRect != null)
            {
                canvas.ClipRect(_skClipRect.Value, SKClipOperation.Intersect); 
            }

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

            foreach (var drawable in _childrenDrawable)
            {
                drawable.Draw(canvas, 0f, 0f);
            }

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
