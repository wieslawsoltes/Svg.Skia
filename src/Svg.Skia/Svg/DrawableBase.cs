﻿using System;
using System.Collections.Generic;
using System.Globalization;
#if !USE_PICTURE
using SkiaSharp;
using CropRect = SkiaSharp.SKImageFilter.CropRect;
#endif
#if USE_PICTURE
using SKBlendMode = Svg.Picture.BlendMode;
using SKCanvas = Svg.Picture.Canvas;
using SKClipOperation = Svg.Picture.ClipOperation;
using SKColorFilter = Svg.Picture.ColorFilter;
using SKDrawable = Svg.Picture.Drawable;
using SKMatrix = Svg.Picture.Matrix;
using SKPaint = Svg.Picture.Paint;
using SKPaintStyle = Svg.Picture.PaintStyle;
using SKPicture = Svg.Picture.Picture;
using SKPictureRecorder = Svg.Picture.PictureRecorder;
using SKRect = Svg.Picture.Rect;
#endif

namespace Svg.Skia
{
    internal abstract class DrawableBase : SKDrawable, IFilterSource, IPictureSource
    {
        public readonly CompositeDisposable Disposable;
        public SvgElement? Element;
        public DrawableBase? Parent;
        public bool IsDrawable;
        public Attributes IgnoreAttributes;
        public bool IsAntialias;
        public SKRect TransformedBounds;
        public SKMatrix Transform;
        public SKRect? Overflow;
        public SKRect? Clip;
#if USE_PICTURE
        public Svg.Picture.ClipPath? ClipPath;
#else
        public SKPath? ClipPath;
#endif
        public MaskDrawable? MaskDrawable;
        public SKPaint? Mask;
        public SKPaint? MaskDstIn;
        public SKPaint? Opacity;
        public SKPaint? Filter;
        public SKPaint? Fill;
        public SKPaint? Stroke;

        protected DrawableBase()
        {
            Disposable = new CompositeDisposable();
        }

        protected override void OnDraw(SKCanvas canvas)
        {
            Draw(canvas, IgnoreAttributes, null);
        }

        protected override SKRect OnGetBounds()
        {
            return IsDrawable ? TransformedBounds : SKRect.Empty;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Disposable?.Dispose();
        }

        protected virtual void CreateMaskPaints()
        {
            Mask = new SKPaint()
            {
                IsAntialias = true,
                Style = SKPaintStyle.StrokeAndFill
            };
            Disposable.Add(Mask);

            var lumaColor = SKColorFilter.CreateLumaColor();
            Disposable.Add(lumaColor);

            MaskDstIn = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.StrokeAndFill,
                BlendMode = SKBlendMode.DstIn,
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

        public abstract void OnDraw(SKCanvas canvas, Attributes ignoreAttributes, DrawableBase? until);

        public virtual void Draw(SKCanvas canvas, Attributes ignoreAttributes, DrawableBase? until)
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
                canvas.ClipRect(Overflow.Value, SKClipOperation.Intersect);
            }

            var skMatrixTotal = canvas.TotalMatrix;
            skMatrixTotal = skMatrixTotal.PreConcat(Transform);
            canvas.SetMatrix(skMatrixTotal);

            if (Clip != null)
            {
                canvas.ClipRect(Clip.Value, SKClipOperation.Intersect);
            }

            if (ClipPath != null && enableClip)
            {
                canvas.ClipPath(ClipPath, SKClipOperation.Intersect, IsAntialias);
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
#if USE_PICTURE
                var clipPath = new Svg.Picture.ClipPath()
                {
                    Clip = new Svg.Picture.ClipPath()
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
#else
                var clipPath = SvgExtensions.GetSvgVisualElementClipPath(visualElement, TransformedBounds, new HashSet<Uri>(), Disposable);
                if (clipPath != null)
                {
                    ClipPath = clipPath;
                }
                else
                {
                    ClipPath = null;
                }
#endif
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

            Opacity = enableOpacity ? SvgExtensions.GetOpacitySKPaint(element, Disposable) : null;

            if (visualElement != null && enableFilter)
            {
                Filter = SvgExtensions.GetFilterSKPaint(visualElement, TransformedBounds, this, Disposable, out var isValid);
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

        public DrawableBase? FindContainerParentBackground(DrawableBase? drawable, out SKRect skClipRect)
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

            if (element.IsContainerElement())
            {
                if (element.TryGetAttribute("enable-background", out string enableBackground))
                {
                    enableBackground = enableBackground.Trim();

                    if (enableBackground.Equals("accumulate", StringComparison.Ordinal))
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

        public SKPicture? RecordGraphic(DrawableBase? drawable, Attributes ignoreAttributes)
        {
            // TODO: Record using SKColorSpace.CreateSrgbLinear because .color-interpolation-filters. is by default linearRGB.
            if (drawable == null)
            {
                return null;
            }

            if (drawable.TransformedBounds.Width <= 0f && drawable.TransformedBounds.Height <= 0f)
            {
                return null;
            }

            using var skPictureRecorder = new SKPictureRecorder();
            using var skCanvas = skPictureRecorder.BeginRecording(drawable.TransformedBounds);

#if USE_EXPERIMENTAL_LINEAR_RGB
            // TODO:
            using var skPaint = new SKPaint();
            using var skColorFilter = SKColorFilter.CreateTable(null, SvgExtensions.s_SRGBtoLinearRGB, SvgExtensions.s_SRGBtoLinearRGB, SvgExtensions.s_SRGBtoLinearRGB);
            using var skImageFilter = SKImageFilter.CreateColorFilter(skColorFilter);
            skPaint.ImageFilter = skImageFilter;
            skCanvas.SaveLayer(skPaint);
#endif

            drawable.Draw(skCanvas, ignoreAttributes, null);

#if USE_EXPERIMENTAL_LINEAR_RGB
            // TODO:
            skCanvas.Restore();
#endif

            return skPictureRecorder.EndRecording();
        }

        public SKPicture? RecordBackground(DrawableBase? drawable, Attributes ignoreAttributes)
        {
            // TODO: Record using SKColorSpace.CreateSrgbLinear because 'color-interpolation-filters' is by default linearRGB.
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

#if USE_EXPERIMENTAL_LINEAR_RGB
                // TODO:
                using var skPaint = new SKPaint();
                using var skColorFilter = SKColorFilter.CreateTable(null, SvgExtensions.s_SRGBtoLinearRGB, SvgExtensions.s_SRGBtoLinearRGB, SvgExtensions.s_SRGBtoLinearRGB);
                using var skImageFilter = SKImageFilter.CreateColorFilter(skColorFilter);
                skPaint.ImageFilter = skImageFilter;
                skCanvas.SaveLayer(skPaint);
#endif

                container.Draw(skCanvas, ignoreAttributes, drawable);

#if USE_EXPERIMENTAL_LINEAR_RGB
                // TODO:
                skCanvas.Restore();
#endif

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
