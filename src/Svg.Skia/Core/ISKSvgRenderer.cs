// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using SkiaSharp;
using Svg.Document_Structure;

namespace Svg.Skia
{
    public interface ISKSvgRenderer : IDisposable
    {
        void Draw(SKCanvas skCanvas, SKSize skSize, SvgElement svgElement);
    }
}
