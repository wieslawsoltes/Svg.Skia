// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using SkiaSharp;
using Svg.Document_Structure;

namespace Svg.Skia
{
    public class SymbolDrawable : DrawableContainer
    {
        public SymbolDrawable(SvgSymbol svgSymbol, float x, float y, float width, float height, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgSymbol, IgnoreAttributes);

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
                    Clip = SKRect.Create(x, y, width, height);
                    break;
            }

            CreateChildren(svgSymbol, skOwnerBounds, ignoreAttributes);

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgSymbol);

            TransformedBounds = SKRect.Empty;

            CreateTransformedBounds();

            Transform = SvgTransformsExtensions.ToSKMatrix(svgSymbol.Transforms);
            var skMatrixViewBox = SvgTransformsExtensions.ToSKMatrix(svgSymbol.ViewBox, svgSymbol.AspectRatio, x, y, width, height);
            SKMatrix.PreConcat(ref Transform, ref skMatrixViewBox);

            ClipPath = SvgClippingExtensions.GetSvgVisualElementClipPath(svgSymbol, TransformedBounds, new HashSet<Uri>(), _disposable);
            MaskDrawable = SvgClippingExtensions.GetSvgVisualElementMask(svgSymbol, TransformedBounds, new HashSet<Uri>(), _disposable);
            if (MaskDrawable != null)
            {
                CreateMaskPaints();
            }
            Opacity = ignoreAttributes.HasFlag(IgnoreAttributes.Opacity) ? null : SvgPaintingExtensions.GetOpacitySKPaint(svgSymbol, _disposable);
            Filter = ignoreAttributes.HasFlag(IgnoreAttributes.Filter) ? null : SvgFiltersExtensions.GetFilterSKPaint(svgSymbol, TransformedBounds, _disposable);

            Fill = null;
            Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);
        }
    }
}
