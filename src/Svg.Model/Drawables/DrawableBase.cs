using System;
using System.Collections.Generic;
using System.Globalization;
using Svg.Model.Drawables.Elements;
using Svg.Model.Painting;
using Svg.Model.Primitives;

namespace Svg.Model.Drawables
{
    public abstract class DrawableBase : Drawable, IFilterSource, IPictureSource
    {
        public IAssetLoader AssetLoader { get; }
        public SvgElement? Element { get; set; }
        public DrawableBase? Parent { get; set; }
        public bool IsDrawable { get; set; }
        public Attributes IgnoreAttributes { get; set; }
        public bool IsAntialias { get; set; }
        public Rect GeometryBounds { get; set; }
        public Rect TransformedBounds { get; set; }
        public Matrix Transform { get; set; }
        public Rect? Overflow { get; set; }
        public Rect? Clip { get; set; }
        public ClipPath? ClipPath { get; set; }
        public MaskDrawable? MaskDrawable { get; set; }
        public Paint? Mask { get; set; }
        public Paint? MaskDstIn { get; set; }
        public Paint? Opacity { get; set; }
        public Paint? Filter { get; set; }
        public Rect? FilterClip { get; set; }
        public Paint? Fill { get; set; }
        public Paint? Stroke { get; set; }

        protected DrawableBase(IAssetLoader assetLoader)
        {
            AssetLoader = assetLoader;
        }

        protected override void OnDraw(Canvas canvas)
        {
            Draw(canvas, IgnoreAttributes, null, true);
        }

        protected override Rect OnGetBounds()
        {
            return IsDrawable ? TransformedBounds : Rect.Empty;
        }

        protected void CreateMaskPaints()
        {
            Mask = new Paint
            {
                IsAntialias = true,
                Style = PaintStyle.StrokeAndFill
            };

            var lumaColor = ColorFilter.CreateLumaColor();

            MaskDstIn = new Paint
            {
                IsAntialias = true,
                Style = PaintStyle.StrokeAndFill,
                BlendMode = BlendMode.DstIn,
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

        public abstract void OnDraw(Canvas canvas, Attributes ignoreAttributes, DrawableBase? until);

        public virtual void Draw(Canvas canvas, Attributes ignoreAttributes, DrawableBase? until, bool enableTransform)
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
                canvas.ClipRect(Overflow.Value, ClipOperation.Intersect);
            }

            if (!Transform.IsIdentity && enableTransform)
            {
                var skMatrixTotal = canvas.TotalMatrix;
                skMatrixTotal = skMatrixTotal.PreConcat(Transform);
                canvas.SetMatrix(skMatrixTotal);
            }

            if (Clip is { })
            {
                canvas.ClipRect(Clip.Value, ClipOperation.Intersect);
            }

            if (ClipPath is { } && enableClip)
            {
                canvas.ClipPath(ClipPath, ClipOperation.Intersect, IsAntialias);
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
                    canvas.ClipRect(FilterClip.Value, ClipOperation.Intersect);
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

        public virtual void PostProcess(Rect? viewport)
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

        public DrawableBase? FindContainerParentBackground(DrawableBase? drawable, out Rect skClipRect)
        {
            skClipRect = Rect.Empty;

            if (drawable is null)
            {
                return null;
            }

            var element = drawable.Element;
            if (element is null)
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
            if (parent is { })
            {
                return FindContainerParentBackground(parent, out skClipRect);
            }

            return null;
        }

        public Picture? RecordGraphic(DrawableBase? drawable, Attributes ignoreAttributes)
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

            var cullRect = Rect.Create(
                0, 
                0, 
                skBounds.Width + skBounds.Left, 
                skBounds.Height + skBounds.Top);
            var skPictureRecorder = new PictureRecorder();
            var skCanvas = skPictureRecorder.BeginRecording(cullRect);

            drawable.Draw(skCanvas, ignoreAttributes, null, false);

            return skPictureRecorder.EndRecording();
        }

        public Picture? RecordBackground(DrawableBase? drawable, Attributes ignoreAttributes)
        {
            // TODO: Record using ColorSpace.CreateSrgbLinear because 'color-interpolation-filters' is by default linearRGB.
            if (drawable is null)
            {
                return null;
            }

            var container = FindContainerParentBackground(drawable, out var skClipRect);
            if (container is { })
            {
                var skBounds = drawable.GeometryBounds;
                var cullRect = Rect.Create(
                    0, 
                    0, 
                    skBounds.Width + skBounds.Left, 
                    skBounds.Height + skBounds.Top);
                var skPictureRecorder = new PictureRecorder();
                var skCanvas = skPictureRecorder.BeginRecording(cullRect);

                if (!skClipRect.IsEmpty)
                {
                    skCanvas.ClipRect(skClipRect, ClipOperation.Intersect);
                }

                container.Draw(skCanvas, ignoreAttributes, drawable, false);

                return skPictureRecorder.EndRecording();
            }
            return null;
        }

        private const Attributes FilterInput = Attributes.ClipPath | Attributes.Mask | Attributes.Opacity | Attributes.Filter;

        Picture? IFilterSource.SourceGraphic() => RecordGraphic(this, FilterInput);

        Picture? IFilterSource.BackgroundImage() => RecordBackground(this, FilterInput);

        Paint? IFilterSource.FillPaint() => Fill;

        Paint? IFilterSource.StrokePaint() => Stroke;
    }
}
