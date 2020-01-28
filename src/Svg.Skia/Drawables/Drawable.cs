// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;
using Svg.Document_Structure;

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

            var lumaColor = SKColorFilter.CreateLumaColor();
            _disposable.Add(lumaColor);

            MaskDstIn = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.StrokeAndFill,
                BlendMode = SKBlendMode.DstIn,
                Color = SvgPaintingExtensions.TransparentBlack,
                ColorFilter = lumaColor
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

        public bool IsContainerElement(SvgElement svgElement)
        {
            switch (svgElement)
            {
                case SvgAnchor _:
                case SvgDefinitionList _:
                case SvgMissingGlyph _:
                case SvgGlyph _:
                case SvgGroup _:
                case SvgMarker _:
                case SvgMask _:
                case SvgPatternServer _:
                case SvgFragment _:
                case SvgSwitch _:
                case SvgSymbol _:
                    return true;
                default:
                    return false;
            }
        }

        public Drawable? FindContainerParentBackground(Drawable? drawable, out SKRect skClipRect)
        {
            skClipRect = SKRect.Empty;

            if (drawable == null)
            {
                return null;
            }

            var element = drawable.Element;
            if (element == null)
            {
                return null;
            }

            if (IsContainerElement(element))
            {
                if (element.TryGetAttribute("enable-background", out string enableBackground))
                {
                    enableBackground = enableBackground.Trim();

                    if (enableBackground == "accumulate")
                    {
                        // TODO:
                    }
                    else if (enableBackground.StartsWith("new"))
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

                            skClipRect = SKRect.Create(values[0], values[1], values[2], values[3]);
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

        public SKPicture? RecordGraphic(Drawable? drawable, Attributes ignoreAttributes)
        {
            if (drawable == null)
            {
                return null;
            }
            using var skPictureRecorder = new SKPictureRecorder();
            using var skCanvas = skPictureRecorder.BeginRecording(drawable.TransformedBounds);
            drawable.Draw(skCanvas, ignoreAttributes, null);
            return skPictureRecorder.EndRecording();
        }

        public SKPicture? RecordBackground(Drawable? drawable, Attributes ignoreAttributes)
        {
            if (drawable == null)
            {
                return null;
            }
            var container = FindContainerParentBackground(drawable, out var skClipRect);
            if (container != null)
            {
                using var skPictureRecorder = new SKPictureRecorder();
                using var skCanvas = skPictureRecorder.BeginRecording(container.TransformedBounds);
                if (!skClipRect.IsEmpty)
                {
                    skCanvas.ClipRect(skClipRect, SKClipOperation.Intersect);
                }
                container.Draw(skCanvas, ignoreAttributes, drawable);
                return skPictureRecorder.EndRecording();
            }
            return null;
        }

        public const Attributes FilterInput = Attributes.ClipPath | Attributes.Mask | Attributes.Opacity | Attributes.Filter;

        SKPicture? IFilterSource.SourceGraphic() => RecordGraphic(this, FilterInput);

        SKPicture? IFilterSource.BackgroundImage() => RecordBackground(this, FilterInput);

        SKPaint? IFilterSource.FillPaint() => Fill;

        SKPaint? IFilterSource.StrokePaint() => Stroke;
    }
}
