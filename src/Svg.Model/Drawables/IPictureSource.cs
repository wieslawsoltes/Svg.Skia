// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using ShimSkiaSharp;

namespace Svg.Model.Drawables;

internal interface IPictureSource
{
    void OnDraw(SKCanvas canvas, DrawAttributes ignoreAttributes, DrawableBase? until);
    void Draw(SKCanvas canvas, DrawAttributes ignoreAttributes, DrawableBase? until, bool enableTransform);
}
