// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using SkiaSharp;

namespace Svg.Skia
{
    public interface ISvgRenderer : IDisposable
    {
#if USE_SVG_ANCHOR
        void DrawAnchor(SvgAnchor svgAnchor, SKRect skOwnerBounds, bool ignoreDisplay);
#endif
        void DrawFragment(SvgFragment svgFragment, SKRect skOwnerBounds, bool ignoreDisplay);
        void DrawImage(SvgImage svgImage, SKRect skOwnerBounds, bool ignoreDisplay);
        void DrawSwitch(SvgSwitch svgSwitch, SKRect skOwnerBounds, bool ignoreDisplay);
        void DrawUse(SvgUse svgUse, SKRect skOwnerBounds, bool ignoreDisplay);
        void DrawForeignObject(SvgForeignObject svgForeignObject, SKRect skOwnerBounds, bool ignoreDisplay);
        void DrawCircle(SvgCircle svgCircle, SKRect skOwnerBounds, bool ignoreDisplay);
        void DrawEllipse(SvgEllipse svgEllipse, SKRect skOwnerBounds, bool ignoreDisplay);
        void DrawRectangle(SvgRectangle svgRectangle, SKRect skOwnerBounds, bool ignoreDisplay);
        void DrawGlyph(SvgGlyph svgGlyph, SKRect skOwnerBounds, bool ignoreDisplay);
        void DrawGroup(SvgGroup svgGroup, SKRect skOwnerBounds, bool ignoreDisplay);
        void DrawLine(SvgLine svgLine, SKRect skOwnerBounds, bool ignoreDisplay);
        void DrawPath(SvgPath svgPath, SKRect skOwnerBounds, bool ignoreDisplay);
        void DrawPolyline(SvgPolyline svgPolyline, SKRect skOwnerBounds, bool ignoreDisplay);
        void DrawPolygon(SvgPolygon svgPolygon, SKRect skOwnerBounds, bool ignoreDisplay);
        void DrawText(SvgText svgText, SKRect skOwnerBounds, bool ignoreDisplay);
    }
}
