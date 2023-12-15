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
using Svg.Model.Drawables.Elements;
using ShimSkiaSharp;

namespace Svg.Model.Drawables;

public static class DrawableFactory
{
    public static DrawableBase? Create(SvgElement svgElement, SKRect skViewport, DrawableBase? parent, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
    {
        return svgElement switch
        {
            SvgAnchor svgAnchor => AnchorDrawable.Create(svgAnchor, skViewport, parent, assetLoader, references, ignoreAttributes),
            SvgFragment svgFragment => FragmentDrawable.Create(svgFragment, skViewport, parent, assetLoader, references, ignoreAttributes),
            SvgImage svgImage => ImageDrawable.Create(svgImage, skViewport, parent, assetLoader, references, ignoreAttributes),
            SvgSwitch svgSwitch => SwitchDrawable.Create(svgSwitch, skViewport, parent, assetLoader, references, ignoreAttributes),
            SvgUse svgUse => UseDrawable.Create(svgUse, skViewport, parent, assetLoader, references, ignoreAttributes),
            SvgCircle svgCircle => CircleDrawable.Create(svgCircle, skViewport, parent, assetLoader, references, ignoreAttributes),
            SvgEllipse svgEllipse => EllipseDrawable.Create(svgEllipse, skViewport, parent, assetLoader, references, ignoreAttributes),
            SvgRectangle svgRectangle => RectangleDrawable.Create(svgRectangle, skViewport, parent, assetLoader, references, ignoreAttributes),
            SvgGroup svgGroup => GroupDrawable.Create(svgGroup, skViewport, parent, assetLoader, references, ignoreAttributes),
            SvgLine svgLine => LineDrawable.Create(svgLine, skViewport, parent, assetLoader, references, ignoreAttributes),
            SvgPath svgPath => PathDrawable.Create(svgPath, skViewport, parent, assetLoader, references, ignoreAttributes),
            SvgPolyline svgPolyline => PolylineDrawable.Create(svgPolyline, skViewport, parent, assetLoader, references, ignoreAttributes),
            SvgPolygon svgPolygon => PolygonDrawable.Create(svgPolygon, skViewport, parent, assetLoader, references, ignoreAttributes),
            SvgText svgText => TextDrawable.Create(svgText, skViewport, parent, assetLoader, references, ignoreAttributes),
            _ => null,
        };
    }
}
