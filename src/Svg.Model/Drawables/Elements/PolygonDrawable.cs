using System;
using System.Collections.Generic;
using ShimSkiaSharp;

namespace Svg.Model.Drawables.Elements;

public sealed class PolygonDrawable : DrawablePath
{
    private PolygonDrawable(IAssetLoader assetLoader, HashSet<Uri>? references)
        : base(assetLoader, references)
    {
    }

    public static PolygonDrawable Create(SvgPolygon svgPolygon, SKRect skViewport, DrawableBase? parent, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
    {
        var drawable = new PolygonDrawable(assetLoader, references)
        {
            Element = svgPolygon,
            Parent = parent,
            IgnoreAttributes = ignoreAttributes
        };

        drawable.IsDrawable = drawable.CanDraw(svgPolygon, drawable.IgnoreAttributes) && drawable.HasFeatures(svgPolygon, drawable.IgnoreAttributes);

        if (!drawable.IsDrawable)
        {
            return drawable;
        }

        drawable.Path = svgPolygon.Points?.ToPath(svgPolygon.FillRule, true, skViewport);
        if (drawable.Path is null || drawable.Path.IsEmpty)
        {
            drawable.IsDrawable = false;
            return drawable;
        }

        drawable.Initialize(skViewport, references);

        return drawable;
    }

    private void Initialize(SKRect skViewport, HashSet<Uri>? references)
    {
        if (Element is not SvgPolygon svgPolygon || Path is null)
        {
            return;
        }
        
        IsAntialias = PaintingService.IsAntialias(svgPolygon);

        GeometryBounds = Path.Bounds;

        Transform = TransformsService.ToMatrix(svgPolygon.Transforms);

        var canDrawFill = true;
        var canDrawStroke = true;

        if (PaintingService.IsValidFill(svgPolygon))
        {
            Fill = PaintingService.GetFillPaint(svgPolygon, GeometryBounds, AssetLoader, references, IgnoreAttributes);
            if (Fill is null)
            {
                canDrawFill = false;
            }
        }

        if (PaintingService.IsValidStroke(svgPolygon, GeometryBounds))
        {
            Stroke = PaintingService.GetStrokePaint(svgPolygon, GeometryBounds, AssetLoader, references, IgnoreAttributes);
            if (Stroke is null)
            {
                canDrawStroke = false;
            }
        }

        if (canDrawFill && !canDrawStroke)
        {
            IsDrawable = false;
            return;
        }

        MarkerService.CreateMarkers(svgPolygon, Path, skViewport, this, AssetLoader, references);
    }
}
