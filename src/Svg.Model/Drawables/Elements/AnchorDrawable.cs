// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg.Model.Services;

namespace Svg.Model.Drawables.Elements;

public sealed class AnchorDrawable : DrawableContainer
{
    private AnchorDrawable(ISvgAssetLoader assetLoader, HashSet<Uri>? references)
        : base(assetLoader, references)
    {
    }

    public static AnchorDrawable Create(SvgAnchor svgAnchor, SKRect skViewport, DrawableBase? parent, ISvgAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
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

        IsAntialias = PaintingService.IsAntialias(svgAnchor);

        GeometryBounds = SKRect.Empty;

        CreateGeometryBounds();

        Transform = TransformsService.ToMatrix(svgAnchor.Transforms);

        Fill = null;
        Stroke = null;

        ClipPath = null;
        MaskDrawable = null;
        Opacity = IgnoreAttributes.HasFlag(DrawAttributes.Opacity)
            ? null
            : PaintingService.GetOpacityPaint(svgAnchor);
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
        Opacity = enableOpacity ? PaintingService.GetOpacityPaint(element) : null;
        Filter = null;

        TotalTransform = totalMatrix.PreConcat(Transform);
        TransformedBounds = TotalTransform.MapRect(GeometryBounds);

        foreach (var child in ChildrenDrawables)
        {
            child.PostProcess(viewport, totalMatrix);
        }
    }
}
