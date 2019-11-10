// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using SkiaSharp;
using Svg;

namespace Svg.Skia
{
    internal struct Text : IElement
    {
        public SvgText svgText;
        public SKMatrix matrix;

        public Text(SvgText text)
        {
            svgText = text;
            matrix = SKSvgHelper.GetSKMatrix(svgText.Transforms);
        }

        public void Draw(SKCanvas skCanvas, SKSize skSize, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgText, disposable);
            var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgText, disposable);
            SKSvgHelper.SetTransform(skCanvas, matrix);

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
