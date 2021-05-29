using System;
using System.Collections.Generic;
using System.Globalization;
using Svg.Model.Drawables.Elements;
using Svg.Model.Painting;
using Svg.Model.Primitives;

namespace Svg.Model.Drawables
{
    public abstract class DrawableBase : SKDrawable, IFilterSource, IPictureSource
    {
        public IAssetLoader AssetLoader { get; }
        public SvgElement? Element { get; set; }
        public DrawableBase? Parent { get; set; }
        public bool IsDrawable { get; set; }
        public Attributes IgnoreAttributes { get; set; }
        public bool IsAntialias { get; set; }
        public SKRect GeometryBounds { get; set; }
        public SKRect TransformedBounds { get; set; }
        public SKMatrix Transform { get; set; }
        public SKRect? Overflow { get; set; }
        public SKRect? Clip { get; set; }
        public ClipPath? ClipPath { get; set; }
        public MaskDrawable? MaskDrawable { get; set; }
        public SKPaint? Mask { get; set; }
        public SKPaint? MaskDstIn { get; set; }
        public SKPaint? Opacity { get; set; }
        public SKPaint? Filter { get; set; }
        public SKRect? FilterClip { get; set; }
        public SKPaint? Fill { get; set; }
        public SKPaint? Stroke { get; set; }

        protected DrawableBase(IAssetLoader assetLoader)
        {
            AssetLoader = assetLoader;
        }

        protected override void OnDraw(SKCanvas canvas)
        {
            Draw(canvas, IgnoreAttributes, null, true);
        }

        protected override SKRect OnGetBounds()
        {
            return IsDrawable ? TransformedBounds : SKRect.Empty;
        }

        protected void CreateMaskPaints()
        {
            Mask = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.StrokeAndFill
            };

            var lumaColor = SKColorFilter.CreateLumaColor();

            MaskDstIn = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.StrokeAndFill,
                BlendMode = SKBlendMode.DstIn,
                Color = SvgModelExtensions.s_transparentBlack,
                ColorFilter = lumaColor
            };
        }

        protected bool HasFeatures(SvgElement svgElement, Attributes ignoreAttributes)
        {
            var hasRequiredFeatures = ignoreAttributes.HasFlag(Attributes.RequiredFeatures) || svgElement.HasRequiredFeatures();
            var hasRequiredExtensions = ignoreAttributes.HasFlag(Attributes.RequiredExtensions) || svgElement.HasRequiredExtensions();
            var hasSystemLanguage = ignoreAttributes.HasFlag(Attributes.SystemLanguage) || svgElement.HasSystemLanguage();
            return hasRequiredFeatures && hasRequiredExtensions && hasSystemLanguage;
        }

        protected bool CanDraw(SvgVisualElement svgVisualElement, Attributes ignoreAttributes)
        {
            var isVisible = ignoreAttributes.HasFlag(Attributes.Visibility) || string.Equals(svgVisualElement.Visibility, "visible", StringComparison.OrdinalIgnoreCase);
            var isDisplay = ignoreAttributes.HasFlag(Attributes.Display) || !string.Equals(svgVisualElement.Display, "none", StringComparison.OrdinalIgnoreCase);
            return isVisible && isDisplay;
        }

        public abstract void OnDraw(SKCanvas canvas, Attributes ignoreAttributes, DrawableBase? until);

        public virtual void Draw(SKCanvas canvas, Attributes ignoreAttributes, DrawableBase? until, bool enableTransform)
        {
            if (!IsDrawable)
            {
                return;
            }

            if (until is { } && this == until)
            {
                return;
            }

            var enableClip = !ignoreAttributes.HasFlag(Attributes.ClipPath);
            var enableMask = !ignoreAttributes.HasFlag(Attributes.Mask);
            var enableOpacity = !ignoreAttributes.HasFlag(Attributes.Opacity);
            var enableFilter = !ignoreAttributes.HasFlag(Attributes.Filter);

            canvas.Save();

            if (Overflow is { })
            {
                canvas.ClipRect(Overflow.Value, SKClipOperation.Intersect);
            }

            if (!Transform.IsIdentity && enableTransform)
            {
                var skMatrixTotal = canvas.TotalMatrix;
                skMatrixTotal = skMatrixTotal.PreConcat(Transform);
                canvas.SetMatrix(skMatrixTotal);
            }

            if (Clip is { })
            {
                canvas.ClipRect(Clip.Value, SKClipOperation.Intersect);
            }

            if (ClipPath is { } && enableClip)
            {
                canvas.ClipPath(ClipPath, SKClipOperation.Intersect, IsAntialias);
            }

            if (MaskDrawable is { } && Mask is { } && enableMask)
            {
                canvas.SaveLayer(Mask);
            }

            if (Opacity is { } && enableOpacity)
            {
                canvas.SaveLayer(Opacity);
            }

            if (Filter is { } && enableFilter)
            {
                if (FilterClip is not null)
                {
                    canvas.ClipRect(FilterClip.Value, SKClipOperation.Intersect);
                }

                canvas.SaveLayer(Filter);
            }
            else
            {
                OnDraw(canvas, ignoreAttributes, until);
            }

            if (Filter is { } && enableFilter)
            {
                canvas.Restore();
            }

            if (Opacity is { } && enableOpacity)
            {
                canvas.Restore();
            }

            if (MaskDrawable is { } && MaskDstIn is { } && enableMask)
            {
                canvas.SaveLayer(MaskDstIn);
                MaskDrawable.Draw(canvas, ignoreAttributes, until, true);
                canvas.Restore();
                canvas.Restore();
            }

            canvas.Restore();
        }

        public virtual void PostProcess(SKRect? viewport)
        {
            var element = Element;
            if (element is null)
            {
                return;
            }

            var visualElement = element as SvgVisualElement;

            var enableClip = !IgnoreAttributes.HasFlag(Attributes.ClipPath);
            var enableMask = !IgnoreAttributes.HasFlag(Attributes.Mask);
            var enableOpacity = !IgnoreAttributes.HasFlag(Attributes.Opacity);
            var enableFilter = !IgnoreAttributes.HasFlag(Attributes.Filter);

            if (visualElement is { } && enableClip)
            {
                var clipPath = new ClipPath
                {
                    Clip = new ClipPath()
                };
                SvgModelExtensions.GetSvgVisualElementClipPath(visualElement, GeometryBounds, new HashSet<Uri>(), clipPath);
                if (clipPath.Clips is { } && clipPath.Clips.Count > 0)
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
                MaskDrawable = SvgModelExtensions.GetSvgElementMask(element, GeometryBounds, new HashSet<Uri>(), AssetLoader);
                if (MaskDrawable is { })
                {
                    CreateMaskPaints();
                }
            }
            else
            {
                MaskDrawable = null;
            }

            Opacity = enableOpacity ? SvgModelExtensions.GetOpacityPaint(element) : null;

            if (visualElement is { } && enableFilter)
            {
                Filter = SvgModelExtensions.GetFilterPaint(visualElement, GeometryBounds, viewport ?? GeometryBounds, this, AssetLoader, out var isValid, out var filterClip);
                FilterClip = filterClip;
                if (isValid == false)
                {
                    IsDrawable = false;
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

            if (drawable is null)
            {
                return null;
            }

            var element = drawable.Element;
            if (element is null)
            {
                return null;
            }

            if (element.IsContainerElement() && element.TryGetAttribute("enable-background", out string enableBackground))
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

                        skClipRect = SKRect.Create(values[0], values[1], values[2], values[3]);
                    }
                    return drawable;
                }
            }

            var parent = drawable.Parent;
            if (parent is { })
            {
                return FindContainerParentBackground(parent, out skClipRect);
            }

            return null;
        }

        public SKPicture? RecordGraphic(DrawableBase? drawable, Attributes ignoreAttributes)
        {
            // TODO: Record using ColorSpace.CreateSrgbLinear because .color-interpolation-filters. is by default linearRGB.
            if (drawable is null)
            {
                return null;
            }

            var skBounds = drawable.GeometryBounds;
            if (skBounds.Width <= 0f && skBounds.Height <= 0f)
            {
                return null;
            }

            var cullRect = SKRect.Create(
                0, 
                0, 
                Math.Abs(skBounds.Left) + skBounds.Width, 
                Math.Abs(skBounds.Top) + skBounds.Height);
            var skPictureRecorder = new SKPictureRecorder();
            var skCanvas = skPictureRecorder.BeginRecording(cullRect);

            drawable.Draw(skCanvas, ignoreAttributes, null, false);

            return skPictureRecorder.EndRecording();
        }

        public SKPicture? RecordBackground(DrawableBase? drawable, Attributes ignoreAttributes)
        {
            // TODO: Record using ColorSpace.CreateSrgbLinear because 'color-interpolation-filters' is by default linearRGB.
            if (drawable is null)
            {
                return null;
            }

            var container = FindContainerParentBackground(drawable, out var skClipRect);
            if (container is null)
            {
                return null;
            }

            var skBounds = drawable.GeometryBounds;
            var cullRect = SKRect.Create(
                0, 
                0, 
                Math.Abs(skBounds.Left) + skBounds.Width, 
                Math.Abs(skBounds.Top) + skBounds.Height);
            var skPictureRecorder = new SKPictureRecorder();
            var skCanvas = skPictureRecorder.BeginRecording(cullRect);

            if (!skClipRect.IsEmpty)
            {
                skCanvas.ClipRect(skClipRect, SKClipOperation.Intersect);
            }

            container.Draw(skCanvas, ignoreAttributes, drawable, false);

            return skPictureRecorder.EndRecording();
        }

        private const Attributes FilterInput = Attributes.ClipPath | Attributes.Mask | Attributes.Opacity | Attributes.Filter;

        SKPicture? IFilterSource.SourceGraphic() => RecordGraphic(this, FilterInput);

        SKPicture? IFilterSource.BackgroundImage() => RecordBackground(this, FilterInput);

        SKPaint? IFilterSource.FillPaint() => Fill;

        SKPaint? IFilterSource.StrokePaint() => Stroke;
    }
}
