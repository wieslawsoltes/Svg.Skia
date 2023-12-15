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
        SvgExtensions.AddMarkers(svgGroup);

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

        IsAntialias = SvgExtensions.IsAntialias(svgGroup);

        GeometryBounds = SKRect.Empty;

        CreateGeometryBounds();

        Transform = SvgExtensions.ToMatrix(svgGroup.Transforms);

        if (SvgExtensions.IsValidFill(svgGroup))
        {
            Fill = SvgExtensions.GetFillPaint(svgGroup, GeometryBounds, AssetLoader, references, IgnoreAttributes);
        }

        if (SvgExtensions.IsValidStroke(svgGroup, GeometryBounds))
        {
            Stroke = SvgExtensions.GetStrokePaint(svgGroup, GeometryBounds, AssetLoader, references, IgnoreAttributes);
        }
    }
}
