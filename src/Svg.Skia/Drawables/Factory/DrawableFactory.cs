// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using SkiaSharp;

namespace Svg.Skia
{
    public static class DrawableFactory
    {
        public static Drawable? Create(SvgElement svgElement, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            return svgElement switch
            {
                SvgAnchor svgAnchor => new AnchorDrawable(svgAnchor, skOwnerBounds, ignoreDisplay),
                SvgFragment svgFragment => new FragmentDrawable(svgFragment, skOwnerBounds, ignoreDisplay),
                SvgImage svgImage => new ImageDrawable(svgImage, skOwnerBounds, ignoreDisplay),
                SvgSwitch svgSwitch => new SwitchDrawable(svgSwitch, skOwnerBounds, ignoreDisplay),
                SvgUse svgUse => new UseDrawable(svgUse, skOwnerBounds, ignoreDisplay),
                SvgCircle svgCircle => new CircleDrawable(svgCircle, skOwnerBounds, ignoreDisplay),
                SvgEllipse svgEllipse => new EllipseDrawable(svgEllipse, skOwnerBounds, ignoreDisplay),
                SvgRectangle svgRectangle => new RectangleDrawable(svgRectangle, skOwnerBounds, ignoreDisplay),
                SvgGlyph svgGlyph => new GlyphDrawable(svgGlyph, skOwnerBounds, ignoreDisplay),
                SvgGroup svgGroup => new GroupDrawable(svgGroup, skOwnerBounds, ignoreDisplay),
                SvgLine svgLine => new LineDrawable(svgLine, skOwnerBounds, ignoreDisplay),
                SvgPath svgPath => new PathDrawable(svgPath, skOwnerBounds, ignoreDisplay),
                SvgPolyline svgPolyline => new PolylineDrawable(svgPolyline, skOwnerBounds, ignoreDisplay),
                SvgPolygon svgPolygon => new PolygonDrawable(svgPolygon, skOwnerBounds, ignoreDisplay),
                SvgText svgText => new TextDrawable(svgText, skOwnerBounds, ignoreDisplay),
                _ => null,
            };
        }
    }
}
