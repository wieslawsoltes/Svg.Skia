// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using SkiaSharp;

namespace Svg.Skia
{
    public static class DrawableFactory
    {
        public static Drawable? Create(SvgElement svgElement, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
        {
            return svgElement switch
            {
                SvgAnchor svgAnchor => new AnchorDrawable(svgAnchor, skOwnerBounds, root, parent, ignoreAttributes),
                SvgFragment svgFragment => new FragmentDrawable(svgFragment, skOwnerBounds, root, parent, ignoreAttributes),
                SvgImage svgImage => new ImageDrawable(svgImage, skOwnerBounds, root, parent, ignoreAttributes),
                SvgSwitch svgSwitch => new SwitchDrawable(svgSwitch, skOwnerBounds, root, parent, ignoreAttributes),
                SvgUse svgUse => new UseDrawable(svgUse, skOwnerBounds, root, parent, ignoreAttributes),
                SvgCircle svgCircle => new CircleDrawable(svgCircle, skOwnerBounds, root, parent, ignoreAttributes),
                SvgEllipse svgEllipse => new EllipseDrawable(svgEllipse, skOwnerBounds, root, parent, ignoreAttributes),
                SvgRectangle svgRectangle => new RectangleDrawable(svgRectangle, skOwnerBounds, root, parent, ignoreAttributes),
                SvgGroup svgGroup => new GroupDrawable(svgGroup, skOwnerBounds, root, parent, ignoreAttributes),
                SvgLine svgLine => new LineDrawable(svgLine, skOwnerBounds, root, parent, ignoreAttributes),
                SvgPath svgPath => new PathDrawable(svgPath, skOwnerBounds, root, parent, ignoreAttributes),
                SvgPolyline svgPolyline => new PolylineDrawable(svgPolyline, skOwnerBounds, root, parent, ignoreAttributes),
                SvgPolygon svgPolygon => new PolygonDrawable(svgPolygon, skOwnerBounds, root, parent, ignoreAttributes),
                SvgText svgText => new TextDrawable(svgText, skOwnerBounds, root, parent, ignoreAttributes),
                _ => null,
            };
        }
    }
}
