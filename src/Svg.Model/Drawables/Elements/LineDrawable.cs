// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg.Model.Services;

namespace Svg.Model.Drawables.Elements;

public sealed class LineDrawable : DrawablePath
{
    private LineDrawable(ISvgAssetLoader assetLoader, HashSet<Uri>? references)
        : base(assetLoader, references)
    {
    }

    public static LineDrawable Create(SvgLine svgLine, SKRect skViewport, DrawableBase? parent, ISvgAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
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

    private void Initialize(SKRect skViewport, HashSet<Uri>? references)
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

    private static float DistanceToSegment(SKPoint p, SKPoint a, SKPoint b)
    {
        var vx = b.X - a.X;
        var vy = b.Y - a.Y;
        var ux = p.X - a.X;
        var uy = p.Y - a.Y;
        var lenSq = vx * vx + vy * vy;
        if (lenSq <= float.Epsilon)
        {
            return (float)Math.Sqrt(ux * ux + uy * uy);
        }

        var t = (ux * vx + uy * vy) / lenSq;
        if (t < 0f)
            t = 0f;
        else if (t > 1f)
            t = 1f;

        var px = a.X + t * vx;
        var py = a.Y + t * vy;
        var dx = p.X - px;
        var dy = p.Y - py;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    public override bool HitTest(SKPoint point)
    {
        if (Path?.Commands is { Count: >= 2 } commands &&
            commands[0] is MoveToPathCommand move &&
            commands[1] is LineToPathCommand line)
        {
            var start = TotalTransform.MapPoint(new SKPoint(move.X, move.Y));
            var end = TotalTransform.MapPoint(new SKPoint(line.X, line.Y));
            var distance = DistanceToSegment(point, start, end);
            var tolerance = Stroke?.StrokeWidth / 2f ?? 1f;
            if (tolerance <= 0f)
                tolerance = 1f;
            return distance <= tolerance;
        }

        return base.HitTest(point);
    }

    public override SKDrawable Clone()
    {
        var clone = new LineDrawable(AssetLoader, CloneReferences(References));
        CopyTo(clone, Parent);
        return clone;
    }
}
