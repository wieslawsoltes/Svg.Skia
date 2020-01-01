// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using SkiaSharp;

namespace Svg.Skia
{
    internal abstract class Drawable : SKDrawable
    {
        protected CompositeDisposable _disposable = new CompositeDisposable();
        protected SvgVisualElement? _svgVisualElement;
        protected bool _canDraw;
        protected bool _ignoreDisplay;
        protected bool _antialias;
        protected SKPath? skPath;
        protected SKPath? skPathClip;
        protected SKPaint? skPaintOpacity;
        protected SKPaint? skPaintFilter;
        protected SKPaint? skPaintFill;
        protected SKPaint? skPaintStroke;
        protected SKRect _skBounds;
        protected SKMatrix _skMatrix;

        protected bool CanDraw(SvgVisualElement svgVisualElement, bool ignoreDisplay)
        {
            bool visible = svgVisualElement.Visible == true;
            bool display = ignoreDisplay ? true : !string.Equals(svgVisualElement.Display, "none", StringComparison.OrdinalIgnoreCase);
            return visible && display;
        }

        protected override void OnDraw(SKCanvas canvas)
        {
            base.OnDraw(canvas);

            if (!_canDraw)
            {
                return;
            }

            canvas.Save();

            var skMatrixTotal = canvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref _skMatrix);
            canvas.SetMatrix(skMatrixTotal);

            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                canvas.ClipPath(skPathClip, SKClipOperation.Intersect, _antialias);
            }

            if (skPaintOpacity != null)
            {
                canvas.SaveLayer(skPaintOpacity);
            }

            if (skPaintFilter != null)
            {
                canvas.SaveLayer(skPaintFilter);
            }

            if (skPaintFill != null)
            {
                canvas.DrawPath(skPath, skPaintFill);
            }

            if (skPaintStroke != null)
            {
                canvas.DrawPath(skPath, skPaintStroke);
            }

            if (skPaintFilter != null)
            {
                canvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                canvas.Restore();
            }

            canvas.Restore();
        }
        protected override SKRect OnGetBounds()
        {
            if (_canDraw)
            {
                return _skBounds;
            }
            return SKRect.Empty;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _disposable?.Dispose();
        }
    }
}
