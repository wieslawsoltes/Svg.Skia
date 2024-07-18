using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Svg.Model.Drawables.Elements;
using ShimSkiaSharp;

namespace Svg.Model.Drawables;

public abstract class DrawableBase : SKDrawable, IFilterSource, IPictureSource
{
    public IAssetLoader AssetLoader { get; }
    public HashSet<Uri>? References { get; }
    public SvgElement? Element { get; set; }
    public DrawableBase? Parent { get; set; }
    public bool IsDrawable { get; set; }
    public DrawAttributes IgnoreAttributes { get; set; }
    public bool IsAntialias { get; set; }
    public SKRect GeometryBounds { get; set; }
    public SKRect TransformedBounds { get; set; }
    public SKMatrix Transform { get; set; }
    public SKMatrix TotalTransform { get; set; }
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

    protected DrawableBase(IAssetLoader assetLoader, HashSet<Uri>? references)
    {
        AssetLoader = assetLoader;
        References = references;
    }

    protected override void OnDraw(SKCanvas canvas)
    {
        Draw(canvas, IgnoreAttributes, null, true);
#if USE_DEBUG_DRAW_BOUNDS
        DebugDrawBounds(canvas);
#endif
    }

    protected override SKRect OnGetBounds()
    {
        return IsDrawable ? GeometryBounds : SKRect.Empty;
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
            Color = SvgExtensions.s_transparentBlack,
            ColorFilter = lumaColor
        };
    }

    protected bool HasFeatures(SvgElement svgElement, DrawAttributes ignoreAttributes)
    {
        var hasRequiredFeatures = ignoreAttributes.HasFlag(DrawAttributes.RequiredFeatures) || svgElement.HasRequiredFeatures();
        var hasRequiredExtensions = ignoreAttributes.HasFlag(DrawAttributes.RequiredExtensions) || svgElement.HasRequiredExtensions();
        var hasSystemLanguage = ignoreAttributes.HasFlag(DrawAttributes.SystemLanguage) || svgElement.HasSystemLanguage();
        return hasRequiredFeatures && hasRequiredExtensions && hasSystemLanguage;
    }

    protected bool CanDraw(SvgVisualElement svgVisualElement, DrawAttributes ignoreAttributes)
    {
        var isVisible = ignoreAttributes.HasFlag(DrawAttributes.Visibility) || string.Equals(svgVisualElement.Visibility, "visible", StringComparison.OrdinalIgnoreCase);
        var isDisplay = ignoreAttributes.HasFlag(DrawAttributes.Display) || !string.Equals(svgVisualElement.Display, "none", StringComparison.OrdinalIgnoreCase);
        return isVisible && isDisplay;
    }

    public abstract void OnDraw(SKCanvas canvas, DrawAttributes ignoreAttributes, DrawableBase? until);

    public virtual void Draw(SKCanvas canvas, DrawAttributes ignoreAttributes, DrawableBase? until, bool enableTransform)
    {
        if (!IsDrawable)
        {
            return;
        }

        if (until is { } && this == until)
        {
            return;
        }

        var enableClip = !ignoreAttributes.HasFlag(DrawAttributes.ClipPath);
        var enableMask = !ignoreAttributes.HasFlag(DrawAttributes.Mask);
        var enableOpacity = !ignoreAttributes.HasFlag(DrawAttributes.Opacity);
        var enableFilter = !ignoreAttributes.HasFlag(DrawAttributes.Filter);

        canvas.Save();

        if (Overflow is { })
        {
            canvas.ClipRect(Overflow.Value, SKClipOperation.Intersect);
        }

        if (!Transform.IsIdentity && enableTransform)
        {
            canvas.SetMatrix(Transform);
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

            // DEBUG: Filter Region - FilterClip
#if USE_DEBUG_DRAW_FILTER_BOUNDS
            if (FilterClip is not null)
            {
                Debug.WriteLine($"FilterClip {FilterClip}");
                {
                    var path = new SKPath();
                    path.AddRect(FilterClip.Value);
                    var clipPaint = new SKPaint
                    {
                        IsAntialias = true, Style = SKPaintStyle.Stroke, Color = new SKColor(255, 0, 0, 255)
                    };
                    canvas.DrawPath(path, clipPaint);
                }
            }
#endif
            // DEBUG: Filter Region - GeometryBounds
#if USE_DEBUG_DRAW_FILTER_BOUNDS
            if (FilterClip is not null)
            {
                Debug.WriteLine($"GeometryBounds {GeometryBounds}");
                {
                    var path = new SKPath();
                    path.AddRect(GeometryBounds);
                    var clipPaint = new SKPaint
                    {
                        IsAntialias = true, Style = SKPaintStyle.Stroke, Color = new SKColor(255, 0, 255, 255)
                    };
                    canvas.DrawPath(path, clipPaint);
                }
            }
#endif
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

    public virtual void PostProcess(SKRect? viewport, SKMatrix totalMatrix)
    {
        var element = Element;
        if (element is null)
        {
            return;
        }

        var visualElement = element as SvgVisualElement;

        var enableClip = !IgnoreAttributes.HasFlag(DrawAttributes.ClipPath);
        var enableMask = !IgnoreAttributes.HasFlag(DrawAttributes.Mask);
        var enableOpacity = !IgnoreAttributes.HasFlag(DrawAttributes.Opacity);
        var enableFilter = !IgnoreAttributes.HasFlag(DrawAttributes.Filter);

        TotalTransform = totalMatrix.PreConcat(Transform);
        TransformedBounds = TotalTransform.MapRect(GeometryBounds);

        if (visualElement is { } && enableClip)
        {
            var clipPath = new ClipPath
            {
                Clip = new ClipPath()
            };
            SvgExtensions.GetSvgVisualElementClipPath(visualElement, GeometryBounds, new HashSet<Uri>(), clipPath);
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
            MaskDrawable = SvgExtensions.GetSvgElementMask(element, GeometryBounds, new HashSet<Uri>(), AssetLoader, References);
            if (MaskDrawable is { })
            {
                CreateMaskPaints();
            }
        }
        else
        {
            MaskDrawable = null;
        }

        Opacity = enableOpacity ? SvgExtensions.GetOpacityPaint(element) : null;

        if (visualElement is { } && enableFilter)
        {
            var filterContext = new SvgFilterContext(visualElement, GeometryBounds, viewport ?? GeometryBounds, this, AssetLoader, References);
            Filter = filterContext.FilterPaint;
            FilterClip = filterContext.FilterClip;
            if (filterContext.IsValid == false)
            {
                IsDrawable = false;
            }
        }
        else
        {
            Filter = null;
        }
    }
#if USE_DEBUG_DRAW_BOUNDS
    public virtual void DebugDrawBounds(SKCanvas canvas)
    {
        Debug.WriteLine($"DebugDraw {this} {TransformedBounds}");
        var path = new SKPath();
        path.AddRect(TransformedBounds);
        var clipPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke, Color = new SKColor(0, 255, 255, 255)
        };
        canvas.DrawPath(path, clipPaint);
    }
#endif
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

    public SKPicture? RecordGraphic(DrawableBase? drawable, SKRect? clip, DrawAttributes ignoreAttributes)
    {
        if (drawable is null)
        {
            return null;
        }

        var skBounds = drawable.GeometryBounds;
        if (skBounds.Width <= 0f && skBounds.Height <= 0f)
        {
            return null;
        }

        var cullRect = clip ?? SKRect.Create(
            0, 
            0, 
            Math.Abs(skBounds.Left) + skBounds.Width, 
            Math.Abs(skBounds.Top) + skBounds.Height);
        var skPictureRecorder = new SKPictureRecorder();
        var skCanvas = skPictureRecorder.BeginRecording(cullRect);

        drawable.Draw(skCanvas, ignoreAttributes, null, false);

        return skPictureRecorder.EndRecording();
    }

    public SKPicture? RecordBackground(DrawableBase? drawable, SKRect? clip, DrawAttributes ignoreAttributes)
    {
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
        var cullRect = clip ?? SKRect.Create(
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

    private const DrawAttributes FilterBackgroundInput =
        DrawAttributes.ClipPath
        | DrawAttributes.Mask
        | DrawAttributes.Opacity
        | DrawAttributes.Filter;

    protected virtual void PostProcessChildren(SKRect? clip, SKMatrix totalMatrix)
    {
    }

    SKPicture? IFilterSource.SourceGraphic(SKRect? clip)
    {
        PostProcessChildren(clip, SKMatrix.Identity);
        return RecordGraphic(this, clip, DrawAttributes.None);
    }

    SKPicture? IFilterSource.BackgroundImage(SKRect? clip) => RecordBackground(this, clip, FilterBackgroundInput);

    SKPaint? IFilterSource.FillPaint() => Fill;

    SKPaint? IFilterSource.StrokePaint() => Stroke;
}
