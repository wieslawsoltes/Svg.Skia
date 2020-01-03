// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
#if SVG_ANCHOR
    internal class AnchorDrawable : BaseDrawable
    {
        internal List<BaseDrawable> _childrenDrawable = new List<BaseDrawable>();

        public AnchorDrawable(SvgAnchor svgAnchor, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            _ignoreDisplay = ignoreDisplay;
            _canDraw = true;

            if (!_canDraw)
            {
                return;
            }

            foreach (var svgElement in svgAnchor.Children)
            {
                var drawable = DrawableFactory.Create(svgElement, skOwnerBounds, ignoreDisplay);
                if (drawable != null)
                {
                    _childrenDrawable.Add(drawable);
                    _disposable.Add(drawable);
                }
            }

            _antialias = SkiaUtil.IsAntialias(svgAnchor);

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

            _skMatrix = SkiaUtil.GetSKMatrix(svgAnchor.Transforms);
            _skPathClip = null;
            _skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgAnchor, _disposable);
            _skPaintFilter = null;

            _skPaintFill = null;
            _skPaintStroke = null;
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
#endif
}
