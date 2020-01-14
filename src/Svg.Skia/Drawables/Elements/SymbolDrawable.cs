// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using SkiaSharp;
using Svg.Document_Structure;

namespace Svg.Skia
{
    public class SymbolDrawable : DrawableContainer
    {
        public SymbolDrawable(SvgSymbol svgSymbol, float x, float y, float width, float height, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            IgnoreDisplay = ignoreDisplay;
            IsDrawable = CanDraw(svgSymbol, IgnoreDisplay);

            if (!IsDrawable)
            {
                return;
            }

            if (svgSymbol.CustomAttributes.TryGetValue("width", out string? _widthString))
            {
                if (new SvgUnitConverter().ConvertFromString(_widthString) is SvgUnit _width)
                {
                    width = _width.ToDeviceValue(UnitRenderingType.Horizontal, svgSymbol, skOwnerBounds);
                }
            }

            if (svgSymbol.CustomAttributes.TryGetValue("height", out string? heightString))
            {
                if (new SvgUnitConverter().ConvertFromString(heightString) is SvgUnit _height)
                {
                    height = _height.ToDeviceValue(UnitRenderingType.Vertical, svgSymbol, skOwnerBounds);
                }
            }

            SvgOverflow svgOverflow = SvgOverflow.Hidden;
            if (svgSymbol.TryGetAttribute("overflow", out string overflowString))
            {
                if (new SvgOverflowConverter().ConvertFromString(overflowString) is SvgOverflow _svgOverflow)
                {
                    svgOverflow = _svgOverflow;
                }
            }

            switch (svgOverflow)
            {
                case SvgOverflow.Auto:
                case SvgOverflow.Visible:
                case SvgOverflow.Inherit:
                    break;
                default:
                    ClipRect = SKRect.Create(x, y, width, height);
                    break;
            }

            foreach (var svgElement in svgSymbol.Children)
            {
                var drawable = DrawableFactory.Create(svgElement, skOwnerBounds, ignoreDisplay);
                if (drawable != null)
                {
                    ChildrenDrawables.Add(drawable);
                    _disposable.Add(drawable);
                }
            }

            IsAntialias = SKPaintUtil.IsAntialias(svgSymbol);

            TransformedBounds = SKRect.Empty;

            foreach (var drawable in ChildrenDrawables)
            {
                if (TransformedBounds.IsEmpty)
                {
                    TransformedBounds = drawable.TransformedBounds;
                }
                else
                {
                    if (!drawable.TransformedBounds.IsEmpty)
                    {
                        TransformedBounds = SKRect.Union(TransformedBounds, drawable.TransformedBounds);
                    }
                }
            }

            Transform = SKMatrixUtil.GetSKMatrix(svgSymbol.Transforms);
            var skMatrixViewBox = SKMatrixUtil.GetSvgViewBoxTransform(svgSymbol.ViewBox, svgSymbol.AspectRatio, x, y, width, height);
            SKMatrix.PreConcat(ref Transform, ref skMatrixViewBox);

            PathClip = SvgClipPathUtil.GetSvgVisualElementClipPath(svgSymbol, TransformedBounds, new HashSet<Uri>(), _disposable);
            PictureMask = SvgMaskUtil.GetSvgVisualElementMask(svgSymbol, TransformedBounds, new HashSet<Uri>(), _disposable);
            CreateMaskPaints();
            PaintOpacity = SKPaintUtil.GetOpacitySKPaint(svgSymbol, _disposable);
            PaintFilter = SKPaintUtil.GetFilterSKPaint(svgSymbol, TransformedBounds, _disposable);

            PaintFill = null;
            PaintStroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);
        }
    }
}
