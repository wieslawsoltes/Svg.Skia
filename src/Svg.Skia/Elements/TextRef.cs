// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using SkiaSharp;
using Svg;

namespace Svg.Skia
{
    internal struct TextRef : IElement
    {
        public SvgTextRef svgTextRef;
        public SKMatrix matrix;

        public TextRef(SvgTextRef textRef)
        {
            svgTextRef = textRef;
            matrix = SKSvgHelper.GetSKMatrix(svgTextRef.Transforms);
        }

        public void Draw(SKCanvas skCanvas, SKSize skSize, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgTextRef, disposable);
            var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgTextRef, disposable);
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
