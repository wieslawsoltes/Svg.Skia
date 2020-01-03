// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using SkiaSharp;

namespace Svg.Skia
{
    internal static class DrawableFactory
    {
        public static BaseDrawable? Create(SvgElement svgElement, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            return svgElement switch
            {
#if SVG_ANCHOR
                SvgAnchor svgAnchor => new AnchorDrawable(/* TODO: */),
#endif
                SvgFragment svgFragment => new FragmentDrawable(/* TODO: */),
                SvgImage svgImage => new ImageDrawable(/* TODO: */),
                SvgSwitch svgSwitch => new SwitchDrawable(/* TODO: */),
                SvgUse svgUse => new UseDrawable(/* TODO: */),
                SvgForeignObject svgForeignObject => new ForeignObjectDrawable(/* TODO: */),
                SvgCircle svgCircle => new CircleDrawable(svgCircle, skOwnerBounds, ignoreDisplay),
                SvgEllipse svgEllipse => new EllipseDrawable(svgEllipse, skOwnerBounds, ignoreDisplay),
                SvgRectangle svgRectangle => new RectangleDrawable(svgRectangle, skOwnerBounds, ignoreDisplay),
                SvgGlyph svgGlyph => new GlyphDrawable(/* TODO: */),
                SvgGroup svgGroup => new GroupDrawable(svgGroup, skOwnerBounds, ignoreDisplay),
                SvgLine svgLine => new LineDrawable(svgLine, skOwnerBounds, ignoreDisplay),
                SvgPath svgPath => new PathDrawable(svgPath, skOwnerBounds, ignoreDisplay),
                SvgPolyline svgPolyline => new PolylineDrawable(svgPolyline, skOwnerBounds, ignoreDisplay),
                SvgPolygon svgPolygon => new PolygonDrawable(svgPolygon, skOwnerBounds, ignoreDisplay),
                SvgText svgText => new TextDrawable(/* TODO: */),
                _ => null,
            };
        }
    }
}
