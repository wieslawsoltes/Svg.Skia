// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System.Collections.Generic;
using SkiaSharp;
using Svg;
using Svg.Document_Structure;

namespace Svg.Skia
{
    internal struct Symbol : IElement
    {
        public SvgSymbol svgSymbol;
        public List<IElement> children;
        public float x;
        public float y;
        public float width;
        public float height;
        public SKRect bounds;
        public SKMatrix matrix;

        public Symbol(SvgSymbol symbol)
        {
            svgSymbol = symbol;
            children = new List<IElement>();

            foreach (var svgElement in svgSymbol.Children)
            {
                var element = ElementFactory.Create(svgElement);
                if (element != null)
                {
                    children.Add(element);
                }
            }

            x = 0f;
            y = 0f;
            width = svgSymbol.ViewBox.Width;
            height = svgSymbol.ViewBox.Height;

            if (svgSymbol.CustomAttributes.TryGetValue("width", out string? _widthString))
            {
                if (new SvgUnitConverter().ConvertFrom(_widthString) is SvgUnit _width)
                {
                    width = _width.ToDeviceValue(null, UnitRenderingType.Horizontal, svgSymbol);
                }
            }

            if (svgSymbol.CustomAttributes.TryGetValue("height", out string? heightString))
            {
                if (new SvgUnitConverter().ConvertFrom(heightString) is SvgUnit _height)
                {
                    height = _height.ToDeviceValue(null, UnitRenderingType.Vertical, svgSymbol);
                }
            }

            bounds = SKRect.Create(x, y, width, height);

            matrix = SkiaUtil.GetSKMatrix(svgSymbol.Transforms);
            var viewBoxMatrix = SkiaUtil.GetSvgViewBoxTransform(svgSymbol.ViewBox, svgSymbol.AspectRatio, x, y, width, height);
            SKMatrix.Concat(ref matrix, ref matrix, ref viewBoxMatrix);
        }

        public void Draw(SKCanvas skCanvas, SKSize skSize, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgSymbol, disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgSymbol, disposable);
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
