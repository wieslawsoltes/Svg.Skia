﻿using System;
using System.Collections.Generic;
using ShimSkiaSharp;

namespace Svg.Model.Drawables.Elements;

public sealed class GroupDrawable : DrawableContainer
{
    private GroupDrawable(IAssetLoader assetLoader, HashSet<Uri>? references)
        : base(assetLoader, references)
    {
    }

    public static GroupDrawable Create(SvgGroup svgGroup, SKRect skViewport, DrawableBase? parent, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
    {
        var drawable = new GroupDrawable(assetLoader, references)
        {
            Element = svgGroup,
            Parent = parent,
            IgnoreAttributes = ignoreAttributes
        };

        drawable.IsDrawable = drawable.CanDraw(svgGroup, drawable.IgnoreAttributes) && drawable.HasFeatures(svgGroup, drawable.IgnoreAttributes);

        // NOTE: Call AddMarkers only once.
        svgGroup.AddMarkers();

        drawable.CreateChildren(svgGroup, skViewport, drawable, assetLoader, references, ignoreAttributes);

        // TODO: Check if children are explicitly set to be visible.
        //foreach (var child in drawable.ChildrenDrawables)
        //{
        //    if (child.IsDrawable)
        //    {
        //        IsDrawable = true;
        //        break;
        //    }
        //}

        if (!drawable.IsDrawable)
        {
            return drawable;
        }

        drawable.Initialize(references);

        return drawable;
    }

    private void Initialize(HashSet<Uri>? references)
    {
        if (Element is not SvgGroup svgGroup)
        {
            return;;
        }

        IsAntialias = SvgExtensions.IsAntialias(svgGroup);

        GeometryBounds = SKRect.Empty;

        CreateGeometryBounds();

        Transform = svgGroup.Transforms.ToMatrix();

        if (SvgExtensions.IsValidFill(svgGroup))
        {
            Fill = SvgExtensions.GetFillPaint(svgGroup, GeometryBounds, AssetLoader, references, IgnoreAttributes);
        }

        if (SvgExtensions.IsValidStroke(svgGroup, GeometryBounds))
        {
            Stroke = SvgExtensions.GetStrokePaint(svgGroup, GeometryBounds, AssetLoader, references, IgnoreAttributes);
        }
    }
}
