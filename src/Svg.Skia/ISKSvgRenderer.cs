// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using SkiaSharp;
using Svg.Document_Structure;

namespace Svg.Skia
{
    public interface ISKSvgRenderer : IDisposable
    {
        void DrawFragment(SKCanvas skCanvas, SKSize skSize, SvgFragment svgFragment);
        void DrawSymbol(SKCanvas skCanvas, SKSize skSize, SvgSymbol svgSymbol);
        void DrawImage(SKCanvas skCanvas, SKSize skSize, SvgImage svgImage);
        void DrawSwitch(SKCanvas skCanvas, SKSize skSize, SvgSwitch svgSwitch);
        void DrawUse(SKCanvas skCanvas, SKSize skSize, SvgUse svgUse);
        void DrawForeignObject(SKCanvas skCanvas, SKSize skSize, SvgForeignObject svgForeignObject);
        void DrawCircle(SKCanvas skCanvas, SKSize skSize, SvgCircle svgCircle);
        void DrawEllipse(SKCanvas skCanvas, SKSize skSize, SvgEllipse svgEllipse);
        void DrawRectangle(SKCanvas skCanvas, SKSize skSize, SvgRectangle svgRectangle);
        void DrawMarker(SKCanvas skCanvas, SKSize skSize, SvgMarker svgMarker);
        void DrawGlyph(SKCanvas skCanvas, SKSize skSize, SvgGlyph svgGlyph);
        void DrawGroup(SKCanvas skCanvas, SKSize skSize, SvgGroup svgGroup);
        void DrawLine(SKCanvas skCanvas, SKSize skSize, SvgLine svgLine);
        void DrawPath(SKCanvas skCanvas, SKSize skSize, SvgPath svgPath);
        void DrawPolyline(SKCanvas skCanvas, SKSize skSize, SvgPolyline svgPolyline);
        void DrawPolygon(SKCanvas skCanvas, SKSize skSize, SvgPolygon svgPolygon);
        void DrawText(SKCanvas skCanvas, SKSize skSize, SvgText svgText);
        void DrawTextPath(SKCanvas skCanvas, SKSize skSize, SvgTextPath svgTextPath);
        void DrawTextRef(SKCanvas skCanvas, SKSize skSize, SvgTextRef svgTextRef);
        void DrawTextSpan(SKCanvas skCanvas, SKSize skSize, SvgTextSpan svgTextSpan);
        void DrawElement(SKCanvas skCanvas, SKSize skSize, SvgElement svgElement);
        void DrawElementCollection(SKCanvas canvas, SKSize skSize, SvgElementCollection svgElementCollection);
    }
}
