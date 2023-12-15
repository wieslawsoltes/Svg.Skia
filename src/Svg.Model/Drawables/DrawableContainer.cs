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

namespace Svg.Model.Drawables;

public abstract class DrawableContainer : DrawableBase
{
    public List<DrawableBase> ChildrenDrawables { get; }

    protected DrawableContainer(IAssetLoader assetLoader, HashSet<Uri>? references)
        : base(assetLoader, references)
    {
        ChildrenDrawables = new List<DrawableBase>();
    }

    protected void CreateChildren(SvgElement svgElement, SKRect skViewport, DrawableBase? parent, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes)
    {
        foreach (var child in svgElement.Children)
        {
            var drawable = DrawableFactory.Create(child, skViewport, parent, assetLoader, references, ignoreAttributes);
            if (drawable is { })
            {
                ChildrenDrawables.Add(drawable);
            }
        }
    }

    protected void CreateGeometryBounds()
    {
        foreach (var drawable in ChildrenDrawables)
        {
            if (GeometryBounds.IsEmpty)
            {
                GeometryBounds = drawable.GeometryBounds;
            }
            else
            {
                if (!drawable.GeometryBounds.IsEmpty)
                {
                    GeometryBounds = SKRect.Union(GeometryBounds, drawable.GeometryBounds);
                }
            }
        }
    }

    public override void OnDraw(SKCanvas canvas, DrawAttributes ignoreAttributes, DrawableBase? until)
    {
        if (until is { } && this == until)
        {
            return;
        }

        foreach (var drawable in ChildrenDrawables)
        {
            if (until is { } && drawable == until)
            {
                break;
            }
            drawable.Draw(canvas, ignoreAttributes, until, true);
        }
    }

    public override void PostProcess(SKRect? viewport, SKMatrix totalMatrix)
    {
        base.PostProcess(viewport, totalMatrix);

        PostProcessChildren(viewport, TotalTransform);
    }

    protected override void PostProcessChildren(SKRect? clip, SKMatrix totalMatrix)
    {
        base.PostProcessChildren(clip, totalMatrix);

        foreach (var child in ChildrenDrawables)
        {
            child.PostProcess(clip, SKMatrix.Identity);
        }
    }

#if USE_DEBUG_DRAW_BOUNDS
    public override void DebugDrawBounds(SKCanvas canvas)
    {
        base.DebugDrawBounds(canvas);

        foreach (var child in ChildrenDrawables)
        {
            child.DebugDrawBounds(canvas);
        }
    }
#endif
}
