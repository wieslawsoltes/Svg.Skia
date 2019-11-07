// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using SkiaSharp;
using Svg;

namespace Svg.Skia
{
    internal struct ForeignObject : IElement
    {
        public SvgForeignObject svgForeignObject;
        public SKMatrix matrix;

        public ForeignObject(SvgForeignObject foreignObject)
        {
            svgForeignObject = foreignObject;
            matrix = SKSvgHelper.GetSKMatrix(svgForeignObject.Transforms);
        }

        public void Draw(SKCanvas skCanvas, SKSize skSize, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgForeignObject, disposable);
            var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgForeignObject, disposable);
            SKSvgHelper.SetTransform(skCanvas, foreignObject.matrix);

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
