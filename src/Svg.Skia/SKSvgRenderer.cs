// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using SkiaSharp;
using Svg.Document_Structure;

namespace Svg.Skia
{
    public class SKSvgRenderer : ISvgRenderer
    {
        private readonly CompositeDisposable _disposable = new CompositeDisposable();
        private readonly SKSize _skSize;

        public SKSvgRenderer(SKSize skSize)
        {
            _disposable = new CompositeDisposable();
            _skSize = skSize;
        }

        public void Draw(object canvas, SvgElement svgElement)
        {
            if (canvas is SKCanvas skCanvas)
            {
                var element = ElementFactory.Create(svgElement);
                if (element != null)
                {
                    element.Draw(skCanvas, _skSize, _disposable);
                }
            }
        }

        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
}
