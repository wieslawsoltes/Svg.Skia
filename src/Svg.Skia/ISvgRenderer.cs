// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using SkiaSharp;
using Svg.Document_Structure;

namespace Svg.Skia
{
    public interface ISvgRenderer : IDisposable
    {
        void DrawSvgFragment(SKCanvas skCanvas, SKSize skSize, SvgFragment svgFragment);
        void DrawSvgSymbol(SKCanvas skCanvas, SKSize skSize, SvgSymbol svgSymbol);
        void DrawSvgImage(SKCanvas skCanvas, SKSize skSize, SvgImage svgImage);
        void DrawSvgSwitch(SKCanvas skCanvas, SKSize skSize, SvgSwitch svgSwitch);
        void DrawSvgUse(SKCanvas skCanvas, SKSize skSize, SvgUse svgUse);
        void DrawSvgForeignObject(SKCanvas skCanvas, SKSize skSize, SvgForeignObject svgForeignObject);
        void DrawSvgCircle(SKCanvas skCanvas, SKSize skSize, SvgCircle svgCircle);
        void DrawSvgEllipse(SKCanvas skCanvas, SKSize skSize, SvgEllipse svgEllipse);
        void DrawSvgRectangle(SKCanvas skCanvas, SKSize skSize, SvgRectangle svgRectangle);
        void DrawSvgMarker(SKCanvas skCanvas, SKSize skSize, SvgMarker svgMarker);
        void DrawSvgGlyph(SKCanvas skCanvas, SKSize skSize, SvgGlyph svgGlyph);
        void DrawSvgGroup(SKCanvas skCanvas, SKSize skSize, SvgGroup svgGroup);
        void DrawSvgLine(SKCanvas skCanvas, SKSize skSize, SvgLine svgLine);
        void DrawSvgPath(SKCanvas skCanvas, SKSize skSize, SvgPath svgPath);
        void DrawSvgPolyline(SKCanvas skCanvas, SKSize skSize, SvgPolyline svgPolyline);
        void DrawSvgPolygon(SKCanvas skCanvas, SKSize skSize, SvgPolygon svgPolygon);
        void DrawSvgText(SKCanvas skCanvas, SKSize skSize, SvgText svgText);
        void DrawSvgTextPath(SKCanvas skCanvas, SKSize skSize, SvgTextPath svgTextPath);
        void DrawSvgTextRef(SKCanvas skCanvas, SKSize skSize, SvgTextRef svgTextRef);
        void DrawSvgTextSpan(SKCanvas skCanvas, SKSize skSize, SvgTextSpan svgTextSpan);
        void DrawSvgElement(SKCanvas skCanvas, SKSize skSize, SvgElement svgElement);
        void DrawSvgElementCollection(SKCanvas canvas, SKSize skSize, SvgElementCollection svgElementCollection);
    }
}
