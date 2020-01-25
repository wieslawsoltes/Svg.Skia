// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using SkiaSharp;

namespace Svg.Skia
{
    public abstract class Drawable : SKDrawable, IFilterSource, IPictureSource
    {
        internal CompositeDisposable _disposable = new CompositeDisposable();

        public bool IsDrawable;
        public IgnoreAttributes IgnoreAttributes;
        public bool IsAntialias;
        public SKRect TransformedBounds;
        public SKMatrix Transform;
        public SKRect? Clip;
        public SKPath? ClipPath;
        public MaskDrawable? MaskDrawable;
        public SKPaint? Mask;
        public SKPaint? MaskDstIn;
        public SKPaint? Opacity;
        public SKPaint? Filter;
        public SKPaint? Fill;
        public SKPaint? Stroke;

        protected override void OnDraw(SKCanvas canvas)
        {
            Draw(canvas, IgnoreAttributes);
        }

        protected override SKRect OnGetBounds()
        {
            if (IsDrawable)
            {
                return TransformedBounds;
            }
            return SKRect.Empty;
        }

        public virtual Drawable? HitTest(SKPoint skPoint)
        {
            if (TransformedBounds.Contains(skPoint))
            {
                return this;
            }
            return null;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _disposable?.Dispose();
        }

        protected bool CanDraw(SvgVisualElement svgVisualElement, IgnoreAttributes ignoreAttributes)
        {
            bool visible = svgVisualElement.Visible == true;
            bool display = ignoreAttributes.HasFlag(IgnoreAttributes.Display) ? true : !string.Equals(svgVisualElement.Display, "none", StringComparison.OrdinalIgnoreCase);
            return visible && display;
        }

        protected void CreateMaskPaints()
        {
            Mask = new SKPaint()
            {
                IsAntialias = true,
                Style = SKPaintStyle.StrokeAndFill
            };
            _disposable.Add(Mask);

            MaskDstIn = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.StrokeAndFill,
                BlendMode = SKBlendMode.DstIn,
                Color = new SKColor(0, 0, 0, 255),
                ColorFilter = SKColorFilter.CreateLumaColor()
            };
            _disposable.Add(MaskDstIn);
        }

        public abstract void OnDraw(SKCanvas canvas, IgnoreAttributes ignoreAttributes);

        public virtual void Draw(SKCanvas canvas, IgnoreAttributes ignoreAttributes)
        {
            if (!IsDrawable)
            {
                return;
            }

            var enableClip = !ignoreAttributes.HasFlag(IgnoreAttributes.Clip);
            var enableMask = !ignoreAttributes.HasFlag(IgnoreAttributes.Mask);
            var enableOpacity = !ignoreAttributes.HasFlag(IgnoreAttributes.Opacity);
            var enableFilter = !ignoreAttributes.HasFlag(IgnoreAttributes.Filter);

            canvas.Save();

            if (Clip != null)
            {
                canvas.ClipRect(Clip.Value, SKClipOperation.Intersect);
            }

            var skMatrixTotal = canvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref Transform);
            canvas.SetMatrix(skMatrixTotal);

            if (ClipPath != null && enableClip == true)
            {
                canvas.ClipPath(ClipPath, SKClipOperation.Intersect, IsAntialias);
            }

            if (MaskDrawable != null && enableMask == true)
            {
                canvas.SaveLayer(Mask);
            }

            if (Opacity != null && enableOpacity == true)
            {
                canvas.SaveLayer(Opacity);
            }

            if (Filter != null && enableFilter == true)
            {
                canvas.SaveLayer(Filter);
            }

            OnDraw(canvas, ignoreAttributes);

            if (Filter != null && enableFilter == true)
            {
                canvas.Restore();
            }

            if (Opacity != null && enableOpacity == true)
            {
                canvas.Restore();
            }

            if (MaskDrawable != null && enableMask == true)
            {
                canvas.SaveLayer(MaskDstIn);
                MaskDrawable.Draw(canvas, ignoreAttributes);
                canvas.Restore();
                canvas.Restore();
            }

            canvas.Restore();
        }

        SKPicture? IFilterSource.SourceGraphic()
        {
            var ignoreAttributes = IgnoreAttributes.Clip | IgnoreAttributes.Mask | IgnoreAttributes.Opacity | IgnoreAttributes.Filter;
            using var skPictureRecorder = new SKPictureRecorder();
            using var skCanvas = skPictureRecorder.BeginRecording(TransformedBounds);
            Draw(skCanvas, ignoreAttributes);
            return skPictureRecorder.EndRecording();
        }

        SKPicture? IFilterSource.BackgroundImage()
        {
            return null;
        }

        SKPaint? IFilterSource.FillPaint()
        {
            return Fill;
        }

        SKPaint? IFilterSource.StrokePaint()
        {
            return Stroke;
        }
    }
}
