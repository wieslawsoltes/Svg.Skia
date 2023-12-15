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

public abstract class DrawablePath : DrawableBase, IMarkerHost
{
    public SKPath? Path { get; set; }
    public List<DrawableBase>? MarkerDrawables { get; set; }

    protected DrawablePath(IAssetLoader assetLoader, HashSet<Uri>? references)
        : base(assetLoader, references)
    {
    }

    void IMarkerHost.AddMarker(DrawableBase drawable)
    {
        MarkerDrawables ??= new List<DrawableBase>();
        MarkerDrawables.Add(drawable);
    }

    public override void OnDraw(SKCanvas canvas, DrawAttributes ignoreAttributes, DrawableBase? until)
    {
        if (until is { } && this == until)
        {
            return;
        }

        if (Fill is { } && Path is { })
        {
            canvas.DrawPath(Path, Fill);
        }

        if (Stroke is { } && Path is { })
        {
            canvas.DrawPath(Path, Stroke);
        }

        if (MarkerDrawables is { })
        {
            foreach (var drawable in MarkerDrawables)
            {
                drawable.Draw(canvas, ignoreAttributes, until, true);
            }
        }
    }

    public override void PostProcess(SKRect? viewport, SKMatrix totalMatrix)
    {
        base.PostProcess(viewport, totalMatrix);

        if (MarkerDrawables is { })
        {
            foreach (var drawable in MarkerDrawables)
            {
                drawable.PostProcess(viewport, TotalTransform);
            }
        }
    }
}
