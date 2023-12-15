/*
 * Svg.Skia SVG rendering library.
 * Copyright (C) 2023  Wiesław Šoltés
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as
 * published by the Free Software Foundation, either version 3 of the
 * License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
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
