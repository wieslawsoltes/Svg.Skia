using System;
using System.Collections.Generic;
using System.Globalization;
using Svg.Model.Drawables;

namespace Svg.Model
{
    public abstract class DrawableBase : Drawable, IFilterSource, IPictureSource
    {
        internal readonly CompositeDisposable Disposable;
        public SvgElement? Element;
        public DrawableBase? Parent;
        public bool IsDrawable;
        public Attributes IgnoreAttributes;
        public bool IsAntialias;
        public Rect TransformedBounds;
        public Matrix Transform;
        public Rect? Overflow;
        public Rect? Clip;
        public Svg.Model.ClipPath? ClipPath;
        public MaskDrawable? MaskDrawable;
        public Paint? Mask;
        public Paint? MaskDstIn;
        public Paint? Opacity;
        public Paint? Filter;
        public Paint? Fill;
        public Paint? Stroke;

        protected DrawableBase()
        {
            Disposable = new CompositeDisposable();
        }

        protected override void OnDraw(Canvas canvas)
        {
            Draw(canvas, IgnoreAttributes, null);
        }

        protected override Rect OnGetBounds()
        {
            return IsDrawable ? TransformedBounds : Rect.Empty;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Disposable?.Dispose();
        }

        protected virtual void CreateMaskPaints()
        {
            Mask = new Paint()
            {
                IsAntialias = true,
                Style = PaintStyle.StrokeAndFill
            };
            Disposable.Add(Mask);

            var lumaColor = ColorFilter.CreateLumaColor();
            Disposable.Add(lumaColor);

            MaskDstIn = new Paint
            {
                IsAntialias = true,
                Style = PaintStyle.StrokeAndFill,
                BlendMode = BlendMode.DstIn,
                Color = SvgExtensions.s_transparentBlack,
                ColorFilter = lumaColor
            };
            Disposable.Add(MaskDstIn);
        }

        protected virtual bool HasFeatures(SvgElement svgElement, Attributes ignoreAttributes)
        {
            bool hasRequiredFeatures = ignoreAttributes.HasFlag(Attributes.RequiredFeatures) || svgElement.HasRequiredFeatures();
            bool hasRequiredExtensions = ignoreAttributes.HasFlag(Attributes.RequiredExtensions) || svgElement.HasRequiredExtensions();
            bool hasSystemLanguage = ignoreAttributes.HasFlag(Attributes.SystemLanguage) || svgElement.HasSystemLanguage();
            return hasRequiredFeatures && hasRequiredExtensions && hasSystemLanguage;
        }

        protected virtual bool CanDraw(SvgVisualElement svgVisualElement, Attributes ignoreAttributes)
        {
            bool isVisible = ignoreAttributes.HasFlag(Attributes.Visibility) || string.Equals(svgVisualElement.Visibility, "visible", StringComparison.OrdinalIgnoreCase);
            bool isDisplay = ignoreAttributes.HasFlag(Attributes.Display) || !string.Equals(svgVisualElement.Display, "none", StringComparison.OrdinalIgnoreCase);
            return isVisible && isDisplay;
        }

        public abstract void OnDraw(Canvas canvas, Attributes ignoreAttributes, DrawableBase? until);

        public virtual void Draw(Canvas canvas, Attributes ignoreAttributes, DrawableBase? until)
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

            if (Overflow != null)
            {
                canvas.ClipRect(Overflow.Value, ClipOperation.Intersect);
            }

            var skMatrixTotal = canvas.TotalMatrix;
            skMatrixTotal = skMatrixTotal.PreConcat(Transform);
            canvas.SetMatrix(skMatrixTotal);

            if (Clip != null)
            {
                canvas.ClipRect(Clip.Value, ClipOperation.Intersect);
            }

            if (ClipPath != null && enableClip)
            {
                canvas.ClipPath(ClipPath, ClipOperation.Intersect, IsAntialias);
            }

            if (MaskDrawable != null && Mask != null && enableMask)
            {
                canvas.SaveLayer(Mask);
            }

            if (Opacity != null && enableOpacity)
            {
                canvas.SaveLayer(Opacity);
            }

            if (Filter != null && enableFilter)
            {
                canvas.SaveLayer(Filter);
            }

            OnDraw(canvas, ignoreAttributes, until);

            if (Filter != null && enableFilter)
            {
                canvas.Restore();
            }

            if (Opacity != null && enableOpacity)
            {
                canvas.Restore();
            }

            if (MaskDrawable != null && MaskDstIn != null && enableMask)
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

            if (visualElement != null && enableClip)
            {
                var clipPath = new Svg.Model.ClipPath()
                {
                    Clip = new Svg.Model.ClipPath()
                };
                SvgExtensions.GetSvgVisualElementClipPath(visualElement, TransformedBounds, new HashSet<Uri>(), Disposable, clipPath);
                if (clipPath.Clips != null && clipPath.Clips.Count > 0)
                {
                    ClipPath = clipPath;
                }
                else
                {
                    ClipPath = null;
                }
            }
            else
            {
                ClipPath = null;
            }

            if (enableMask)
            {
                MaskDrawable = SvgExtensions.GetSvgElementMask(element, TransformedBounds, new HashSet<Uri>(), Disposable);
                if (MaskDrawable != null)
                {
                    CreateMaskPaints();
                }
            }
            else
            {
                MaskDrawable = null;
            }

            Opacity = enableOpacity ? SvgExtensions.GetOpacityPaint(element, Disposable) : null;

            if (visualElement != null && enableFilter)
            {
                Filter = SvgExtensions.GetFilterPaint(visualElement, TransformedBounds, this, Disposable, out var isValid);
                if (isValid == false)
                {
                    IsDrawable = false;
                    return;
                }
            }
            else
            {
                Filter = null;
            }
        }

        public DrawableBase? FindContainerParentBackground(DrawableBase? drawable, out Rect skClipRect)
        {
            skClipRect = Rect.Empty;

            if (drawable == null)
            {
                return null;
            }

            var element = drawable.Element;
            if (element == null)
            {
                return null;
            }

            if (element.IsContainerElement())
            {
                if (element.TryGetAttribute("enable-background", out string enableBackground))
                {
                    enableBackground = enableBackground.Trim();

                    if (enableBackground.Equals("accumulate", StringComparison.Ordinal))
                    {
                        // TODO:
                    }
                    else if (enableBackground.StartsWith("new", StringComparison.Ordinal))
                    {
                        if (enableBackground.Length > 3)
                        {
                            var values = new List<float>();
                            var parts = enableBackground.Substring(4, enableBackground.Length - 4).Split(' ');
                            foreach (var o in parts)
                            {
                                values.Add(float.Parse(o.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture));
                            }

                            if (values.Count != 4)
                            {
                                return null;
                            }

                            skClipRect = Rect.Create(values[0], values[1], values[2], values[3]);
                        }
                        return drawable;
                    }
                }
            }

            var parent = drawable.Parent;
            if (parent != null)
            {
                return FindContainerParentBackground(parent, out skClipRect);
            }

            return null;
        }

        public Picture? RecordGraphic(DrawableBase? drawable, Attributes ignoreAttributes)
        {
            // TODO: Record using ColorSpace.CreateSrgbLinear because .color-interpolation-filters. is by default linearRGB.
            if (drawable == null)
            {
                return null;
            }

            if (drawable.TransformedBounds.Width <= 0f && drawable.TransformedBounds.Height <= 0f)
            {
                return null;
            }

            using var skPictureRecorder = new PictureRecorder();
            using var skCanvas = skPictureRecorder.BeginRecording(drawable.TransformedBounds);

            drawable.Draw(skCanvas, ignoreAttributes, null);

            return skPictureRecorder.EndRecording();
        }

        public Picture? RecordBackground(DrawableBase? drawable, Attributes ignoreAttributes)
        {
            // TODO: Record using ColorSpace.CreateSrgbLinear because 'color-interpolation-filters' is by default linearRGB.
            if (drawable == null)
            {
                return null;
            }

            var container = FindContainerParentBackground(drawable, out var skClipRect);
            if (container != null)
            {
                using var skPictureRecorder = new PictureRecorder();
                using var skCanvas = skPictureRecorder.BeginRecording(container.TransformedBounds);

                if (!skClipRect.IsEmpty)
                {
                    skCanvas.ClipRect(skClipRect, ClipOperation.Intersect);
                }

                container.Draw(skCanvas, ignoreAttributes, drawable);

                return skPictureRecorder.EndRecording();
            }
            return null;
        }

        public const Attributes FilterInput = Attributes.ClipPath | Attributes.Mask | Attributes.Opacity | Attributes.Filter;

        Picture? IFilterSource.SourceGraphic() => RecordGraphic(this, FilterInput);

        Picture? IFilterSource.BackgroundImage() => RecordBackground(this, FilterInput);

        Paint? IFilterSource.FillPaint() => Fill;

        Paint? IFilterSource.StrokePaint() => Stroke;
    }
}
