using System;
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
        MarkerService.AddMarkers(svgGroup);

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

        IsAntialias = PaintingService.IsAntialias(svgGroup);

        GeometryBounds = SKRect.Empty;

        CreateGeometryBounds();

        Transform = TransformsService.ToMatrix(svgGroup.Transforms);

        if (PaintingService.IsValidFill(svgGroup))
        {
            Fill = PaintingService.GetFillPaint(svgGroup, GeometryBounds, AssetLoader, references, IgnoreAttributes);
        }

        if (PaintingService.IsValidStroke(svgGroup, GeometryBounds))
        {
            Stroke = PaintingService.GetStrokePaint(svgGroup, GeometryBounds, AssetLoader, references, IgnoreAttributes);
        }
    }
}
