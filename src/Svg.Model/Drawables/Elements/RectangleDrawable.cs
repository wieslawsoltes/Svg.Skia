// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg.Model.Services;

namespace Svg.Model.Drawables.Elements;

public sealed class RectangleDrawable : DrawablePath
{
    private RectangleDrawable(ISvgAssetLoader assetLoader, HashSet<Uri>? references)
        : base(assetLoader, references)
    {
    }

    public static RectangleDrawable Create(SvgRectangle svgRectangle, SKRect skViewport, DrawableBase? parent, ISvgAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
    {
        var drawable = new RectangleDrawable(assetLoader, references)
        {
            Element = svgRectangle,
            Parent = parent,
            IgnoreAttributes = ignoreAttributes
        };

        drawable.IsDrawable = drawable.CanDraw(svgRectangle, drawable.IgnoreAttributes) && drawable.HasFeatures(svgRectangle, drawable.IgnoreAttributes);

        if (!drawable.IsDrawable)
        {
            return drawable;
        }

        drawable.Path = svgRectangle.ToPath(svgRectangle.FillRule, skViewport);
        if (drawable.Path is null || drawable.Path.IsEmpty)
        {
            drawable.IsDrawable = false;
            return drawable;
        }

        drawable.Initialize(references);

        return drawable;
    }

    private void Initialize(HashSet<Uri>? references)
    {
        if (Element is not SvgRectangle svgRectangle || Path is null)
        {
            return;
        }

        IsAntialias = PaintingService.IsAntialias(svgRectangle);

        GeometryBounds = Path.Bounds;

        Transform = TransformsService.ToMatrix(svgRectangle.Transforms);

        var canDrawFill = true;
        var canDrawStroke = true;

        if (PaintingService.IsValidFill(svgRectangle))
        {
            Fill = PaintingService.GetFillPaint(svgRectangle, GeometryBounds, AssetLoader, references, IgnoreAttributes);
            if (Fill is null)
            {
                canDrawFill = false;
            }
        }

        if (PaintingService.IsValidStroke(svgRectangle, GeometryBounds))
        {
            Stroke = PaintingService.GetStrokePaint(svgRectangle, GeometryBounds, AssetLoader, references, IgnoreAttributes);
            if (Stroke is null)
            {
                canDrawStroke = false;
            }
        }

        if (canDrawFill && !canDrawStroke)
        {
            IsDrawable = false;
        }
    }
}
