using System;
using System.Collections.Generic;
#if USE_SKIASHARP
using SkiaSharp;
#else
using ShimSkiaSharp;
#endif

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
        
        IsAntialias = SvgExtensions.IsAntialias(svgPolygon);

        GeometryBounds = Path.Bounds;

        Transform = SvgExtensions.ToMatrix(svgPolygon.Transforms);

        var canDrawFill = true;
        var canDrawStroke = true;

        if (SvgExtensions.IsValidFill(svgPolygon))
        {
            Fill = SvgExtensions.GetFillPaint(svgPolygon, GeometryBounds, AssetLoader, references, IgnoreAttributes);
            if (Fill is null)
            {
                canDrawFill = false;
            }
        }

        if (SvgExtensions.IsValidStroke(svgPolygon, GeometryBounds))
        {
            Stroke = SvgExtensions.GetStrokePaint(svgPolygon, GeometryBounds, AssetLoader, references, IgnoreAttributes);
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

        SvgExtensions.CreateMarkers(svgPolygon, Path, skViewport, this, AssetLoader, references);
    }
}
