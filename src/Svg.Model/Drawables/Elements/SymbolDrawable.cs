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

public sealed class SymbolDrawable : DrawableContainer
{
    private SymbolDrawable(IAssetLoader assetLoader, HashSet<Uri>? references)
        : base(assetLoader, references)
    {
    }

    public static SymbolDrawable Create(SvgSymbol svgSymbol, float x, float y, float width, float height, SKRect skViewport, DrawableBase? parent, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes)
    {
        var drawable = new SymbolDrawable(assetLoader, references)
        {
            Element = svgSymbol,
            Parent = parent,
            IgnoreAttributes = ignoreAttributes
        };

        drawable.IsDrawable = drawable.CanDraw(svgSymbol, drawable.IgnoreAttributes) && drawable.HasFeatures(svgSymbol, drawable.IgnoreAttributes);

        if (!drawable.IsDrawable)
        {
            return drawable;
        }

        if (svgSymbol.CustomAttributes.TryGetValue("width", out var _widthString))
        {
            if (new SvgUnitConverter().ConvertFromString(_widthString) is SvgUnit _width)
            {
                width = _width.ToDeviceValue(UnitRenderingType.Horizontal, svgSymbol, skViewport);
            }
        }

        if (svgSymbol.CustomAttributes.TryGetValue("height", out var heightString))
        {
            if (new SvgUnitConverter().ConvertFromString(heightString) is SvgUnit _height)
            {
                height = _height.ToDeviceValue(UnitRenderingType.Vertical, svgSymbol, skViewport);
            }
        }

        var svgOverflow = SvgOverflow.Hidden;
        if (svgSymbol.TryGetAttribute("overflow", out string overflowString))
        {
            if (new SvgOverflowConverter().ConvertFromString(overflowString) is SvgOverflow _svgOverflow)
            {
                svgOverflow = _svgOverflow;
            }
        }

        switch (svgOverflow)
        {
            case SvgOverflow.Auto:
            case SvgOverflow.Visible:
            case SvgOverflow.Inherit:
                break;

            default:
                drawable.Overflow = SKRect.Create(x, y, width, height);
                break;
        }

        drawable.CreateChildren(svgSymbol, skViewport, drawable, assetLoader, references, ignoreAttributes);

        drawable.Initialize(x, y, width, height);
        
        return drawable;
    }

    private void Initialize(float x, float y, float width, float height)
    {
        if (Element is not SvgSymbol svgSymbol)
        {
            return;
        }

        IsAntialias = SvgExtensions.IsAntialias(svgSymbol);

        GeometryBounds = SKRect.Empty;

        CreateGeometryBounds();

        Transform = SvgExtensions.ToMatrix(svgSymbol.Transforms);
        var skMatrixViewBox = SvgExtensions.ToMatrix(svgSymbol.ViewBox, svgSymbol.AspectRatio, x, y, width, height);
        Transform = Transform.PreConcat(skMatrixViewBox);

        Fill = null;
        Stroke = null;
    }
}
