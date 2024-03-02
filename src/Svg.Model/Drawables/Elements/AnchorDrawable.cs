using System;
using System.Collections.Generic;
using ShimSkiaSharp;

namespace Svg.Model.Drawables.Elements;

public sealed class AnchorDrawable : DrawableContainer
{
    private AnchorDrawable(IAssetLoader assetLoader, HashSet<Uri>? references)
        : base(assetLoader, references)
    {
    }

    public static AnchorDrawable Create(SvgAnchor svgAnchor, SKRect skViewport, DrawableBase? parent, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
    {
        var drawable = new AnchorDrawable(assetLoader, references)
        {
            Element = svgAnchor,
            Parent = parent,
            IgnoreAttributes = ignoreAttributes,
            IsDrawable = true
        };

        drawable.CreateChildren(svgAnchor, skViewport, drawable, assetLoader, references, ignoreAttributes);

        drawable.Initialize();

        return drawable;
    }

    private void Initialize()
    {
        if (Element is not SvgAnchor svgAnchor)
        {
            return;;
        }

        IsAntialias = SvgExtensions.IsAntialias(svgAnchor);

        GeometryBounds = SKRect.Empty;

        CreateGeometryBounds();

        Transform = svgAnchor.Transforms.ToMatrix();

        Fill = null;
        Stroke = null;

        ClipPath = null;
        MaskDrawable = null;
        Opacity = IgnoreAttributes.HasFlag(DrawAttributes.Opacity)
            ? null
            : SvgExtensions.GetOpacityPaint(svgAnchor);
        Filter = null;
    }

    public override void PostProcess(SKRect? viewport, SKMatrix totalMatrix)
    {
        var element = Element;
        if (element is null)
        {
            return;
        }

        var enableOpacity = !IgnoreAttributes.HasFlag(DrawAttributes.Opacity);

        ClipPath = null;
        MaskDrawable = null;
        Opacity = enableOpacity ? SvgExtensions.GetOpacityPaint(element) : null;
        Filter = null;

        TotalTransform = totalMatrix.PreConcat(Transform);
        TransformedBounds = TotalTransform.MapRect(GeometryBounds);

        foreach (var child in ChildrenDrawables)
        {
            child.PostProcess(viewport, totalMatrix);
        }
    }
}
