// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using Svg.Document_Structure;

namespace Svg.Skia
{
    public interface ISvgRenderer : IDisposable
    {
        // TODO:
        //void DrawAnchor(SvgAnchor svgAnchor);
        void DrawFragment(SvgFragment svgFragment, bool alwaysDisplay);
        void DrawImage(SvgImage svgImage, bool alwaysDisplay);
        void DrawSwitch(SvgSwitch svgSwitch, bool alwaysDisplay);
        void DrawSymbol(SvgSymbol svgSymbol, bool alwaysDisplay);
        void DrawUse(SvgUse svgUse, bool alwaysDisplay);
        void DrawForeignObject(SvgForeignObject svgForeignObject, bool alwaysDisplay);
        void DrawCircle(SvgCircle svgCircle, bool alwaysDisplay);
        void DrawEllipse(SvgEllipse svgEllipse, bool alwaysDisplay);
        void DrawRectangle(SvgRectangle svgRectangle, bool alwaysDisplay);
        void DrawGlyph(SvgGlyph svgGlyph, bool alwaysDisplay);
        void DrawGroup(SvgGroup svgGroup, bool alwaysDisplay);
        void DrawLine(SvgLine svgLine, bool alwaysDisplay);
        void DrawPath(SvgPath svgPath, bool alwaysDisplay);
        void DrawPolyline(SvgPolyline svgPolyline, bool alwaysDisplay);
        void DrawPolygon(SvgPolygon svgPolygon, bool alwaysDisplay);
        void DrawText(SvgText svgText, bool alwaysDisplay);
        void DrawTextPath(SvgTextPath svgTextPath, bool alwaysDisplay);
        void DrawTextRef(SvgTextRef svgTextRef, bool alwaysDisplay);
        void DrawTextSpan(SvgTextSpan svgTextSpan, bool alwaysDisplay);
    }
}
