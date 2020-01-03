// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    internal class GroupDrawable : ChildBaseDrawable
    {
        public GroupDrawable(SvgGroup svgGroup, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            _ignoreDisplay = ignoreDisplay;
            _canDraw = CanDraw(svgGroup, _ignoreDisplay);

            if (!_canDraw)
            {
                return;
            }

            // TODO: Call AddMarkers.

            foreach (var svgElement in svgGroup.Children)
            {
                var drawable = DrawableFactory.Create(svgElement, skOwnerBounds, ignoreDisplay);
                if (drawable != null)
                {
                    _childrenDrawable.Add(drawable);
                    _disposable.Add(drawable);
                }
            }

            _antialias = SkiaUtil.IsAntialias(svgGroup);

            _skBounds = SKRect.Empty;

            foreach (var drawable in _childrenDrawable)
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

            _skMatrix = SkiaUtil.GetSKMatrix(svgGroup.Transforms);
            _skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgGroup, _skBounds, new HashSet<Uri>(), _disposable);
            _skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgGroup, _disposable);
            _skPaintFilter = SkiaUtil.GetFilterSKPaint(svgGroup, _disposable);

            if (SkiaUtil.IsValidFill(svgGroup))
            {
                _skPaintFill = SkiaUtil.GetFillSKPaint(svgGroup, _skBounds, _disposable);
            }

            if (SkiaUtil.IsValidStroke(svgGroup, _skBounds))
            {
                _skPaintStroke = SkiaUtil.GetStrokeSKPaint(svgGroup, _skBounds, _disposable);
            }
        }
    }
}
