// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using SkiaSharp;

namespace Svg.Skia
{
    internal abstract class BaseDrawable : SKDrawable
    {
        internal CompositeDisposable _disposable = new CompositeDisposable();
        internal bool _canDraw;
        internal bool _ignoreDisplay;
        internal bool _antialias;
        internal SKRect _skBounds;
        internal SKMatrix _skMatrix;

        internal SKRect? _skClipRect;
        internal SKPath? _skPathClip;
        internal SKPaint? _skPaintOpacity;
        internal SKPaint? _skPaintFilter;
        internal SKPaint? _skPaintFill;
        internal SKPaint? _skPaintStroke;

        protected bool CanDraw(SvgVisualElement svgVisualElement, bool ignoreDisplay)
        {
            bool visible = svgVisualElement.Visible == true;
            bool display = ignoreDisplay ? true : !string.Equals(svgVisualElement.Display, "none", StringComparison.OrdinalIgnoreCase);
            return visible && display;
        }

        protected override SKRect OnGetBounds()
        {
            if (_canDraw)
            {
                return _skBounds;
            }
            return SKRect.Empty;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _disposable?.Dispose();
        }
    }
}
