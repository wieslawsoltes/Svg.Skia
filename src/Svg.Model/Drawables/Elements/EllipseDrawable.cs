using System;
using System.Collections.Generic;
using ShimSkiaSharp;

namespace Svg.Model.Drawables.Elements;

public sealed class EllipseDrawable : DrawablePath
{
    private EllipseDrawable(IAssetLoader assetLoader, HashSet<Uri>? references)
        : base(assetLoader, references)
    {
    }

    public static EllipseDrawable Create(SvgEllipse svgEllipse, SKRect skViewport, DrawableBase? parent, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
    {
        var drawable = new EllipseDrawable(assetLoader, references)
        {
            Element = svgEllipse,
            Parent = parent,
            IgnoreAttributes = ignoreAttributes
        };

        drawable.IsDrawable = drawable.CanDraw(svgEllipse, drawable.IgnoreAttributes) && drawable.HasFeatures(svgEllipse, drawable.IgnoreAttributes);

        if (!drawable.IsDrawable)
        {
            return drawable;
        }

        drawable.Path = svgEllipse.ToPath(svgEllipse.FillRule, skViewport);
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
        if (Element is not SvgEllipse svgEllipse || Path is null)
        {
            return;
        }
        
        IsAntialias = PaintingService.IsAntialias(svgEllipse);
        Transform = TransformsService.ToMatrix(svgEllipse.Transforms);

        GeometryBounds = Path.Bounds;

        var canDrawFill = true;
        var canDrawStroke = true;

        if (PaintingService.IsValidFill(svgEllipse))
        {
            Fill = PaintingService.GetFillPaint(svgEllipse, GeometryBounds, AssetLoader, references, IgnoreAttributes);
            if (Fill is null)
            {
                canDrawFill = false;
            }
        }

        if (PaintingService.IsValidStroke(svgEllipse, GeometryBounds))
        {
            Stroke = PaintingService.GetStrokePaint(svgEllipse, GeometryBounds, AssetLoader, references, IgnoreAttributes);
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
