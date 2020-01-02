// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using SkiaSharp;

namespace Svg.Skia
{
    internal static class DrawableFactory
    {
        public static BaseDrawable? Create(SvgElement svgElement, SKSize _skSize, bool ignoreDisplay)
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
                SvgCircle svgCircle => new CircleDrawable(svgCircle, _skSize, ignoreDisplay),
                SvgEllipse svgEllipse => new EllipseDrawable(svgEllipse, _skSize, ignoreDisplay),
                SvgRectangle svgRectangle => new RectangleDrawable(svgRectangle, _skSize, ignoreDisplay),
                SvgGlyph svgGlyph => new GlyphDrawable(/* TODO: */),
                SvgGroup svgGroup => new GroupDrawable(svgGroup, _skSize, ignoreDisplay),
                SvgLine svgLine => new LineDrawable(svgLine, _skSize, ignoreDisplay),
                SvgPath svgPath => new PathDrawable(svgPath, _skSize, ignoreDisplay),
                SvgPolyline svgPolyline => new PolylineDrawable(svgPolyline, _skSize, ignoreDisplay),
                SvgPolygon svgPolygon => new PolygonDrawable(svgPolygon, _skSize, ignoreDisplay),
                SvgText svgText => new TextDrawable(/* TODO: */),
                _ => null,
            };
        }
    }
}
