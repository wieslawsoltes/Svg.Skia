// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using ShimSkiaSharp;

namespace Svg.Model.Drawables;

internal interface IFilterSource
{
    SKPicture? SourceGraphic(SKRect? clip);
    SKPicture? BackgroundImage(SKRect? clip);
    SKPaint? FillPaint();
    SKPaint? StrokePaint();
}
