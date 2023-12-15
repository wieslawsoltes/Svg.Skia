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

public sealed class AnchorDrawable : DrawableContainer
{
    private AnchorDrawable(IAssetLoader assetLoader, HashSet<Uri>? references)
        : base(assetLoader, references)
    {
    }

    public static AnchorDrawable Create(SvgAnchor svgAnchor, SKRect skViewport, DrawableBase? parent, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
    {
        var drawable = new AnchorDrawable(assetLoader, references)
        {
            Element = svgAnchor,
            Parent = parent,
            IgnoreAttributes = ignoreAttributes,
            IsDrawable = true
        };

        drawable.CreateChildren(svgAnchor, skViewport, drawable, assetLoader, references, ignoreAttributes);

        drawable.Initialize();

        return drawable;
    }

    private void Initialize()
    {
        if (Element is not SvgAnchor svgAnchor)
        {
            return;;
        }

        IsAntialias = SvgExtensions.IsAntialias(svgAnchor);

        GeometryBounds = SKRect.Empty;

        CreateGeometryBounds();

        Transform = SvgExtensions.ToMatrix(svgAnchor.Transforms);

        Fill = null;
        Stroke = null;

        ClipPath = null;
        MaskDrawable = null;
        Opacity = IgnoreAttributes.HasFlag(DrawAttributes.Opacity)
            ? null
            : SvgExtensions.GetOpacityPaint(svgAnchor);
        Filter = null;
    }

    public override void PostProcess(SKRect? viewport, SKMatrix totalMatrix)
    {
        var element = Element;
        if (element is null)
        {
            return;
        }

        var enableOpacity = !IgnoreAttributes.HasFlag(DrawAttributes.Opacity);

        ClipPath = null;
        MaskDrawable = null;
        Opacity = enableOpacity ? SvgExtensions.GetOpacityPaint(element) : null;
        Filter = null;

        TotalTransform = totalMatrix.PreConcat(Transform);
        TransformedBounds = TotalTransform.MapRect(GeometryBounds);

        foreach (var child in ChildrenDrawables)
        {
            child.PostProcess(viewport, totalMatrix);
        }
    }
}
