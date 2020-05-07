using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;
using Svg.DataTypes;
using Svg.Document_Structure;

namespace Svg.Skia
{
    [Flags]
    public enum Attributes
    {
        None = 0,
        Display = 1,
        Visibility = 2,
        Opacity = 4,
        Filter = 8,
        ClipPath = 16,
        Mask = 32,
        RequiredFeatures = 64,
        RequiredExtensions = 128,
        SystemLanguage = 256
    }

    public interface IFilterSource
    {
        SKPicture? SourceGraphic();
        SKPicture? BackgroundImage();
        SKPaint? FillPaint();
        SKPaint? StrokePaint();
    }

    public interface IPictureSource
    {
        void OnDraw(SKCanvas canvas, Attributes ignoreAttributes, Drawable? until);
        void Draw(SKCanvas canvas, Attributes ignoreAttributes, Drawable? until);
    }

    public abstract class Drawable : SKDrawable, IFilterSource, IPictureSource
    {
        public static CultureInfo? s_systemLanguageOverride = null;

        public static HashSet<string> s_supportedFeatures = new HashSet<string>()
        {
            "http://www.w3.org/TR/SVG11/feature#SVG",
            "http://www.w3.org/TR/SVG11/feature#SVGDOM",
            "http://www.w3.org/TR/SVG11/feature#SVG-static",
            "http://www.w3.org/TR/SVG11/feature#SVGDOM-static",
            "http://www.w3.org/TR/SVG11/feature#SVG-animation",
            "http://www.w3.org/TR/SVG11/feature#SVGDOM-animation",
            "http://www.w3.org/TR/SVG11/feature#SVG-dynamic",
            "http://www.w3.org/TR/SVG11/feature#SVGDOM-dynamic",
            "http://www.w3.org/TR/SVG11/feature#CoreAttribute",
            "http://www.w3.org/TR/SVG11/feature#Structure",
            "http://www.w3.org/TR/SVG11/feature#BasicStructure",
            "http://www.w3.org/TR/SVG11/feature#ContainerAttribute",
            "http://www.w3.org/TR/SVG11/feature#ConditionalProcessing",
            "http://www.w3.org/TR/SVG11/feature#Image",
            "http://www.w3.org/TR/SVG11/feature#Style",
            "http://www.w3.org/TR/SVG11/feature#ViewportAttribute",
            "http://www.w3.org/TR/SVG11/feature#Shape",
            "http://www.w3.org/TR/SVG11/feature#Text",
            "http://www.w3.org/TR/SVG11/feature#BasicText",
            "http://www.w3.org/TR/SVG11/feature#PaintAttribute",
            "http://www.w3.org/TR/SVG11/feature#BasicPaintAttribute",
            "http://www.w3.org/TR/SVG11/feature#OpacityAttribute",
            "http://www.w3.org/TR/SVG11/feature#GraphicsAttribute",
            "http://www.w3.org/TR/SVG11/feature#BasicGraphicsAttribute",
            "http://www.w3.org/TR/SVG11/feature#Marker",
            "http://www.w3.org/TR/SVG11/feature#ColorProfile",
            "http://www.w3.org/TR/SVG11/feature#Gradient",
            "http://www.w3.org/TR/SVG11/feature#Pattern",
            "http://www.w3.org/TR/SVG11/feature#Clip",
            "http://www.w3.org/TR/SVG11/feature#BasicClip",
            "http://www.w3.org/TR/SVG11/feature#Mask",
            "http://www.w3.org/TR/SVG11/feature#Filter",
            "http://www.w3.org/TR/SVG11/feature#BasicFilter",
            "http://www.w3.org/TR/SVG11/feature#DocumentEventsAttribute",
            "http://www.w3.org/TR/SVG11/feature#GraphicalEventsAttribute",
            "http://www.w3.org/TR/SVG11/feature#AnimationEventsAttribute",
            "http://www.w3.org/TR/SVG11/feature#Cursor",
            "http://www.w3.org/TR/SVG11/feature#Hyperlinking",
            "http://www.w3.org/TR/SVG11/feature#XlinkAttribute",
            "http://www.w3.org/TR/SVG11/feature#ExternalResourcesRequired",
            "http://www.w3.org/TR/SVG11/feature#View",
            "http://www.w3.org/TR/SVG11/feature#Script",
            "http://www.w3.org/TR/SVG11/feature#Animation",
            "http://www.w3.org/TR/SVG11/feature#Font",
            "http://www.w3.org/TR/SVG11/feature#BasicFont",
            "http://www.w3.org/TR/SVG11/feature#Extensibility"
        };

        public static HashSet<string> s_supportedExtensions = new HashSet<string>()
        {
        };

        public static bool HasRequiredFeatures(SvgElement svgElement)
        {
            bool hasRequiredFeatures = true;

            if (svgElement.GetAttribute("requiredFeatures", out var requiredFeaturesString) == true)
            {
                if (string.IsNullOrEmpty(requiredFeaturesString))
                {
                    hasRequiredFeatures = false;
                }
                else
                {
                    var features = requiredFeaturesString.Trim().Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (features.Length > 0)
                    {
                        foreach (var feature in features)
                        {
                            if (!s_supportedFeatures.Contains(feature))
                            {
                                hasRequiredFeatures = false;
                                break;
                            }
                        }
                    }
                    else
                    {
                        hasRequiredFeatures = false;
                    }
                }
            }

            return hasRequiredFeatures;
        }

        public static bool HasRequiredExtensions(SvgElement svgElement)
        {
            bool hasRequiredExtensions = true;

            if (svgElement.GetAttribute("requiredExtensions", out var requiredExtensionsString) == true)
            {
                if (string.IsNullOrEmpty(requiredExtensionsString))
                {
                    hasRequiredExtensions = false;
                }
                else
                {
                    var extensions = requiredExtensionsString.Trim().Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (extensions.Length > 0)
                    {
                        foreach (var extension in extensions)
                        {
                            if (!s_supportedExtensions.Contains(extension))
                            {
                                hasRequiredExtensions = false;
                                break;
                            }
                        }
                    }
                    else
                    {
                        hasRequiredExtensions = false;
                    }
                }
            }

            return hasRequiredExtensions;
        }

        public static bool HasSystemLanguage(SvgElement svgElement)
        {
            bool hasSystemLanguage = true;

            if (svgElement.GetAttribute("systemLanguage", out var systemLanguageString) == true)
            {
                if (string.IsNullOrEmpty(systemLanguageString))
                {
                    hasSystemLanguage = false;
                }
                else
                {
                    var languages = systemLanguageString.Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (languages.Length > 0)
                    {
                        hasSystemLanguage = false;
                        var systemLanguage = s_systemLanguageOverride != null ? s_systemLanguageOverride : CultureInfo.InstalledUICulture;

                        foreach (var language in languages)
                        {
                            try
                            {
                                var languageCultureInfo = CultureInfo.CreateSpecificCulture(language.Trim());
                                if (systemLanguage.Equals(languageCultureInfo) || systemLanguage.TwoLetterISOLanguageName == languageCultureInfo.TwoLetterISOLanguageName)
                                {
                                    hasSystemLanguage = true;
                                }
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        hasSystemLanguage = false;
                    }
                }
            }

            return hasSystemLanguage;
        }

        public static bool IsContainerElement(SvgElement svgElement)
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

        public static bool IsKnownElement(SvgElement svgElement)
        {
            switch (svgElement)
            {
                case SvgAnchor _:
                case SvgCircle _:
                case SvgEllipse _:
                case SvgFragment _:
                case SvgGroup _:
                case SvgImage _:
                case SvgLine _:
                case SvgPath _:
                case SvgPolyline _:
                case SvgPolygon _:
                case SvgRectangle _:
                case SvgSwitch _:
                case SvgText _:
                case SvgUse _:
                    return true;
                default:
                    return false;
            }
        }

        internal CompositeDisposable _disposable = new CompositeDisposable();

        public SvgElement? Element;
        public Drawable? Root;
        public Drawable? Parent;
        public bool IsDrawable;
        public Attributes IgnoreAttributes;
        public bool IsAntialias;
        public SKRect TransformedBounds;
        public SKMatrix Transform;
        public SKRect? Overflow;
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

        protected virtual void CreateMaskPaints()
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

        protected virtual bool HasFeatures(SvgElement svgElement, Attributes ignoreAttributes)
        {
            bool hasRequiredFeatures = ignoreAttributes.HasFlag(Attributes.RequiredFeatures) ? true : HasRequiredFeatures(svgElement);
            bool hasRequiredExtensions = ignoreAttributes.HasFlag(Attributes.RequiredExtensions) ? true : HasRequiredExtensions(svgElement);
            bool hasSystemLanguage = ignoreAttributes.HasFlag(Attributes.SystemLanguage) ? true : HasSystemLanguage(svgElement);
            return hasRequiredFeatures && hasRequiredExtensions && hasSystemLanguage;
        }

        protected virtual bool CanDraw(SvgVisualElement svgVisualElement, Attributes ignoreAttributes)
        {
            bool visible = ignoreAttributes.HasFlag(Attributes.Visibility) ? true : string.Equals(svgVisualElement.Visibility, "visible", StringComparison.OrdinalIgnoreCase);
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
                ClipPath = SvgClippingExtensions.GetSvgVisualElementClipPath(visualElement, TransformedBounds, new HashSet<Uri>(), _disposable);
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
                Filter = SvgFiltersExtensions.GetFilterSKPaint(visualElement, TransformedBounds, this, _disposable, out var isValid);
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
            using var skColorFilter = SKColorFilter.CreateTable(null, SvgPaintingExtensions.s_SRGBtoLinearRGB, SvgPaintingExtensions.s_SRGBtoLinearRGB, SvgPaintingExtensions.s_SRGBtoLinearRGB);
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

        public SKPicture? RecordBackground(Drawable? drawable, Attributes ignoreAttributes)
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
                using var skColorFilter = SKColorFilter.CreateTable(null, SvgPaintingExtensions.s_SRGBtoLinearRGB, SvgPaintingExtensions.s_SRGBtoLinearRGB, SvgPaintingExtensions.s_SRGBtoLinearRGB);
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

    public abstract class DrawablePath : Drawable
    {
        public SKPath? Path;
        public List<Drawable>? MarkerDrawables;

        public DrawablePath(SvgElement? element, Drawable? root, Drawable? parent)
            : base(element, root, parent)
        {
        }

        public override void OnDraw(SKCanvas canvas, Attributes ignoreAttributes, Drawable? until)
        {
            if (until != null && this == until)
            {
                return;
            }

            if (Fill != null)
            {
                canvas.DrawPath(Path, Fill);
            }

            if (Stroke != null)
            {
                canvas.DrawPath(Path, Stroke);
            }

            if (MarkerDrawables != null)
            {
                foreach (var drawable in MarkerDrawables)
                {
                    drawable.Draw(canvas, ignoreAttributes, until);
                }
            }
        }

        public override void PostProcess()
        {
            base.PostProcess();

            if (MarkerDrawables != null)
            {
                foreach (var drawable in MarkerDrawables)
                {
                    drawable.PostProcess();
                }
            }
        }

        public override Drawable? HitTest(SKPoint skPoint)
        {
            if (MarkerDrawables != null)
            {
                foreach (var drawable in MarkerDrawables)
                {
                    var result = drawable.HitTest(skPoint);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }
            return base.HitTest(skPoint);
        }
    }

    public abstract class DrawableContainer : Drawable
    {
        public List<Drawable> ChildrenDrawables = new List<Drawable>();

        public DrawableContainer(SvgElement? element, Drawable? root, Drawable? parent)
            : base(element, root, parent)
        {
        }

        protected virtual void CreateChildren(SvgElement svgElement, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes)
        {
            foreach (var child in svgElement.Children)
            {
                var drawable = DrawableFactory.Create(child, skOwnerBounds, root, parent, ignoreAttributes);
                if (drawable != null)
                {
                    ChildrenDrawables.Add(drawable);
                    _disposable.Add(drawable);
                }
            }
        }

        protected virtual void CreateTransformedBounds()
        {
            foreach (var drawable in ChildrenDrawables)
            {
                if (TransformedBounds.IsEmpty)
                {
                    TransformedBounds = drawable.TransformedBounds;
                }
                else
                {
                    if (!drawable.TransformedBounds.IsEmpty)
                    {
                        TransformedBounds = SKRect.Union(TransformedBounds, drawable.TransformedBounds);
                    }
                }
            }
        }

        public override void OnDraw(SKCanvas canvas, Attributes ignoreAttributes, Drawable? until)
        {
            if (until != null && this == until)
            {
                return;
            }

            foreach (var drawable in ChildrenDrawables)
            {
                if (until != null && drawable == until)
                {
                    break;
                }
                drawable.Draw(canvas, ignoreAttributes, until);
            }
        }

        public override void PostProcess()
        {
            base.PostProcess();

            foreach (var child in ChildrenDrawables)
            {
                child.PostProcess();
            }
        }

        public override Drawable? HitTest(SKPoint skPoint)
        {
            foreach (var drawable in ChildrenDrawables)
            {
                var result = drawable.HitTest(skPoint);
                if (result != null)
                {
                    return result;
                }
            }
            return base.HitTest(skPoint);
        }
    }

    public class AnchorDrawable : DrawableContainer
    {
        public AnchorDrawable(SvgAnchor svgAnchor, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(svgAnchor, root, parent)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = true;

            if (!IsDrawable)
            {
                return;
            }

            CreateChildren(svgAnchor, skOwnerBounds, root, this, ignoreAttributes);

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgAnchor);

            TransformedBounds = SKRect.Empty;

            CreateTransformedBounds();

            Transform = SvgTransformsExtensions.ToSKMatrix(svgAnchor.Transforms);

            Fill = null;
            Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            TransformedBounds = Transform.MapRect(TransformedBounds);

            ClipPath = null;
            MaskDrawable = null;
            Opacity = IgnoreAttributes.HasFlag(Attributes.Opacity) ? null : SvgPaintingExtensions.GetOpacitySKPaint(svgAnchor, _disposable);
            Filter = null;
        }

        public override void PostProcess()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            var enableOpacity = !IgnoreAttributes.HasFlag(Attributes.Opacity);

            ClipPath = null;
            MaskDrawable = null;

            if (enableOpacity == true)
            {
                Opacity = SvgPaintingExtensions.GetOpacitySKPaint(element, _disposable);
            }
            else
            {
                Opacity = null;
            }

            Filter = null;

            foreach (var child in ChildrenDrawables)
            {
                child.PostProcess();
            }
        }
    }

    public class FragmentDrawable : DrawableContainer
    {
        public FragmentDrawable(SvgFragment svgFragment, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(svgFragment, root, parent)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = HasFeatures(svgFragment, IgnoreAttributes);

            if (!IsDrawable)
            {
                return;
            }

            var svgFragmentParent = svgFragment.Parent;

            float x = svgFragmentParent == null ? 0f : svgFragment.X.ToDeviceValue(UnitRenderingType.Horizontal, svgFragment, skOwnerBounds);
            float y = svgFragmentParent == null ? 0f : svgFragment.Y.ToDeviceValue(UnitRenderingType.Vertical, svgFragment, skOwnerBounds);

            var skSize = SvgExtensions.GetDimensions(svgFragment);

            if (skOwnerBounds.IsEmpty)
            {
                skOwnerBounds = SKRect.Create(x, y, skSize.Width, skSize.Height);
            }

            CreateChildren(svgFragment, skOwnerBounds, this, this, ignoreAttributes);

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgFragment);

            TransformedBounds = skOwnerBounds;

            CreateTransformedBounds();

            Transform = SvgTransformsExtensions.ToSKMatrix(svgFragment.Transforms);
            var skMatrixViewBox = SvgTransformsExtensions.ToSKMatrix(svgFragment.ViewBox, svgFragment.AspectRatio, x, y, skSize.Width, skSize.Height);
            Transform = Transform.PreConcat(skMatrixViewBox);

            switch (svgFragment.Overflow)
            {
                case SvgOverflow.Auto:
                case SvgOverflow.Visible:
                case SvgOverflow.Inherit:
                    break;
                default:
                    if (skSize.IsEmpty)
                    {
                        Overflow = SKRect.Create(
                            x,
                            y,
                            Math.Abs(TransformedBounds.Left) + TransformedBounds.Width,
                            Math.Abs(TransformedBounds.Top) + TransformedBounds.Height);
                    }
                    else
                    {
                        Overflow = SKRect.Create(x, y, skSize.Width, skSize.Height);
                    }
                    break;
            }

            var clipPathUris = new HashSet<Uri>();
            var svgClipPath = svgFragment.GetUriElementReference<SvgClipPath>("clip-path", clipPathUris);
            if (svgClipPath != null && svgClipPath.Children != null)
            {
                ClipPath = IgnoreAttributes.HasFlag(Attributes.ClipPath) ? null : SvgClippingExtensions.GetClipPath(svgClipPath, TransformedBounds, clipPathUris, _disposable);
            }
            else
            {
                ClipPath = null;
            }

            Fill = null;
            Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            TransformedBounds = Transform.MapRect(TransformedBounds);
        }

        public override void PostProcess()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            var enableOpacity = !IgnoreAttributes.HasFlag(Attributes.Opacity);

            ClipPath = null;
            MaskDrawable = null;

            if (enableOpacity == true)
            {
                Opacity = SvgPaintingExtensions.GetOpacitySKPaint(element, _disposable);
            }
            else
            {
                Opacity = null;
            }

            Filter = null;

            foreach (var child in ChildrenDrawables)
            {
                child.PostProcess();
            }
        }
    }

    public class ImageDrawable : Drawable
    {
        public SKImage? Image;
        public FragmentDrawable? FragmentDrawable;
        public SKRect SrcRect = default;
        public SKRect DestRect = default;
        public SKMatrix FragmentTransform;

        public ImageDrawable(SvgImage svgImage, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(svgImage, root, parent)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgImage, IgnoreAttributes) && HasFeatures(svgImage, IgnoreAttributes);

            if (!IsDrawable)
            {
                return;
            }

            float width = svgImage.Width.ToDeviceValue(UnitRenderingType.Horizontal, svgImage, skOwnerBounds);
            float height = svgImage.Height.ToDeviceValue(UnitRenderingType.Vertical, svgImage, skOwnerBounds);
            float x = svgImage.Location.X.ToDeviceValue(UnitRenderingType.Horizontal, svgImage, skOwnerBounds);
            float y = svgImage.Location.Y.ToDeviceValue(UnitRenderingType.Vertical, svgImage, skOwnerBounds);
            var location = new SKPoint(x, y);

            if (width <= 0f || height <= 0f || svgImage.Href == null)
            {
                IsDrawable = false;
                return;
            }

            // TODO: Check for image recursive references.
            //if (SkiaUtil.HasRecursiveReference(svgImage, (e) => e.Href))
            //{
            //    _canDraw = false;
            //    return;
            //}

            var image = SvgImageExtensions.GetImage(svgImage.Href, svgImage.OwnerDocument);
            var skImage = image as SKImage;
            var svgFragment = image as SvgFragment;
            if (skImage == null && svgFragment == null)
            {
                IsDrawable = false;
                return;
            }

            if (skImage != null)
            {
                _disposable.Add(skImage);
            }

            SrcRect = default;

            if (skImage != null)
            {
                SrcRect = SKRect.Create(0f, 0f, skImage.Width, skImage.Height);
            }

            if (svgFragment != null)
            {
                var skSize = SvgExtensions.GetDimensions(svgFragment);
                SrcRect = SKRect.Create(0f, 0f, skSize.Width, skSize.Height);
            }

            var destClip = SKRect.Create(location.X, location.Y, width, height);

            var aspectRatio = svgImage.AspectRatio;
            if (aspectRatio.Align != SvgPreserveAspectRatio.none)
            {
                var fScaleX = destClip.Width / SrcRect.Width;
                var fScaleY = destClip.Height / SrcRect.Height;
                var xOffset = 0f;
                var yOffset = 0f;

                if (aspectRatio.Slice)
                {
                    fScaleX = Math.Max(fScaleX, fScaleY);
                    fScaleY = Math.Max(fScaleX, fScaleY);
                }
                else
                {
                    fScaleX = Math.Min(fScaleX, fScaleY);
                    fScaleY = Math.Min(fScaleX, fScaleY);
                }

                switch (aspectRatio.Align)
                {
                    case SvgPreserveAspectRatio.xMinYMin:
                        break;
                    case SvgPreserveAspectRatio.xMidYMin:
                        xOffset = (destClip.Width - SrcRect.Width * fScaleX) / 2;
                        break;
                    case SvgPreserveAspectRatio.xMaxYMin:
                        xOffset = (destClip.Width - SrcRect.Width * fScaleX);
                        break;
                    case SvgPreserveAspectRatio.xMinYMid:
                        yOffset = (destClip.Height - SrcRect.Height * fScaleY) / 2;
                        break;
                    case SvgPreserveAspectRatio.xMidYMid:
                        xOffset = (destClip.Width - SrcRect.Width * fScaleX) / 2;
                        yOffset = (destClip.Height - SrcRect.Height * fScaleY) / 2;
                        break;
                    case SvgPreserveAspectRatio.xMaxYMid:
                        xOffset = (destClip.Width - SrcRect.Width * fScaleX);
                        yOffset = (destClip.Height - SrcRect.Height * fScaleY) / 2;
                        break;
                    case SvgPreserveAspectRatio.xMinYMax:
                        yOffset = (destClip.Height - SrcRect.Height * fScaleY);
                        break;
                    case SvgPreserveAspectRatio.xMidYMax:
                        xOffset = (destClip.Width - SrcRect.Width * fScaleX) / 2;
                        yOffset = (destClip.Height - SrcRect.Height * fScaleY);
                        break;
                    case SvgPreserveAspectRatio.xMaxYMax:
                        xOffset = (destClip.Width - SrcRect.Width * fScaleX);
                        yOffset = (destClip.Height - SrcRect.Height * fScaleY);
                        break;
                }

                DestRect = SKRect.Create(
                    destClip.Left + xOffset,
                    destClip.Top + yOffset,
                    SrcRect.Width * fScaleX,
                    SrcRect.Height * fScaleY);
            }
            else
            {
                DestRect = destClip;
            }

            Clip = destClip;

            var skClipRect = SvgClippingExtensions.GetClipRect(svgImage, destClip);
            if (skClipRect != null)
            {
                Clip = skClipRect;
            }

            if (skImage != null)
            {
                Image = skImage;
            }

            if (svgFragment != null)
            {
                FragmentDrawable = new FragmentDrawable(svgFragment, skOwnerBounds, root, this, ignoreAttributes);
                _disposable.Add(FragmentDrawable);
            }

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgImage);

            if (Image != null)
            {
                TransformedBounds = DestRect;
            }

            if (FragmentDrawable != null)
            {
                //_skBounds = _fragmentDrawable._skBounds;
                TransformedBounds = DestRect;
            }

            Transform = SvgTransformsExtensions.ToSKMatrix(svgImage.Transforms);
            FragmentTransform = SKMatrix.MakeIdentity();
            if (FragmentDrawable != null)
            {
                float dx = DestRect.Left;
                float dy = DestRect.Top;
                float sx = DestRect.Width / SrcRect.Width;
                float sy = DestRect.Height / SrcRect.Height;
                var skTranslationMatrix = SKMatrix.MakeTranslation(dx, dy);
                var skScaleMatrix = SKMatrix.MakeScale(sx, sy);
                FragmentTransform = FragmentTransform.PreConcat(skTranslationMatrix);
                FragmentTransform = FragmentTransform.PreConcat(skScaleMatrix);
            }

            Fill = null;
            Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            TransformedBounds = Transform.MapRect(TransformedBounds);
        }

        public override void OnDraw(SKCanvas canvas, Attributes ignoreAttributes, Drawable? until)
        {
            if (until != null && this == until)
            {
                return;
            }

            if (Image != null)
            {
                using var skImagePaint = new SKPaint()
                {
                    IsAntialias = true,
                    FilterQuality = SKFilterQuality.High
                };
                canvas.DrawImage(Image, SrcRect, DestRect, skImagePaint);
            }

            if (FragmentDrawable != null)
            {
                canvas.Save();

                var skMatrixTotal = canvas.TotalMatrix;
                skMatrixTotal = skMatrixTotal.PreConcat(FragmentTransform);
                canvas.SetMatrix(skMatrixTotal);

                FragmentDrawable.Draw(canvas, ignoreAttributes, until);

                canvas.Restore();
            }
        }

        public override void PostProcess()
        {
            base.PostProcess();
            FragmentDrawable?.PostProcess();
        }

        public override Drawable? HitTest(SKPoint skPoint)
        {
            if (Image != null)
            {
                if (DestRect.Contains(skPoint))
                {
                    return this;
                }
            }

            if (FragmentDrawable != null)
            {
                var result = FragmentDrawable?.HitTest(skPoint);
                if (result != null)
                {
                    return result;
                }
            }

            return base.HitTest(skPoint);
        }
    }

    public class SwitchDrawable : Drawable
    {
        public Drawable? FirstChild;

        public SwitchDrawable(SvgSwitch svgSwitch, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(svgSwitch, root, parent)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgSwitch, IgnoreAttributes) && HasFeatures(svgSwitch, IgnoreAttributes);

            if (!IsDrawable)
            {
                return;
            }

            foreach (var child in svgSwitch.Children)
            {
                if (!IsKnownElement(child))
                {
                    continue;
                }

                bool hasRequiredFeatures = HasRequiredFeatures(child);
                bool hasRequiredExtensions = HasRequiredExtensions(child);
                bool hasSystemLanguage = HasSystemLanguage(child);

                if (hasRequiredFeatures && hasRequiredExtensions && hasSystemLanguage)
                {
                    //var ignoreAttributesSwitch = ignoreAttributes 
                    //    | Attributes.Visibility
                    //    | Attributes.Display
                    //    | Attributes.RequiredFeatures
                    //    | Attributes.RequiredExtensions
                    //    | Attributes.SystemLanguage;

                    var drawable = DrawableFactory.Create(child, skOwnerBounds, root, parent, ignoreAttributes);
                    if (drawable != null)
                    {
                        FirstChild = drawable;
                        _disposable.Add(FirstChild);
                    }
                    break;
                }
            }

            if (FirstChild == null)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgSwitch);

            TransformedBounds = FirstChild.TransformedBounds;

            Transform = SvgTransformsExtensions.ToSKMatrix(svgSwitch.Transforms);

            Fill = null;
            Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            TransformedBounds = Transform.MapRect(TransformedBounds);
        }

        public override void OnDraw(SKCanvas canvas, Attributes ignoreAttributes, Drawable? until)
        {
            if (until != null && this == until)
            {
                return;
            }

            FirstChild?.Draw(canvas, ignoreAttributes, until);
        }

        public override void PostProcess()
        {
            base.PostProcess();
            FirstChild?.PostProcess();
        }

        public override Drawable? HitTest(SKPoint skPoint)
        {
            var result = FirstChild?.HitTest(skPoint);
            if (result != null)
            {
                return result;
            }
            return base.HitTest(skPoint);
        }
    }

    public class UseDrawable : Drawable
    {
        public Drawable? ReferencedDrawable;

        public UseDrawable(SvgUse svgUse, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(svgUse, root, parent)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgUse, IgnoreAttributes) && HasFeatures(svgUse, IgnoreAttributes);

            if (!IsDrawable)
            {
                return;
            }

            if (SvgExtensions.HasRecursiveReference(svgUse, (e) => e.ReferencedElement, new HashSet<Uri>()))
            {
                IsDrawable = false;
                return;
            }

            var svgReferencedElement = SvgExtensions.GetReference<SvgElement>(svgUse, svgUse.ReferencedElement);
            if (svgReferencedElement == null)
            {
                IsDrawable = false;
                return;
            }

            float x = svgUse.X.ToDeviceValue(UnitRenderingType.Horizontal, svgUse, skOwnerBounds);
            float y = svgUse.Y.ToDeviceValue(UnitRenderingType.Vertical, svgUse, skOwnerBounds);
            float width = svgUse.Width.ToDeviceValue(UnitRenderingType.Horizontal, svgUse, skOwnerBounds);
            float height = svgUse.Height.ToDeviceValue(UnitRenderingType.Vertical, svgUse, skOwnerBounds);

            if (width <= 0f)
            {
                width = new SvgUnit(SvgUnitType.Percentage, 100f).ToDeviceValue(UnitRenderingType.Horizontal, svgUse, skOwnerBounds);
            }

            if (height <= 0f)
            {
                height = new SvgUnit(SvgUnitType.Percentage, 100f).ToDeviceValue(UnitRenderingType.Vertical, svgUse, skOwnerBounds);
            }

            var originalReferencedElementParent = svgReferencedElement.Parent;
            var referencedElementParent = default(FieldInfo);

            try
            {
                referencedElementParent = svgReferencedElement.GetType().GetField("_parent", BindingFlags.NonPublic | BindingFlags.Instance);
                if (referencedElementParent != null)
                {
                    referencedElementParent.SetValue(svgReferencedElement, svgUse);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine(ex.StackTrace);
            }

            svgReferencedElement.InvalidateChildPaths();

            if (svgReferencedElement is SvgSymbol svgSymbol)
            {
                ReferencedDrawable = new SymbolDrawable(svgSymbol, x, y, width, height, skOwnerBounds, root, this, ignoreAttributes);
                _disposable.Add(ReferencedDrawable);
            }
            else
            {
                var drawable = DrawableFactory.Create(svgReferencedElement, skOwnerBounds, root, this, ignoreAttributes);
                if (drawable != null)
                {
                    ReferencedDrawable = drawable;
                    _disposable.Add(ReferencedDrawable);
                }
                else
                {
                    IsDrawable = false;
                    return;
                }
            }

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgUse);

            TransformedBounds = ReferencedDrawable.TransformedBounds;

            Transform = SvgTransformsExtensions.ToSKMatrix(svgUse.Transforms);
            if (!(svgReferencedElement is SvgSymbol))
            {
                var skMatrixTranslateXY = SKMatrix.MakeTranslation(x, y);
                Transform = Transform.PreConcat(skMatrixTranslateXY);
            }

            Fill = null;
            Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            TransformedBounds = Transform.MapRect(TransformedBounds);

            try
            {
                if (referencedElementParent != null)
                {
                    referencedElementParent.SetValue(svgReferencedElement, originalReferencedElementParent);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine(ex.StackTrace);
            }
        }

        public override void OnDraw(SKCanvas canvas, Attributes ignoreAttributes, Drawable? until)
        {
            if (until != null && this == until)
            {
                return;
            }

            ReferencedDrawable?.Draw(canvas, ignoreAttributes, until);
        }

        public override void PostProcess()
        {
            base.PostProcess();
            // TODO: Fix PostProcess() using correct ReferencedElement Parent.
            ReferencedDrawable?.PostProcess();
        }

        public override Drawable? HitTest(SKPoint skPoint)
        {
            var result = ReferencedDrawable?.HitTest(skPoint);
            if (result != null)
            {
                return result;
            }
            return base.HitTest(skPoint);
        }
    }

    public class CircleDrawable : DrawablePath
    {
        public CircleDrawable(SvgCircle svgCircle, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(svgCircle, root, parent)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgCircle, IgnoreAttributes) && HasFeatures(svgCircle, IgnoreAttributes);

            if (!IsDrawable)
            {
                return;
            }

            Path = svgCircle.ToSKPath(svgCircle.FillRule, skOwnerBounds, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgCircle);

            TransformedBounds = Path.Bounds;

            Transform = SvgTransformsExtensions.ToSKMatrix(svgCircle.Transforms);

            bool canDrawFill = true;
            bool canDrawStroke = true;

            if (SvgPaintingExtensions.IsValidFill(svgCircle))
            {
                Fill = SvgPaintingExtensions.GetFillSKPaint(svgCircle, TransformedBounds, ignoreAttributes, _disposable);
                if (Fill == null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgPaintingExtensions.IsValidStroke(svgCircle, TransformedBounds))
            {
                Stroke = SvgPaintingExtensions.GetStrokeSKPaint(svgCircle, TransformedBounds, ignoreAttributes, _disposable);
                if (Stroke == null)
                {
                    canDrawStroke = false;
                }
            }

            if (canDrawFill && !canDrawStroke)
            {
                IsDrawable = false;
                return;
            }

            // TODO: Transform _skBounds using _skMatrix.
            TransformedBounds = Transform.MapRect(TransformedBounds);
        }
    }

    public class EllipseDrawable : DrawablePath
    {
        public EllipseDrawable(SvgEllipse svgEllipse, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(svgEllipse, root, parent)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgEllipse, IgnoreAttributes) && HasFeatures(svgEllipse, IgnoreAttributes);

            if (!IsDrawable)
            {
                return;
            }

            Path = svgEllipse.ToSKPath(svgEllipse.FillRule, skOwnerBounds, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgEllipse);

            TransformedBounds = Path.Bounds;

            Transform = SvgTransformsExtensions.ToSKMatrix(svgEllipse.Transforms);

            bool canDrawFill = true;
            bool canDrawStroke = true;

            if (SvgPaintingExtensions.IsValidFill(svgEllipse))
            {
                Fill = SvgPaintingExtensions.GetFillSKPaint(svgEllipse, TransformedBounds, ignoreAttributes, _disposable);
                if (Fill == null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgPaintingExtensions.IsValidStroke(svgEllipse, TransformedBounds))
            {
                Stroke = SvgPaintingExtensions.GetStrokeSKPaint(svgEllipse, TransformedBounds, ignoreAttributes, _disposable);
                if (Stroke == null)
                {
                    canDrawStroke = false;
                }
            }

            if (canDrawFill && !canDrawStroke)
            {
                IsDrawable = false;
                return;
            }

            // TODO: Transform _skBounds using _skMatrix.
            TransformedBounds = Transform.MapRect(TransformedBounds);
        }
    }

    public class RectangleDrawable : DrawablePath
    {
        public RectangleDrawable(SvgRectangle svgRectangle, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(svgRectangle, root, parent)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgRectangle, IgnoreAttributes) && HasFeatures(svgRectangle, IgnoreAttributes);

            if (!IsDrawable)
            {
                return;
            }

            Path = svgRectangle.ToSKPath(svgRectangle.FillRule, skOwnerBounds, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgRectangle);

            TransformedBounds = Path.Bounds;

            Transform = SvgTransformsExtensions.ToSKMatrix(svgRectangle.Transforms);

            bool canDrawFill = true;
            bool canDrawStroke = true;

            if (SvgPaintingExtensions.IsValidFill(svgRectangle))
            {
                Fill = SvgPaintingExtensions.GetFillSKPaint(svgRectangle, TransformedBounds, ignoreAttributes, _disposable);
                if (Fill == null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgPaintingExtensions.IsValidStroke(svgRectangle, TransformedBounds))
            {
                Stroke = SvgPaintingExtensions.GetStrokeSKPaint(svgRectangle, TransformedBounds, ignoreAttributes, _disposable);
                if (Stroke == null)
                {
                    canDrawStroke = false;
                }
            }

            if (canDrawFill && !canDrawStroke)
            {
                IsDrawable = false;
                return;
            }

            // TODO: Transform _skBounds using _skMatrix.
            TransformedBounds = Transform.MapRect(TransformedBounds);
        }
    }

    public class GroupDrawable : DrawableContainer
    {
        public GroupDrawable(SvgGroup svgGroup, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(svgGroup, root, parent)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgGroup, IgnoreAttributes) && HasFeatures(svgGroup, IgnoreAttributes);

            // NOTE: Call AddMarkers only once.
            SvgMarkerExtensions.AddMarkers(svgGroup);

            CreateChildren(svgGroup, skOwnerBounds, root, this, ignoreAttributes);

            // TODO: Check if children are explicitly set to be visible.
            //foreach (var child in ChildrenDrawables)
            //{
            //    if (child.IsDrawable)
            //    {
            //        IsDrawable = true;
            //        break;
            //    }
            //}

            if (!IsDrawable)
            {
                return;
            }

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgGroup);

            TransformedBounds = SKRect.Empty;

            CreateTransformedBounds();

            Transform = SvgTransformsExtensions.ToSKMatrix(svgGroup.Transforms);

            Fill = null;
            Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            TransformedBounds = Transform.MapRect(TransformedBounds);
        }
    }

    public class LineDrawable : DrawablePath
    {
        public LineDrawable(SvgLine svgLine, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(svgLine, root, parent)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgLine, IgnoreAttributes) && HasFeatures(svgLine, IgnoreAttributes);

            if (!IsDrawable)
            {
                return;
            }

            Path = svgLine.ToSKPath(svgLine.FillRule, skOwnerBounds, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgLine);

            TransformedBounds = Path.Bounds;

            Transform = SvgTransformsExtensions.ToSKMatrix(svgLine.Transforms);

            bool canDrawFill = true;
            bool canDrawStroke = true;

            if (SvgPaintingExtensions.IsValidFill(svgLine))
            {
                Fill = SvgPaintingExtensions.GetFillSKPaint(svgLine, TransformedBounds, ignoreAttributes, _disposable);
                if (Fill == null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgPaintingExtensions.IsValidStroke(svgLine, TransformedBounds))
            {
                Stroke = SvgPaintingExtensions.GetStrokeSKPaint(svgLine, TransformedBounds, ignoreAttributes, _disposable);
                if (Stroke == null)
                {
                    canDrawStroke = false;
                }
            }

            if (canDrawFill && !canDrawStroke)
            {
                IsDrawable = false;
                return;
            }

            SvgMarkerExtensions.CreateMarkers(svgLine, Path, skOwnerBounds, ref MarkerDrawables, _disposable);

            // TODO: Transform _skBounds using _skMatrix.
            TransformedBounds = Transform.MapRect(TransformedBounds);
        }
    }

    public class PathDrawable : DrawablePath
    {
        public PathDrawable(SvgPath svgPath, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(svgPath, root, parent)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgPath, IgnoreAttributes) && HasFeatures(svgPath, IgnoreAttributes);

            if (!IsDrawable)
            {
                return;
            }

            Path = svgPath.PathData?.ToSKPath(svgPath.FillRule, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgPath);

            TransformedBounds = Path.Bounds;

            Transform = SvgTransformsExtensions.ToSKMatrix(svgPath.Transforms);

            bool canDrawFill = true;
            bool canDrawStroke = true;

            if (SvgPaintingExtensions.IsValidFill(svgPath))
            {
                Fill = SvgPaintingExtensions.GetFillSKPaint(svgPath, TransformedBounds, ignoreAttributes, _disposable);
                if (Fill == null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgPaintingExtensions.IsValidStroke(svgPath, TransformedBounds))
            {
                Stroke = SvgPaintingExtensions.GetStrokeSKPaint(svgPath, TransformedBounds, ignoreAttributes, _disposable);
                if (Stroke == null)
                {
                    canDrawStroke = false;
                }
            }

            if (canDrawFill && !canDrawStroke)
            {
                IsDrawable = false;
                return;
            }

            SvgMarkerExtensions.CreateMarkers(svgPath, Path, skOwnerBounds, ref MarkerDrawables, _disposable);

            // TODO: Transform _skBounds using _skMatrix.
            TransformedBounds = Transform.MapRect(TransformedBounds);
        }
    }

    public class PolylineDrawable : DrawablePath
    {
        public PolylineDrawable(SvgPolyline svgPolyline, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(svgPolyline, root, parent)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgPolyline, IgnoreAttributes) && HasFeatures(svgPolyline, IgnoreAttributes);

            if (!IsDrawable)
            {
                return;
            }

            Path = svgPolyline.Points?.ToSKPath(svgPolyline.FillRule, false, skOwnerBounds, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgPolyline);

            TransformedBounds = Path.Bounds;

            Transform = SvgTransformsExtensions.ToSKMatrix(svgPolyline.Transforms);

            bool canDrawFill = true;
            bool canDrawStroke = true;

            if (SvgPaintingExtensions.IsValidFill(svgPolyline))
            {
                Fill = SvgPaintingExtensions.GetFillSKPaint(svgPolyline, TransformedBounds, ignoreAttributes, _disposable);
                if (Fill == null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgPaintingExtensions.IsValidStroke(svgPolyline, TransformedBounds))
            {
                Stroke = SvgPaintingExtensions.GetStrokeSKPaint(svgPolyline, TransformedBounds, ignoreAttributes, _disposable);
                if (Stroke == null)
                {
                    canDrawStroke = false;
                }
            }

            if (canDrawFill && !canDrawStroke)
            {
                IsDrawable = false;
                return;
            }

            SvgMarkerExtensions.CreateMarkers(svgPolyline, Path, skOwnerBounds, ref MarkerDrawables, _disposable);

            // TODO: Transform _skBounds using _skMatrix.
            TransformedBounds = Transform.MapRect(TransformedBounds);
        }
    }

    public class PolygonDrawable : DrawablePath
    {
        public PolygonDrawable(SvgPolygon svgPolygon, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(svgPolygon, root, parent)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgPolygon, IgnoreAttributes) && HasFeatures(svgPolygon, IgnoreAttributes);

            if (!IsDrawable)
            {
                return;
            }

            Path = svgPolygon.Points?.ToSKPath(svgPolygon.FillRule, true, skOwnerBounds, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgPolygon);

            TransformedBounds = Path.Bounds;

            Transform = SvgTransformsExtensions.ToSKMatrix(svgPolygon.Transforms);

            bool canDrawFill = true;
            bool canDrawStroke = true;

            if (SvgPaintingExtensions.IsValidFill(svgPolygon))
            {
                Fill = SvgPaintingExtensions.GetFillSKPaint(svgPolygon, TransformedBounds, ignoreAttributes, _disposable);
                if (Fill == null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgPaintingExtensions.IsValidStroke(svgPolygon, TransformedBounds))
            {
                Stroke = SvgPaintingExtensions.GetStrokeSKPaint(svgPolygon, TransformedBounds, ignoreAttributes, _disposable);
                if (Stroke == null)
                {
                    canDrawStroke = false;
                }
            }

            if (canDrawFill && !canDrawStroke)
            {
                IsDrawable = false;
                return;
            }

            SvgMarkerExtensions.CreateMarkers(svgPolygon, Path, skOwnerBounds, ref MarkerDrawables, _disposable);

            // TODO: Transform _skBounds using _skMatrix.
            TransformedBounds = Transform.MapRect(TransformedBounds);
        }
    }

    public class TextDrawable : Drawable
    {
        private static readonly Regex s_multipleSpaces = new Regex(@" {2,}", RegexOptions.Compiled);

        // TODO: Implement drawable.

        private readonly SvgText _svgText;
        private SKRect _skOwnerBounds;

        public TextDrawable(SvgText svgText, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(svgText, root, parent)
        {
            _svgText = svgText;
            _skOwnerBounds = skOwnerBounds;
            IgnoreAttributes = ignoreAttributes;
        }

        internal void GetPositionsX(SvgTextBase svgTextBase, SKRect skBounds, List<float> xs)
        {
            var _x = svgTextBase.X;

            for (int i = 0; i < _x.Count; i++)
            {
                xs.Add(_x[i].ToDeviceValue(UnitRenderingType.HorizontalOffset, svgTextBase, skBounds));
            }
        }

        internal void GetPositionsY(SvgTextBase svgTextBase, SKRect skBounds, List<float> ys)
        {
            var _y = svgTextBase.Y;

            for (int i = 0; i < _y.Count; i++)
            {
                ys.Add(_y[i].ToDeviceValue(UnitRenderingType.VerticalOffset, svgTextBase, skBounds));
            }
        }

        internal void GetPositionsDX(SvgTextBase svgTextBase, SKRect skBounds, List<float> dxs)
        {
            var _dx = svgTextBase.Dx;

            for (int i = 0; i < _dx.Count; i++)
            {
                dxs.Add(_dx[i].ToDeviceValue(UnitRenderingType.HorizontalOffset, svgTextBase, skBounds));
            }
        }

        internal void GetPositionsDY(SvgTextBase svgTextBase, SKRect skBounds, List<float> dys)
        {
            var _dy = svgTextBase.Dy;

            for (int i = 0; i < _dy.Count; i++)
            {
                dys.Add(_dy[i].ToDeviceValue(UnitRenderingType.VerticalOffset, svgTextBase, skBounds));
            }
        }

        internal virtual IEnumerable<ISvgNode> GetContentNodes(SvgTextBase svgTextBase)
        {
            return svgTextBase.Nodes == null || svgTextBase.Nodes.Count < 1 ?
                svgTextBase.Children.OfType<ISvgNode>().Where(o => !(o is ISvgDescriptiveElement)) :
                svgTextBase.Nodes;
        }

        internal string PrepareText(SvgTextBase svgTextBase, string value)
        {
            value = ApplyTransformation(svgTextBase, value);
            value = new StringBuilder(value).Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').ToString();
            return svgTextBase.SpaceHandling == XmlSpaceHandling.preserve ? value : s_multipleSpaces.Replace(value.Trim(), " ");
        }

        internal string ApplyTransformation(SvgTextBase svgTextBase, string value)
        {
            return svgTextBase.TextTransformation switch
            {
                SvgTextTransformation.Capitalize => value.ToUpper(),
                SvgTextTransformation.Uppercase => value.ToUpper(),
                SvgTextTransformation.Lowercase => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value),
                _ => value,
            };
        }

        internal void BeginDraw(SvgTextBase svgTextBase, SKCanvas skCanvas, SKRect skBounds, Attributes ignoreAttributes, CompositeDisposable disposable, out MaskDrawable? maskDrawable, out SKPaint? maskDstIn, out SKPaint? skPaintOpacity, out SKPaint? skPaintFilter)
        {
            var enableClip = !ignoreAttributes.HasFlag(Attributes.ClipPath);
            var enableMask = !ignoreAttributes.HasFlag(Attributes.Mask);
            var enableOpacity = !ignoreAttributes.HasFlag(Attributes.Opacity);
            var enableFilter = !ignoreAttributes.HasFlag(Attributes.Filter);

            skCanvas.Save();

            var skMatrix = SvgTransformsExtensions.ToSKMatrix(svgTextBase.Transforms);

            var skMatrixTotal = skCanvas.TotalMatrix;
            skMatrixTotal = skMatrixTotal.PreConcat(skMatrix);
            skCanvas.SetMatrix(skMatrixTotal);

            if (enableClip == true)
            {
                var skPathClip = SvgClippingExtensions.GetSvgVisualElementClipPath(svgTextBase, skBounds, new HashSet<Uri>(), disposable);
                if (skPathClip != null && !IgnoreAttributes.HasFlag(Attributes.ClipPath))
                {
                    bool antialias = SvgPaintingExtensions.IsAntialias(svgTextBase);
                    skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
                }
            }

            if (enableMask == true)
            {
                var mask = default(SKPaint);
                maskDstIn = default(SKPaint);
                maskDrawable = SvgClippingExtensions.GetSvgElementMask(svgTextBase, skBounds, new HashSet<Uri>(), disposable);
                if (maskDrawable != null)
                {
                    mask = new SKPaint()
                    {
                        IsAntialias = true,
                        Style = SKPaintStyle.StrokeAndFill
                    };
                    disposable.Add(mask);

                    var lumaColor = SKColorFilter.CreateLumaColor();
                    _disposable.Add(lumaColor);

                    maskDstIn = new SKPaint
                    {
                        IsAntialias = true,
                        Style = SKPaintStyle.StrokeAndFill,
                        BlendMode = SKBlendMode.DstIn,
                        Color = SvgPaintingExtensions.TransparentBlack,
                        ColorFilter = lumaColor
                    };
                    disposable.Add(maskDstIn);
                    skCanvas.SaveLayer(mask);
                }
            }
            else
            {
                maskDstIn = null;
                maskDrawable = null;
            }

            if (enableOpacity == true)
            {
                skPaintOpacity = SvgPaintingExtensions.GetOpacitySKPaint(svgTextBase, disposable);
                if (skPaintOpacity != null && !IgnoreAttributes.HasFlag(Attributes.Opacity))
                {
                    skCanvas.SaveLayer(skPaintOpacity);
                }
            }
            else
            {
                skPaintOpacity = null;
            }

            if (enableFilter == true)
            {
                skPaintFilter = SvgFiltersExtensions.GetFilterSKPaint(svgTextBase, skBounds, this, disposable, out var isValid);
                if (skPaintFilter != null && !IgnoreAttributes.HasFlag(Attributes.Filter))
                {
                    skCanvas.SaveLayer(skPaintFilter);
                }
            }
            else
            {
                skPaintFilter = null;
            }
        }

        internal void EndDraw(SKCanvas skCanvas, Attributes ignoreAttributes, MaskDrawable? maskDrawable, SKPaint? maskDstIn, SKPaint? skPaintOpacity, SKPaint? skPaintFilter, Drawable? until)
        {
            var enableMask = !ignoreAttributes.HasFlag(Attributes.Mask);
            var enableOpacity = !ignoreAttributes.HasFlag(Attributes.Opacity);
            var enableFilter = !ignoreAttributes.HasFlag(Attributes.Filter);

            if (skPaintFilter != null && enableFilter == true)
            {
                skCanvas.Restore();
            }

            if (skPaintOpacity != null && enableOpacity == true)
            {
                skCanvas.Restore();
            }

            if (maskDrawable != null && enableMask == true)
            {
                skCanvas.SaveLayer(maskDstIn);
                maskDrawable.Draw(skCanvas, ignoreAttributes, until);
                skCanvas.Restore();
                skCanvas.Restore();
            }

            skCanvas.Restore();
        }

        internal void DrawTextString(SvgTextBase svgTextBase, string text, float x, float y, SKRect skOwnerBounds, Attributes ignoreAttributes, SKCanvas skCanvas, Drawable? until)
        {
            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            if (SvgPaintingExtensions.IsValidFill(svgTextBase))
            {
                var skPaint = SvgPaintingExtensions.GetFillSKPaint(svgTextBase, skBounds, ignoreAttributes, _disposable);
                if (skPaint != null)
                {
                    SvgTextExtensions.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
#if USE_TEXT_SHAPER
                    var typeface = skPaint.Typeface;
                    if (typeface != null)
                    {
                        using var skShaper = new SKShaper(skPaint.Typeface);
                        skCanvas.DrawShapedText(skShaper, text, x, y, skPaint);
                    }
#else
                    skCanvas.DrawText(text, x, y, skPaint);
#endif
                }
            }

            if (SvgPaintingExtensions.IsValidStroke(svgTextBase, skBounds))
            {
                var skPaint = SvgPaintingExtensions.GetStrokeSKPaint(svgTextBase, skBounds, ignoreAttributes, _disposable);
                if (skPaint != null)
                {
                    SvgTextExtensions.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
#if USE_TEXT_SHAPER
                    var typeface = skPaint.Typeface;
                    if (typeface != null)
                    {
                        using var skShaper = new SKShaper(skPaint.Typeface);
                        skCanvas.DrawShapedText(skShaper, text, x, y, skPaint);
                    }
#else
                    skCanvas.DrawText(text, x, y, skPaint);
#endif
                }
            }
        }

        internal void DrawTextBase(SvgTextBase svgTextBase, string? text, float currentX, float currentY, SKRect skOwnerBounds, Attributes ignoreAttributes, SKCanvas skCanvas, Drawable? until)
        {
            // TODO: Fix SvgTextBase rendering.
            bool isValidFill = SvgPaintingExtensions.IsValidFill(svgTextBase);
            bool isValidStroke = SvgPaintingExtensions.IsValidStroke(svgTextBase, skOwnerBounds);

            if ((!isValidFill && !isValidStroke) || text == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            var xs = new List<float>();
            var ys = new List<float>();
            var dxs = new List<float>();
            var dys = new List<float>();

            GetPositionsX(svgTextBase, skOwnerBounds, xs);
            GetPositionsY(svgTextBase, skOwnerBounds, ys);
            GetPositionsDX(svgTextBase, skOwnerBounds, dxs);
            GetPositionsDY(svgTextBase, skOwnerBounds, dys);

            if (xs.Count >= 1 && ys.Count >= 1 && xs.Count == ys.Count && xs.Count == text.Length)
            {
                // TODO: Fix text position rendering.
                var points = new SKPoint[xs.Count];

                for (int i = 0; i < xs.Count; i++)
                {
                    float x = xs[i];
                    float y = ys[i];
                    points[i] = new SKPoint(x, y);
                }

                // TODO: Calculate correct bounds.
                var skBounds = skOwnerBounds;

                if (SvgPaintingExtensions.IsValidFill(svgTextBase))
                {
                    var skPaint = SvgPaintingExtensions.GetFillSKPaint(svgTextBase, skBounds, ignoreAttributes, _disposable);
                    if (skPaint != null)
                    {
                        SvgTextExtensions.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
                        skCanvas.DrawPositionedText(text, points, skPaint);
                    }
                }

                if (SvgPaintingExtensions.IsValidStroke(svgTextBase, skBounds))
                {
                    var skPaint = SvgPaintingExtensions.GetStrokeSKPaint(svgTextBase, skBounds, ignoreAttributes, _disposable);
                    if (skPaint != null)
                    {
                        SvgTextExtensions.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
                        skCanvas.DrawPositionedText(text, points, skPaint);
                    }
                }
            }
            else
            {
                float x = (xs.Count >= 1) ? xs[0] : currentX;
                float y = (ys.Count >= 1) ? ys[0] : currentY;
                float dx = (dxs.Count >= 1) ? dxs[0] : 0f;
                float dy = (dys.Count >= 1) ? dys[0] : 0f;

                DrawTextString(svgTextBase, text, x + dx, y + dy, skOwnerBounds, ignoreAttributes, skCanvas, until);
            }
        }

        internal void DrawTextPath(SvgTextPath svgTextPath, float currentX, float currentY, SKRect skOwnerBounds, Attributes ignoreAttributes, SKCanvas skCanvas, Drawable? until)
        {
            if (!CanDraw(svgTextPath, ignoreAttributes) || !HasFeatures(svgTextPath, ignoreAttributes))
            {
                return;
            }

            if (SvgExtensions.HasRecursiveReference(svgTextPath, (e) => e.ReferencedPath, new HashSet<Uri>()))
            {
                return;
            }

            var svgPath = SvgExtensions.GetReference<SvgPath>(svgTextPath, svgTextPath.ReferencedPath);
            if (svgPath == null)
            {
                return;
            }

            var skPath = svgPath.PathData?.ToSKPath(svgPath.FillRule, _disposable);
            if (skPath == null || skPath.IsEmpty)
            {
                return;
            }

            var skMatrixPath = SvgTransformsExtensions.ToSKMatrix(svgPath.Transforms);
            skPath.Transform(skMatrixPath);

            // TODO: Implement StartOffset
            var startOffset = svgTextPath.StartOffset.ToDeviceValue(UnitRenderingType.Other, svgTextPath, skOwnerBounds);

            float hOffset = currentX + startOffset;
            float vOffset = currentY;

            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            SKPaint? maskDstIn, skPaintOpacity, skPaintFilter;
            MaskDrawable? maskDrawable;
            BeginDraw(svgTextPath, skCanvas, skBounds, ignoreAttributes, _disposable, out maskDrawable, out maskDstIn, out skPaintOpacity, out skPaintFilter);

            // TODO: Fix SvgTextPath rendering.
            bool isValidFill = SvgPaintingExtensions.IsValidFill(svgTextPath);
            bool isValidStroke = SvgPaintingExtensions.IsValidStroke(svgTextPath, skBounds);

            if (isValidFill || isValidStroke)
            {
                if (!string.IsNullOrEmpty(svgTextPath.Text))
                {
                    var text = PrepareText(svgTextPath, svgTextPath.Text);

                    if (SvgPaintingExtensions.IsValidFill(svgTextPath))
                    {
                        var skPaint = SvgPaintingExtensions.GetFillSKPaint(svgTextPath, skBounds, ignoreAttributes, _disposable);
                        if (skPaint != null)
                        {
                            SvgTextExtensions.SetSKPaintText(svgTextPath, skBounds, skPaint, _disposable);
                            skCanvas.DrawTextOnPath(text, skPath, hOffset, vOffset, skPaint);
                        }
                    }

                    if (SvgPaintingExtensions.IsValidStroke(svgTextPath, skBounds))
                    {
                        var skPaint = SvgPaintingExtensions.GetStrokeSKPaint(svgTextPath, skBounds, ignoreAttributes, _disposable);
                        if (skPaint != null)
                        {
                            SvgTextExtensions.SetSKPaintText(svgTextPath, skBounds, skPaint, _disposable);
                            skCanvas.DrawTextOnPath(text, skPath, hOffset, vOffset, skPaint);
                        }
                    }
                }
            }

            EndDraw(skCanvas, ignoreAttributes, maskDrawable, maskDstIn, skPaintOpacity, skPaintFilter, until);
        }

        internal void DrawTextRef(SvgTextRef svgTextRef, float currentX, float currentY, SKRect skOwnerBounds, Attributes ignoreAttributes, SKCanvas skCanvas, Drawable? until)
        {
            if (!CanDraw(svgTextRef, ignoreAttributes) || !HasFeatures(svgTextRef, ignoreAttributes))
            {
                return;
            }

            if (SvgExtensions.HasRecursiveReference(svgTextRef, (e) => e.ReferencedElement, new HashSet<Uri>()))
            {
                return;
            }

            var svgReferencedText = SvgExtensions.GetReference<SvgText>(svgTextRef, svgTextRef.ReferencedElement);
            if (svgReferencedText == null)
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            SKPaint? maskDstIn, skPaintOpacity, skPaintFilter;
            MaskDrawable? maskDrawable;
            BeginDraw(svgTextRef, skCanvas, skBounds, ignoreAttributes, _disposable, out maskDrawable, out maskDstIn, out skPaintOpacity, out skPaintFilter);

            // TODO: Draw svgReferencedText
            if (!string.IsNullOrEmpty(svgReferencedText.Text))
            {
                var text = PrepareText(svgReferencedText, svgReferencedText.Text);
                DrawTextBase(svgReferencedText, svgReferencedText.Text, currentX, currentY, skOwnerBounds, ignoreAttributes, skCanvas, until);
            }

            EndDraw(skCanvas, ignoreAttributes, maskDrawable, maskDstIn, skPaintOpacity, skPaintFilter, until);
        }

        internal void DrawTextSpan(SvgTextSpan svgTextSpan, float currentX, float currentY, SKRect skOwnerBounds, Attributes ignoreAttributes, SKCanvas skCanvas, Drawable? until)
        {
            if (!CanDraw(svgTextSpan, ignoreAttributes) || !HasFeatures(svgTextSpan, ignoreAttributes))
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            SKPaint? maskDstIn, skPaintOpacity, skPaintFilter;
            MaskDrawable? maskDrawable;
            BeginDraw(svgTextSpan, skCanvas, skBounds, ignoreAttributes, _disposable, out maskDrawable, out maskDstIn, out skPaintOpacity, out skPaintFilter);

            // TODO: Implement SvgTextSpan drawing.
            if (!string.IsNullOrEmpty(svgTextSpan.Text))
            {
                var text = PrepareText(svgTextSpan, svgTextSpan.Text);
                DrawTextBase(svgTextSpan, text, currentX, currentY, skOwnerBounds, ignoreAttributes, skCanvas, until);
            }

            EndDraw(skCanvas, ignoreAttributes, maskDrawable, maskDstIn, skPaintOpacity, skPaintFilter, until);
        }

        internal void DrawText(SvgText svgText, SKRect skOwnerBounds, Attributes ignoreAttributes, SKCanvas skCanvas, Drawable? until)
        {
            if (!CanDraw(svgText, ignoreAttributes) || !HasFeatures(svgText, ignoreAttributes))
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            SKPaint? maskDstIn, skPaintOpacity, skPaintFilter;
            MaskDrawable? maskDrawable;
            BeginDraw(svgText, skCanvas, skBounds, ignoreAttributes, _disposable, out maskDrawable, out maskDstIn, out skPaintOpacity, out skPaintFilter);

            var xs = new List<float>();
            var ys = new List<float>();
            var dxs = new List<float>();
            var dys = new List<float>();
            GetPositionsX(svgText, skOwnerBounds, xs);
            GetPositionsY(svgText, skOwnerBounds, ys);
            GetPositionsDX(svgText, skOwnerBounds, dxs);
            GetPositionsDY(svgText, skOwnerBounds, dys);

            float x = (xs.Count >= 1) ? xs[0] : 0f;
            float y = (ys.Count >= 1) ? ys[0] : 0f;
            float dx = (dxs.Count >= 1) ? dxs[0] : 0f;
            float dy = (dys.Count >= 1) ? dys[0] : 0f;

            float currentX = x + dx;
            float currentY = y + dy;

            foreach (var node in GetContentNodes(svgText))
            {
                if (!(node is SvgTextBase textNode))
                {
                    if (!string.IsNullOrEmpty(node.Content))
                    {
                        var text = PrepareText(svgText, node.Content);
                        DrawTextBase(svgText, text, 0f, 0f, skOwnerBounds, ignoreAttributes, skCanvas, until);
                    }
                }
                else
                {
                    switch (textNode)
                    {
                        case SvgTextPath svgTextPath:
                            DrawTextPath(svgTextPath, currentX, currentY, skOwnerBounds, ignoreAttributes, skCanvas, until);
                            break;
                        case SvgTextRef svgTextRef:
                            DrawTextRef(svgTextRef, currentX, currentY, skOwnerBounds, ignoreAttributes, skCanvas, until);
                            break;
                        case SvgTextSpan svgTextSpan:
                            DrawTextSpan(svgTextSpan, currentX, currentY, skOwnerBounds, ignoreAttributes, skCanvas, until);
                            break;
                        default:
                            break;
                    }
                }
            }

            EndDraw(skCanvas, ignoreAttributes, maskDrawable, maskDstIn, skPaintOpacity, skPaintFilter, until);
        }

        public override void OnDraw(SKCanvas canvas, Attributes ignoreAttributes, Drawable? until)
        {
            if (until != null && this == until)
            {
                return;
            }

            // TODO: Currently using custom OnDraw override.
        }

        public override void Draw(SKCanvas canvas, Attributes ignoreAttributes, Drawable? until)
        {
            if (until != null && this == until)
            {
                return;
            }

            DrawText(_svgText, _skOwnerBounds, ignoreAttributes, canvas, until);
        }

        protected override void OnDraw(SKCanvas canvas)
        {
            // TODO:
            Draw(canvas, IgnoreAttributes, null);
        }

        public override void PostProcess()
        {
            // TODO:
        }
    }

    public class MarkerDrawable : Drawable
    {
        public Drawable? MarkerElementDrawable;
        public SKRect? MarkerClipRect;

        public MarkerDrawable(SvgMarker svgMarker, SvgVisualElement pOwner, SKPoint pMarkerPoint, float fAngle, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(svgMarker, root, parent)
        {
            IgnoreAttributes = Attributes.Display | ignoreAttributes;
            IsDrawable = true;

            if (!IsDrawable)
            {
                return;
            }

            var markerElement = GetMarkerElement(svgMarker);
            if (markerElement == null)
            {
                IsDrawable = false;
                return;
            }

            var skMarkerMatrix = SKMatrix.MakeIdentity();

            var skMatrixMarkerPoint = SKMatrix.MakeTranslation(pMarkerPoint.X, pMarkerPoint.Y);
            skMarkerMatrix = skMarkerMatrix.PreConcat(skMatrixMarkerPoint);

            var skMatrixAngle = SKMatrix.MakeRotationDegrees(svgMarker.Orient.IsAuto ? fAngle : svgMarker.Orient.Angle);
            skMarkerMatrix = skMarkerMatrix.PreConcat(skMatrixAngle);

            var strokeWidth = pOwner.StrokeWidth.ToDeviceValue(UnitRenderingType.Other, svgMarker, skOwnerBounds);

            var refX = svgMarker.RefX.ToDeviceValue(UnitRenderingType.Horizontal, svgMarker, skOwnerBounds);
            var refY = svgMarker.RefY.ToDeviceValue(UnitRenderingType.Vertical, svgMarker, skOwnerBounds);
            float markerWidth = svgMarker.MarkerWidth.ToDeviceValue(UnitRenderingType.Other, svgMarker, skOwnerBounds);
            float markerHeight = svgMarker.MarkerHeight.ToDeviceValue(UnitRenderingType.Other, svgMarker, skOwnerBounds);
            float viewBoxToMarkerUnitsScaleX = 1f;
            float viewBoxToMarkerUnitsScaleY = 1f;

            switch (svgMarker.MarkerUnits)
            {
                case SvgMarkerUnits.StrokeWidth:
                    {
                        var skMatrixStrokeWidth = SKMatrix.MakeScale(strokeWidth, strokeWidth);
                        skMarkerMatrix = skMarkerMatrix.PreConcat(skMatrixStrokeWidth);

                        var viewBoxWidth = svgMarker.ViewBox.Width;
                        var viewBoxHeight = svgMarker.ViewBox.Height;

                        var scaleFactorWidth = (viewBoxWidth <= 0) ? 1 : (markerWidth / viewBoxWidth);
                        var scaleFactorHeight = (viewBoxHeight <= 0) ? 1 : (markerHeight / viewBoxHeight);

                        viewBoxToMarkerUnitsScaleX = Math.Min(scaleFactorWidth, scaleFactorHeight);
                        viewBoxToMarkerUnitsScaleY = Math.Min(scaleFactorWidth, scaleFactorHeight);

                        var skMatrixTranslateRefXY = SKMatrix.MakeTranslation(-refX * viewBoxToMarkerUnitsScaleX, -refY * viewBoxToMarkerUnitsScaleY);
                        skMarkerMatrix = skMarkerMatrix.PreConcat(skMatrixTranslateRefXY);

                        var skMatrixScaleXY = SKMatrix.MakeScale(viewBoxToMarkerUnitsScaleX, viewBoxToMarkerUnitsScaleY);
                        skMarkerMatrix = skMarkerMatrix.PreConcat(skMatrixScaleXY);
                    }
                    break;
                case SvgMarkerUnits.UserSpaceOnUse:
                    {
                        var skMatrixTranslateRefXY = SKMatrix.MakeTranslation(-refX, -refY);
                        skMarkerMatrix = skMarkerMatrix.PreConcat(skMatrixTranslateRefXY);
                    }
                    break;
            }

            switch (svgMarker.Overflow)
            {
                case SvgOverflow.Auto:
                case SvgOverflow.Visible:
                case SvgOverflow.Inherit:
                    break;
                default:
                    MarkerClipRect = SKRect.Create(
                        svgMarker.ViewBox.MinX,
                        svgMarker.ViewBox.MinY,
                        markerWidth / viewBoxToMarkerUnitsScaleX,
                        markerHeight / viewBoxToMarkerUnitsScaleY);
                    break;
            }

            var drawable = DrawableFactory.Create(markerElement, skOwnerBounds, root, this, Attributes.Display);
            if (drawable != null)
            {
                MarkerElementDrawable = drawable;
                _disposable.Add(MarkerElementDrawable);
            }
            else
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgMarker);

            TransformedBounds = MarkerElementDrawable.TransformedBounds;

            Transform = SvgTransformsExtensions.ToSKMatrix(svgMarker.Transforms);
            Transform = Transform.PreConcat(skMarkerMatrix);

            Fill = null;
            Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            TransformedBounds = Transform.MapRect(TransformedBounds);
        }

        internal SvgVisualElement? GetMarkerElement(SvgMarker svgMarker)
        {
            SvgVisualElement? markerElement = null;

            foreach (var child in svgMarker.Children)
            {
                if (child is SvgVisualElement svgVisualElement)
                {
                    markerElement = svgVisualElement;
                    break;
                }
            }

            return markerElement;
        }

        public override void OnDraw(SKCanvas canvas, Attributes ignoreAttributes, Drawable? until)
        {
            if (until != null && this == until)
            {
                return;
            }

            if (MarkerClipRect != null)
            {
                canvas.ClipRect(MarkerClipRect.Value, SKClipOperation.Intersect);
            }

            MarkerElementDrawable?.Draw(canvas, ignoreAttributes, until);
        }

        public override void PostProcess()
        {
            base.PostProcess();
            MarkerElementDrawable?.PostProcess();
        }

        public override Drawable? HitTest(SKPoint skPoint)
        {
            var result = MarkerElementDrawable?.HitTest(skPoint);
            if (result != null)
            {
                return result;
            }
            return base.HitTest(skPoint);
        }
    }

    public class MaskDrawable : DrawableContainer
    {
        public MaskDrawable(SvgMask svgMask, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(svgMask, root, parent)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = true;

            if (!IsDrawable)
            {
                return;
            }
            var maskUnits = svgMask.MaskUnits;
            var maskContentUnits = svgMask.MaskContentUnits;
            var xUnit = svgMask.X;
            var yUnit = svgMask.Y;
            var widthUnit = svgMask.Width;
            var heightUnit = svgMask.Height;
            float x = xUnit.ToDeviceValue(UnitRenderingType.Horizontal, svgMask, skOwnerBounds);
            float y = yUnit.ToDeviceValue(UnitRenderingType.Vertical, svgMask, skOwnerBounds);
            float width = widthUnit.ToDeviceValue(UnitRenderingType.Horizontal, svgMask, skOwnerBounds);
            float height = heightUnit.ToDeviceValue(UnitRenderingType.Vertical, svgMask, skOwnerBounds);

            if (width <= 0 || height <= 0)
            {
                IsDrawable = false;
                return;
            }

            if (maskUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                if (xUnit.Type != SvgUnitType.Percentage)
                {
                    x *= skOwnerBounds.Width;
                }

                if (yUnit.Type != SvgUnitType.Percentage)
                {
                    y *= skOwnerBounds.Height;
                }

                if (widthUnit.Type != SvgUnitType.Percentage)
                {
                    width *= skOwnerBounds.Width;
                }

                if (heightUnit.Type != SvgUnitType.Percentage)
                {
                    height *= skOwnerBounds.Height;
                }

                x += skOwnerBounds.Left;
                y += skOwnerBounds.Top;
            }

            SKRect skRectTransformed = SKRect.Create(x, y, width, height);

            var skMatrix = SKMatrix.MakeIdentity();

            if (maskContentUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                var skBoundsTranslateTransform = SKMatrix.MakeTranslation(skOwnerBounds.Left, skOwnerBounds.Top);
                skMatrix = skMatrix.PreConcat(skBoundsTranslateTransform);

                var skBoundsScaleTransform = SKMatrix.MakeScale(skOwnerBounds.Width, skOwnerBounds.Height);
                skMatrix = skMatrix.PreConcat(skBoundsScaleTransform);
            }

            CreateChildren(svgMask, skOwnerBounds, root, this, ignoreAttributes);

            Overflow = skRectTransformed;

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgMask);

            TransformedBounds = skRectTransformed;

            Transform = skMatrix;

            Fill = null;
            Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            TransformedBounds = Transform.MapRect(TransformedBounds);
        }

        public override void PostProcess()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            var enableMask = !IgnoreAttributes.HasFlag(Attributes.Mask);

            ClipPath = null;

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

            Opacity = null;
            Filter = null;
        }

    }

    public class SymbolDrawable : DrawableContainer
    {
        public SymbolDrawable(SvgSymbol svgSymbol, float x, float y, float width, float height, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes)
            : base(svgSymbol, root, parent)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgSymbol, IgnoreAttributes) && HasFeatures(svgSymbol, IgnoreAttributes);

            if (!IsDrawable)
            {
                return;
            }

            if (svgSymbol.CustomAttributes.TryGetValue("width", out string? _widthString))
            {
                if (new SvgUnitConverter().ConvertFromString(_widthString) is SvgUnit _width)
                {
                    width = _width.ToDeviceValue(UnitRenderingType.Horizontal, svgSymbol, skOwnerBounds);
                }
            }

            if (svgSymbol.CustomAttributes.TryGetValue("height", out string? heightString))
            {
                if (new SvgUnitConverter().ConvertFromString(heightString) is SvgUnit _height)
                {
                    height = _height.ToDeviceValue(UnitRenderingType.Vertical, svgSymbol, skOwnerBounds);
                }
            }

            SvgOverflow svgOverflow = SvgOverflow.Hidden;
            if (svgSymbol.TryGetAttribute("overflow", out string overflowString))
            {
                if (new SvgOverflowConverter().ConvertFromString(overflowString) is SvgOverflow _svgOverflow)
                {
                    svgOverflow = _svgOverflow;
                }
            }

            switch (svgOverflow)
            {
                case SvgOverflow.Auto:
                case SvgOverflow.Visible:
                case SvgOverflow.Inherit:
                    break;
                default:
                    Overflow = SKRect.Create(x, y, width, height);
                    break;
            }

            CreateChildren(svgSymbol, skOwnerBounds, root, this, ignoreAttributes);

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgSymbol);

            TransformedBounds = SKRect.Empty;

            CreateTransformedBounds();

            Transform = SvgTransformsExtensions.ToSKMatrix(svgSymbol.Transforms);
            var skMatrixViewBox = SvgTransformsExtensions.ToSKMatrix(svgSymbol.ViewBox, svgSymbol.AspectRatio, x, y, width, height);
            Transform = Transform.PreConcat(skMatrixViewBox);

            Fill = null;
            Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            TransformedBounds = Transform.MapRect(TransformedBounds);
        }
    }

    public static class DrawableFactory
    {
        public static Drawable? Create(SvgElement svgElement, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
        {
            return svgElement switch
            {
                SvgAnchor svgAnchor => new AnchorDrawable(svgAnchor, skOwnerBounds, root, parent, ignoreAttributes),
                SvgFragment svgFragment => new FragmentDrawable(svgFragment, skOwnerBounds, root, parent, ignoreAttributes),
                SvgImage svgImage => new ImageDrawable(svgImage, skOwnerBounds, root, parent, ignoreAttributes),
                SvgSwitch svgSwitch => new SwitchDrawable(svgSwitch, skOwnerBounds, root, parent, ignoreAttributes),
                SvgUse svgUse => new UseDrawable(svgUse, skOwnerBounds, root, parent, ignoreAttributes),
                SvgCircle svgCircle => new CircleDrawable(svgCircle, skOwnerBounds, root, parent, ignoreAttributes),
                SvgEllipse svgEllipse => new EllipseDrawable(svgEllipse, skOwnerBounds, root, parent, ignoreAttributes),
                SvgRectangle svgRectangle => new RectangleDrawable(svgRectangle, skOwnerBounds, root, parent, ignoreAttributes),
                SvgGroup svgGroup => new GroupDrawable(svgGroup, skOwnerBounds, root, parent, ignoreAttributes),
                SvgLine svgLine => new LineDrawable(svgLine, skOwnerBounds, root, parent, ignoreAttributes),
                SvgPath svgPath => new PathDrawable(svgPath, skOwnerBounds, root, parent, ignoreAttributes),
                SvgPolyline svgPolyline => new PolylineDrawable(svgPolyline, skOwnerBounds, root, parent, ignoreAttributes),
                SvgPolygon svgPolygon => new PolygonDrawable(svgPolygon, skOwnerBounds, root, parent, ignoreAttributes),
                SvgText svgText => new TextDrawable(svgText, skOwnerBounds, root, parent, ignoreAttributes),
                _ => null,
            };
        }
    }
}
