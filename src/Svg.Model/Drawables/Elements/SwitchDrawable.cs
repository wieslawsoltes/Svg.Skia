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

public sealed class SwitchDrawable : DrawableBase
{
    public DrawableBase? FirstChild { get; set; }

    private SwitchDrawable(IAssetLoader assetLoader, HashSet<Uri>? references)
        : base(assetLoader, references)
    {
    }

    public static SwitchDrawable Create(SvgSwitch svgSwitch, SKRect skViewport, DrawableBase? parent, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
    {
        var drawable = new SwitchDrawable(assetLoader, references)
        {
            Element = svgSwitch,
            Parent = parent,

            IgnoreAttributes = ignoreAttributes
        };
        drawable.IsDrawable = drawable.CanDraw(svgSwitch, drawable.IgnoreAttributes) && drawable.HasFeatures(svgSwitch, drawable.IgnoreAttributes);

        if (!drawable.IsDrawable)
        {
            return drawable;
        }

        foreach (var child in svgSwitch.Children)
        {
            if (!child.IsKnownElement())
            {
                continue;
            }

            var hasRequiredFeatures = child.HasRequiredFeatures();
            var hasRequiredExtensions = child.HasRequiredExtensions();
            var hasSystemLanguage = child.HasSystemLanguage();

            if (hasRequiredFeatures && hasRequiredExtensions && hasSystemLanguage)
            {
                var childDrawable = DrawableFactory.Create(child, skViewport, parent, assetLoader, references, ignoreAttributes);
                if (childDrawable is { })
                {
                    drawable.FirstChild = childDrawable;
                }
                break;
            }
        }

        if (drawable.FirstChild is null)
        {
            drawable.IsDrawable = false;
            return drawable;
        }

        drawable.Initialize();
        
        return drawable;
    }

    private void Initialize()
    {
        if (Element is not SvgSwitch svgSwitch || FirstChild is null)
        {
            return;
        }
        
        IsAntialias = SvgExtensions.IsAntialias(svgSwitch);

        // TODO: use drawable.FirstChild.GeometryBounds
        GeometryBounds = FirstChild.GeometryBounds;

        Transform = SvgExtensions.ToMatrix(svgSwitch.Transforms);

        Fill = null;
        Stroke = null;
    }
    
    public override void OnDraw(SKCanvas canvas, DrawAttributes ignoreAttributes, DrawableBase? until)
    {
        if (until is { } && this == until)
        {
            return;
        }

        FirstChild?.Draw(canvas, ignoreAttributes, until, true);
    }

    public override void PostProcess(SKRect? viewport, SKMatrix totalMatrix)
    {
        base.PostProcess(viewport, totalMatrix);

        FirstChild?.PostProcess(viewport, TotalTransform);
    }
}
