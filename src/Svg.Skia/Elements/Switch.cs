// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using SkiaSharp;
using Svg;

namespace Svg.Skia
{
    internal struct Switch : IElement
    {
        public SvgSwitch svgSwitch;
        public SKMatrix matrix;

        public Switch(SvgSwitch @switch)
        {
            svgSwitch = @switch;
            matrix = SkiaUtil.GetSKMatrix(svgSwitch.Transforms);
        }

        public void Draw(SKCanvas skCanvas, SKSize skSize, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgSwitch, disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgSwitch, disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

            // TODO:

            if (skPaintFilter != null)
            {
                skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                skCanvas.Restore();
            }

            skCanvas.Restore();
        }
    }
}
