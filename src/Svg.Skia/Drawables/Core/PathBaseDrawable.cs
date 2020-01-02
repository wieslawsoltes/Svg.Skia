// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using SkiaSharp;

namespace Svg.Skia
{
    internal abstract class PathBaseDrawable : BaseDrawable
    {
        protected SKPath? skPath;
        protected BaseDrawable? _markerStart;
        protected BaseDrawable? _markerMid;
        protected BaseDrawable? _markerEnd;

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

            if (_skPaintFill != null)
            {
                canvas.DrawPath(skPath, _skPaintFill);
            }

            if (_skPaintStroke != null)
            {
                canvas.DrawPath(skPath, _skPaintStroke);
            }

            if (_markerStart != null)
            {
                _markerStart.Draw(canvas, 0f, 0f);
            }

            if (_markerMid != null)
            {
                _markerMid.Draw(canvas, 0f, 0f);
            }

            if (_markerEnd != null)
            {
                _markerEnd.Draw(canvas, 0f, 0f);
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
