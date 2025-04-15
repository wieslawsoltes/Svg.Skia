using System;
using System.Collections.Generic;
using ShimSkiaSharp;

namespace Svg.Model.Drawables.Elements;

public sealed class CircleDrawable : DrawablePath
{
    private CircleDrawable(IAssetLoader assetLoader, HashSet<Uri>? references)
        : base(assetLoader, references)
    {
    }

    public static CircleDrawable Create(SvgCircle svgCircle, SKRect skViewport, DrawableBase? parent, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
    {
        var drawable = new CircleDrawable(assetLoader, references)
        {
            Element = svgCircle,
            Parent = parent,
            IgnoreAttributes = ignoreAttributes
        };

        drawable.IsDrawable = drawable.CanDraw(svgCircle, drawable.IgnoreAttributes) && drawable.HasFeatures(svgCircle, drawable.IgnoreAttributes);

        if (!drawable.IsDrawable)
        {
            return drawable;
        }

        drawable.Path = svgCircle.ToPath(svgCircle.FillRule, skViewport);
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
        if (Element is not SvgCircle svgCircle || Path is null)
        {
            return;
        }

        IsAntialias = PaintingService.IsAntialias(svgCircle);

        GeometryBounds = Path.Bounds;

        Transform = TransformsService.ToMatrix(svgCircle.Transforms);

        var canDrawFill = true;
        var canDrawStroke = true;

        if (PaintingService.IsValidFill(svgCircle))
        {
            Fill = PaintingService.GetFillPaint(svgCircle, GeometryBounds ,AssetLoader, references, IgnoreAttributes);
            if (Fill is null)
            {
                canDrawFill = false;
            }
        }

        if (PaintingService.IsValidStroke(svgCircle, GeometryBounds))
        {
            Stroke = PaintingService.GetStrokePaint(svgCircle, GeometryBounds, AssetLoader, references, IgnoreAttributes);
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
