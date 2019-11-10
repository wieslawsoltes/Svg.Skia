// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Svg.Skia
{
    internal static class ElementFactory
    {
        public static IElement? Create(SvgElement svgElement)
        {
            switch (svgElement)
            {
                case SvgFragment svgFragment:
                    return new Fragment(svgFragment);
                case SvgImage svgImage:
                    return new Image(svgImage);
                case SvgSwitch svgSwitch:
                    return new Switch(svgSwitch);
                case SvgUse svgUse:
                    var svgVisualElement = SkiaUtil.GetReference<SvgVisualElement>(svgUse, svgUse.ReferencedElement);
                    if (svgVisualElement != null && !SkiaUtil.HasRecursiveReference(svgUse))
                    {
                        return new Use(svgUse, svgVisualElement);
                    }
                    return null;
                case SvgForeignObject svgForeignObject:
                    return new ForeignObject(svgForeignObject);
                case SvgCircle svgCircle:
                    return new Circle(svgCircle);
                case SvgEllipse svgEllipse:
                    return new Ellipse(svgEllipse);
                case SvgRectangle svgRectangle:
                    return new Rectangle(svgRectangle);
                case SvgMarker svgMarker:
                    return new Marker(svgMarker);
                case SvgGlyph svgGlyph:
                    return new Glyph(svgGlyph);
                case SvgGroup svgGroup:
                    return new Group(svgGroup);
                case SvgLine svgLine:
                    return new Line(svgLine);
                case SvgPath svgPath:
                    return new Path(svgPath);
                case SvgPolyline svgPolyline:
                    return new Polyline(svgPolyline);
                case SvgPolygon svgPolygon:
                    return new Polygon(svgPolygon);
                case SvgText svgText:
                    return new Text(svgText);
                case SvgTextPath svgTextPath:
                    return new TextPath(svgTextPath);
                case SvgTextRef svgTextRef:
                    return new TextRef(svgTextRef);
                case SvgTextSpan svgTextSpan:
                    return new TextSpan(svgTextSpan);
                default:
                    return null;
            }
        }
    }
}
