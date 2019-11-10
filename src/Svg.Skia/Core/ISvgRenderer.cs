// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using Svg.Document_Structure;

namespace Svg.Skia
{
    public interface ISvgRenderer : IDisposable
    {
        void DrawFragment(object canvas, SvgFragment svgFragment);
        void DrawImage(object canvas, SvgImage svgImage);
        void DrawSwitch(object canvas, SvgSwitch svgSwitch);
        void DrawSymbol(object canvas, SvgSymbol svgSymbol);
        void DrawUse(object canvas, SvgUse svgUse);
        void DrawForeignObject(object canvas, SvgForeignObject svgForeignObject);
        void DrawCircle(object canvas, SvgCircle svgCircle);
        void DrawEllipse(object canvas, SvgEllipse svgEllipse);
        void DrawRectangle(object canvas, SvgRectangle svgRectangle);
        void DrawMarker(object canvas, SvgMarker svgMarker);
        void DrawGlyph(object canvas, SvgGlyph svgGlyph);
        void DrawGroup(object canvas, SvgGroup svgGroup);
        void DrawLine(object canvas, SvgLine svgLine);
        void DrawPath(object canvas, SvgPath svgPath);
        void DrawPolyline(object canvas, SvgPolyline svgPolyline);
        void DrawPolygon(object canvas, SvgPolygon svgPolygon);
        void DrawText(object canvas, SvgText svgText);
        void DrawTextPath(object canvas, SvgTextPath svgTextPath);
        void DrawTextRef(object canvas, SvgTextRef svgTextRef);
        void DrawTextSpan(object canvas, SvgTextSpan svgTextSpan);
    }
}
