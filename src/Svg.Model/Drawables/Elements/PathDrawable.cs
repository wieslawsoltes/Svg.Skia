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

public sealed class PathDrawable : DrawablePath
{
    private PathDrawable(IAssetLoader assetLoader, HashSet<Uri>? references)
        : base(assetLoader, references)
    {
    }

    public static PathDrawable Create(SvgPath svgPath, SKRect skViewport, DrawableBase? parent, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
    {
        var drawable = new PathDrawable(assetLoader, references)
        {
            Element = svgPath,
            Parent = parent,
            IgnoreAttributes = ignoreAttributes
        };

        drawable.IsDrawable = drawable.CanDraw(svgPath, drawable.IgnoreAttributes) && drawable.HasFeatures(svgPath, drawable.IgnoreAttributes);

        if (!drawable.IsDrawable)
        {
            return drawable;
        }

        drawable.Path = svgPath.PathData?.ToPath(svgPath.FillRule);
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
        if (Element is not SvgPath svgPath || Path is null)
        {
            return;
        }

        IsAntialias = SvgExtensions.IsAntialias(svgPath);

        GeometryBounds = Path.Bounds;

        Transform = SvgExtensions.ToMatrix(svgPath.Transforms);

        var canDrawFill = true;
        var canDrawStroke = true;

        if (SvgExtensions.IsValidFill(svgPath))
        {
            Fill = SvgExtensions.GetFillPaint(svgPath, GeometryBounds, AssetLoader, references, IgnoreAttributes);
            if (Fill is null)
            {
                canDrawFill = false;
            }
        }

        if (SvgExtensions.IsValidStroke(svgPath, GeometryBounds))
        {
            Stroke = SvgExtensions.GetStrokePaint(svgPath, GeometryBounds, AssetLoader, references, IgnoreAttributes);
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
        
        SvgExtensions.CreateMarkers(svgPath, Path, skViewport, this, AssetLoader, references);
    }
}
