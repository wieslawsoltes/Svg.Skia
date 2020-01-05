// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using SkiaSharp;

namespace Svg.Skia
{
#if USE_SVG_ANCHOR
    internal class AnchorDrawable : DrawableContainer
    {
        public AnchorDrawable(SvgAnchor svgAnchor, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            _ignoreDisplay = ignoreDisplay;
            _canDraw = true;

            if (!_canDraw)
            {
                return;
            }

            foreach (var svgElement in svgAnchor.Children)
            {
                var drawable = DrawableFactory.Create(svgElement, skOwnerBounds, ignoreDisplay);
                if (drawable != null)
                {
                    _childrenDrawables.Add(drawable);
                    _disposable.Add(drawable);
                }
            }

            _antialias = SkiaUtil.IsAntialias(svgAnchor);

            _skBounds = SKRect.Empty;

            foreach (var drawable in _childrenDrawables)
            {
                if (_skBounds.IsEmpty)
                {
                    _skBounds = drawable._skBounds;
                }
                else
                {
                    if (!drawable._skBounds.IsEmpty)
                    {
                        _skBounds = SKRect.Union(_skBounds, drawable._skBounds);
                    }
                }
            }

            _skMatrix = SkiaUtil.GetSKMatrix(svgAnchor.Transforms);

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref _skMatrix, out _skBounds, ref _skBounds);

            _skPathClip = null;
            _skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgAnchor, _disposable);
            _skPaintFilter = null;

            _skPaintFill = null;
            _skPaintStroke = null;
        }
    }
#endif
}
