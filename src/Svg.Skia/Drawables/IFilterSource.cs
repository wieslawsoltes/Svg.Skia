// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using SkiaSharp;

namespace Svg.Skia
{
    public interface IFilterSource
    {
        SKPicture? SourceGraphic();
        SKPicture? BackgroundImage();
        SKPaint? FillPaint();
        SKPaint? StrokePaint();
    }
}
