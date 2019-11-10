// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System.Collections.Generic;
using SkiaSharp;
using Svg;

namespace Svg.Skia
{
    internal struct Group : IElement
    {
        public SvgGroup svgGroup;
        public List<IElement> children;
        public SKMatrix matrix;

        public Group(SvgGroup group)
        {
            svgGroup = group;
            children = new List<IElement>();

            foreach (var svgElement in svgGroup.Children)
            {
                var element = ElementFactory.Create(svgElement);
                if (element != null)
                {
                    children.Add(element);
                }
            }

            matrix = SkiaUtil.GetSKMatrix(svgGroup.Transforms);
        }

        public void Draw(SKCanvas skCanvas, SKSize skSize, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgGroup, disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgGroup, disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

            for (int i = 0; i < children.Count; i++)
            {
                children[i].Draw(skCanvas, skSize, disposable);
            }

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
