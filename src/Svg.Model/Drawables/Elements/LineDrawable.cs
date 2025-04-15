using System;
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg.Model.Services;

namespace Svg.Model.Drawables.Elements;

public sealed class LineDrawable : DrawablePath
{
    private LineDrawable(IAssetLoader assetLoader, HashSet<Uri>? references)
        : base(assetLoader, references)
    {
    }

    public static LineDrawable Create(SvgLine svgLine, SKRect skViewport, DrawableBase? parent, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
    {
        var drawable = new LineDrawable(assetLoader, references)
        {
            Element = svgLine,
            Parent = parent,
            IgnoreAttributes = ignoreAttributes
        };

        drawable.IsDrawable = drawable.CanDraw(svgLine, drawable.IgnoreAttributes) && drawable.HasFeatures(svgLine, drawable.IgnoreAttributes);

        if (!drawable.IsDrawable)
        {
            return drawable;
        }

        drawable.Path = svgLine.ToPath(svgLine.FillRule, skViewport);
        if (drawable.Path is null || drawable.Path.IsEmpty)
        {
            drawable.IsDrawable = false;
            return drawable;
        }

        drawable.Initialize(skViewport, references);

        return drawable;
    }

    private void Initialize(SKRect skViewport,HashSet<Uri>? references)
    {
        if (Element is not SvgLine svgLine || Path is null)
        {
            return;
        }
        
        IsAntialias = PaintingService.IsAntialias(svgLine);

        GeometryBounds = Path.Bounds;

        Transform = TransformsService.ToMatrix(svgLine.Transforms);

        var canDrawFill = true;
        var canDrawStroke = true;

        if (PaintingService.IsValidFill(svgLine))
        {
            Fill = PaintingService.GetFillPaint(svgLine, GeometryBounds, AssetLoader, references, IgnoreAttributes);
            if (Fill is null)
            {
                canDrawFill = false;
            }
        }

        if (PaintingService.IsValidStroke(svgLine, GeometryBounds))
        {
            Stroke = PaintingService.GetStrokePaint(svgLine, GeometryBounds, AssetLoader, references, IgnoreAttributes);
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

        MarkerService.CreateMarkers(svgLine, Path, skViewport, this, AssetLoader, references);
    }
}
