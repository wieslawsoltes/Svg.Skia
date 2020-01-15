// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using SkiaSharp;

namespace Svg.Skia
{
    public abstract class Drawable : SKDrawable
    {
        internal CompositeDisposable _disposable = new CompositeDisposable();

        public bool IsDrawable;
        public bool IgnoreDisplay;
        public bool IsAntialias;
        public SKRect TransformedBounds;
        public SKMatrix Transform;
        public SKRect? ClipRect;
        public SKPath? PathClip;
        public SKPicture? PictureMask;
        public SKPaint? PaintMask;
        public SKPaint? PaintDstIn;
        public SKPaint? PaintOpacity;
        public SKPaint? PaintFilter;
        public SKPaint? PaintFill;
        public SKPaint? PaintStroke;

        protected bool CanDraw(SvgVisualElement svgVisualElement, bool ignoreDisplay)
        {
            bool visible = svgVisualElement.Visible == true;
            bool display = ignoreDisplay ? true : !string.Equals(svgVisualElement.Display, "none", StringComparison.OrdinalIgnoreCase);
            return visible && display;
        }

        protected override SKRect OnGetBounds()
        {
            if (IsDrawable)
            {
                return TransformedBounds;
            }
            return SKRect.Empty;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _disposable?.Dispose();
        }

        protected void CreateMaskPaints()
        {
            if (PictureMask == null)
            {
                return;
            }

            PaintMask = new SKPaint()
            {
            };
            _disposable.Add(PaintMask);

            PaintDstIn = new SKPaint
            {
                BlendMode = SKBlendMode.DstIn,
                ColorFilter = SKColorFilter.CreateColorMatrix(
                    new float[]
                    {
                        0, 0, 0, 0, 0,
                        0, 0, 0, 0, 0,
                        0, 0, 0, 0, 0,
                        0.2125f, 0.7154f, 0.0721f, 0, 0
                    })
            };
            _disposable.Add(PaintDstIn);
        }

        protected abstract void Draw(SKCanvas canvas);

        protected override void OnDraw(SKCanvas canvas)
        {
            if (!IsDrawable)
            {
                return;
            }

            canvas.Save();

            if (ClipRect != null)
            {
                canvas.ClipRect(ClipRect.Value, SKClipOperation.Intersect);
            }

            var skMatrixTotal = canvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref Transform);
            canvas.SetMatrix(skMatrixTotal);

            if (PathClip != null && !PathClip.IsEmpty)
            {
                canvas.ClipPath(PathClip, SKClipOperation.Intersect, IsAntialias);
            }

            if (PictureMask != null && PaintOpacity == null)
            {
                canvas.SaveLayer(PaintMask);
            }

            if (PaintOpacity != null)
            {
                canvas.SaveLayer(PaintOpacity);
            }

            if (PaintFilter != null)
            {
                canvas.SaveLayer(PaintFilter);
            }

            Draw(canvas);

            if (PaintFilter != null)
            {
                canvas.Restore();
            }

            if (PaintOpacity != null && PictureMask == null)
            {
                canvas.Restore();
            }

            if (PictureMask != null)
            {
                canvas.SaveLayer(PaintDstIn);
                canvas.DrawPicture(PictureMask);
                canvas.Restore();
                canvas.Restore();
            }

            canvas.Restore();
        }

        public virtual Drawable? HitTest(SKPoint skPoint)
        {
            if (TransformedBounds.Contains(skPoint))
            {
                return this;
            }
            return null;
        }
    }
}
