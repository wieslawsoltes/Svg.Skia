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
        void DrawFragment(SvgFragment svgFragment, bool ignoreDisplay);
        void DrawImage(SvgImage svgImage, bool ignoreDisplay);
        void DrawSwitch(SvgSwitch svgSwitch, bool ignoreDisplay);
        void DrawUse(SvgUse svgUse, bool ignoreDisplay);
        void DrawForeignObject(SvgForeignObject svgForeignObject, bool ignoreDisplay);
        void DrawCircle(SvgCircle svgCircle, bool ignoreDisplay);
        void DrawEllipse(SvgEllipse svgEllipse, bool ignoreDisplay);
        void DrawRectangle(SvgRectangle svgRectangle, bool ignoreDisplay);
        void DrawGlyph(SvgGlyph svgGlyph, bool ignoreDisplay);
        void DrawGroup(SvgGroup svgGroup, bool ignoreDisplay);
        void DrawLine(SvgLine svgLine, bool ignoreDisplay);
        void DrawPath(SvgPath svgPath, bool ignoreDisplay);
        void DrawPolyline(SvgPolyline svgPolyline, bool ignoreDisplay);
        void DrawPolygon(SvgPolygon svgPolygon, bool ignoreDisplay);
        void DrawText(SvgText svgText, bool ignoreDisplay);
    }
}
