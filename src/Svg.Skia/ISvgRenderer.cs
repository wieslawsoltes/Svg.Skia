// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using Svg.Document_Structure;

namespace Svg.Skia
{
    public interface ISvgRenderer : IDisposable
    {
        void DrawFragment(SvgFragment svgFragment);
        void DrawImage(SvgImage svgImage);
        void DrawSwitch(SvgSwitch svgSwitch);
        void DrawSymbol(SvgSymbol svgSymbol);
        void DrawUse(SvgUse svgUse);
        void DrawForeignObject(SvgForeignObject svgForeignObject);
        void DrawCircle(SvgCircle svgCircle);
        void DrawEllipse(SvgEllipse svgEllipse);
        void DrawRectangle(SvgRectangle svgRectangle);
        void DrawGlyph(SvgGlyph svgGlyph);
        void DrawGroup(SvgGroup svgGroup);
        void DrawLine(SvgLine svgLine);
        void DrawPath(SvgPath svgPath);
        void DrawPolyline(SvgPolyline svgPolyline);
        void DrawPolygon(SvgPolygon svgPolygon);
        void DrawText(SvgText svgText);
        void DrawTextPath(SvgTextPath svgTextPath);
        void DrawTextRef(SvgTextRef svgTextRef);
        void DrawTextSpan(SvgTextSpan svgTextSpan);
    }
}
