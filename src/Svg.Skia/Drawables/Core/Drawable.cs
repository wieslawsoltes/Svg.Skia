// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public abstract class Drawable : SKDrawable, IFilterSource, IPictureSource
    {
        internal CompositeDisposable _disposable = new CompositeDisposable();

        public SvgElement? Element;
        public Drawable? Root;
        public Drawable? Parent;
        public bool IsDrawable;
        public Attributes IgnoreAttributes;
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

        public Drawable(SvgElement? element, Drawable? root, Drawable? parent)
        {
            Element = element;
            Root = root;
            Parent = parent;
        }

        protected override void OnDraw(SKCanvas canvas)
        {
            Draw(canvas, IgnoreAttributes, null);
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

        protected bool CanDraw(SvgVisualElement svgVisualElement, Attributes ignoreAttributes)
        {
            bool visible = svgVisualElement.Visible == true;
            bool display = ignoreAttributes.HasFlag(Attributes.Display) ? true : !string.Equals(svgVisualElement.Display, "none", StringComparison.OrdinalIgnoreCase);
            return visible && display;
        }

        public abstract void OnDraw(SKCanvas canvas, Attributes ignoreAttributes, Drawable? until);

        public virtual void Draw(SKCanvas canvas, Attributes ignoreAttributes, Drawable? until)
        {
            if (!IsDrawable)
            {
                return;
            }

            if (until != null && this == until)
            {
                return;
            }

            var enableClip = !ignoreAttributes.HasFlag(Attributes.ClipPath);
            var enableMask = !ignoreAttributes.HasFlag(Attributes.Mask);
            var enableOpacity = !ignoreAttributes.HasFlag(Attributes.Opacity);
            var enableFilter = !ignoreAttributes.HasFlag(Attributes.Filter);

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

            OnDraw(canvas, ignoreAttributes, until);

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
                MaskDrawable.Draw(canvas, ignoreAttributes, until);
                canvas.Restore();
                canvas.Restore();
            }

            canvas.Restore();
        }

        public virtual void PostProcess()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            var visualElement = element as SvgVisualElement;

            var enableClip = !IgnoreAttributes.HasFlag(Attributes.ClipPath);
            var enableMask = !IgnoreAttributes.HasFlag(Attributes.Mask);
            var enableOpacity = !IgnoreAttributes.HasFlag(Attributes.Opacity);
            var enableFilter = !IgnoreAttributes.HasFlag(Attributes.Filter);

            if (visualElement != null && enableClip == true)
            {
                ClipPath =  SvgClippingExtensions.GetSvgVisualElementClipPath(visualElement, TransformedBounds, new HashSet<Uri>(), _disposable);
            }
            else
            {
                ClipPath = null;
            }

            if (enableMask == true)
            {
                MaskDrawable = SvgClippingExtensions.GetSvgElementMask(element, TransformedBounds, new HashSet<Uri>(), _disposable);
                if (MaskDrawable != null)
                {
                    CreateMaskPaints();
                }
            }
            else
            {
                MaskDrawable = null;
            }

            if (enableOpacity == true)
            {
                Opacity = SvgPaintingExtensions.GetOpacitySKPaint(element, _disposable);
            }
            else
            {
                Opacity = null;
            }

            if (visualElement != null && enableFilter == true)
            {
                Filter = SvgFiltersExtensions.GetFilterSKPaint(visualElement, TransformedBounds, this, _disposable);
            }
            else
            {
                Filter = null;
            }
        }

        public SKPicture? Record(Drawable? drawable, Drawable? unitl, Attributes ignoreAttributes)
        {
            if (drawable == null)
            {
                return null;
            }
            using var skPictureRecorder = new SKPictureRecorder();
            using var skCanvas = skPictureRecorder.BeginRecording(drawable.TransformedBounds);
            drawable.Draw(skCanvas, ignoreAttributes, unitl);
            return skPictureRecorder.EndRecording();
        }

        public const Attributes FilterInput = Attributes.ClipPath | Attributes.Mask | Attributes.Opacity | Attributes.Filter;

        SKPicture? IFilterSource.SourceGraphic() => Record(this, null, FilterInput);

        SKPicture? IFilterSource.BackgroundImage() => Record(Root, this, FilterInput);

        SKPaint? IFilterSource.FillPaint() => Fill;

        SKPaint? IFilterSource.StrokePaint() => Stroke;
    }
}
