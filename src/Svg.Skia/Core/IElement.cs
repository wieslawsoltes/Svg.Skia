// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using SkiaSharp;

namespace Svg.Skia
{
    internal interface IElement
    {
        void Draw(SKCanvas skCanvas, SKSize skSize, CompositeDisposable disposable);
    }
}
