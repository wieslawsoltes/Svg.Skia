// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using SkiaSharp;

namespace Svg.Skia
{
    internal class FragmentDrawable : ChildBaseDrawable
    {
        public FragmentDrawable(SvgFragment svgFragment, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            _ignoreDisplay = ignoreDisplay;
            _canDraw = true;

            float x = svgFragment.X.ToDeviceValue(UnitRenderingType.Horizontal, svgFragment, skOwnerBounds);
            float y = svgFragment.Y.ToDeviceValue(UnitRenderingType.Vertical, svgFragment, skOwnerBounds);
            var skSize = SvgExtensions.GetDimensions(svgFragment);

            switch (svgFragment.Overflow)
            {
                case SvgOverflow.Auto:
                case SvgOverflow.Visible:
                case SvgOverflow.Inherit:
                    break;
                default:
                    _skClipRect = SKRect.Create(x, y, skSize.Width, skSize.Height);
                    break;
            }

            foreach (var svgElement in svgFragment.Children)
            {
                var drawable = DrawableFactory.Create(svgElement, skOwnerBounds, ignoreDisplay);
                if (drawable != null)
                {
                    _childrenDrawables.Add(drawable);
                    _disposable.Add(drawable);
                }
            }

            _antialias = SkiaUtil.IsAntialias(svgFragment);

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

            // TODO: Transform _skBounds using _skMatrix.

            _skMatrix = SkiaUtil.GetSKMatrix(svgFragment.Transforms);
            var skMatrixViewBox = SkiaUtil.GetSvgViewBoxTransform(svgFragment.ViewBox, svgFragment.AspectRatio, x, y, skSize.Width, skSize.Height);
            SKMatrix.PreConcat(ref _skMatrix, ref skMatrixViewBox);

            _skPathClip = null;
            _skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgFragment, _disposable);
            _skPaintFilter = null;

            _skPaintFill = null;
            _skPaintStroke = null;
        }
    }
}
