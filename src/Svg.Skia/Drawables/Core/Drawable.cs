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
        public SKPaint? PaintTransparentBlack;
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

            PaintTransparentBlack = new SKPaint()
            {
                Style = SKPaintStyle.StrokeAndFill
            };
            _disposable.Add(PaintTransparentBlack);

            PaintDstIn = new SKPaint
            {
                BlendMode = SKBlendMode.DstIn,
                Style = SKPaintStyle.StrokeAndFill
            };
            _disposable.Add(PaintDstIn);
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
