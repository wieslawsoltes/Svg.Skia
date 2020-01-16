// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using SkiaSharp;

namespace Svg.Skia
{
    public static class DrawableFactory
    {
        public static Drawable? Create(SvgElement svgElement, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes = IgnoreAttributes.None)
        {
            return svgElement switch
            {
                SvgAnchor svgAnchor => new AnchorDrawable(svgAnchor, skOwnerBounds, ignoreAttributes),
                SvgFragment svgFragment => new FragmentDrawable(svgFragment, skOwnerBounds, ignoreAttributes),
                SvgImage svgImage => new ImageDrawable(svgImage, skOwnerBounds, ignoreAttributes),
                SvgSwitch svgSwitch => new SwitchDrawable(svgSwitch, skOwnerBounds, ignoreAttributes),
                SvgUse svgUse => new UseDrawable(svgUse, skOwnerBounds, ignoreAttributes),
                SvgCircle svgCircle => new CircleDrawable(svgCircle, skOwnerBounds, ignoreAttributes),
                SvgEllipse svgEllipse => new EllipseDrawable(svgEllipse, skOwnerBounds, ignoreAttributes),
                SvgRectangle svgRectangle => new RectangleDrawable(svgRectangle, skOwnerBounds, ignoreAttributes),
                SvgGlyph svgGlyph => new GlyphDrawable(svgGlyph, skOwnerBounds, ignoreAttributes),
                SvgGroup svgGroup => new GroupDrawable(svgGroup, skOwnerBounds, ignoreAttributes),
                SvgLine svgLine => new LineDrawable(svgLine, skOwnerBounds, ignoreAttributes),
                SvgPath svgPath => new PathDrawable(svgPath, skOwnerBounds, ignoreAttributes),
                SvgPolyline svgPolyline => new PolylineDrawable(svgPolyline, skOwnerBounds, ignoreAttributes),
                SvgPolygon svgPolygon => new PolygonDrawable(svgPolygon, skOwnerBounds, ignoreAttributes),
                SvgText svgText => new TextDrawable(svgText, skOwnerBounds, ignoreAttributes),
                _ => null,
            };
        }
    }
}
