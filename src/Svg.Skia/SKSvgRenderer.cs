// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using SkiaSharp;
using Svg.Document_Structure;

namespace Svg.Skia
{
    public class SKSvgRenderer : ISKSvgRenderer
    {
        private readonly CompositeDisposable _disposable = new CompositeDisposable();

        public void Draw(SKCanvas skCanvas, SKSize skSize, SvgElement svgElement)
        {
            var element = ElementFactory.Create(svgElement);
            if (element != null)
            {
                element.Draw(skCanvas, skSize, _disposable);
            }
        }

        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
}
