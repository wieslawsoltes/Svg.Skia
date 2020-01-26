// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public class GroupDrawable : DrawableContainer
    {
        public GroupDrawable(SvgGroup svgGroup, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(root, parent)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgGroup, IgnoreAttributes);

            if (!IsDrawable)
            {
                return;
            }

            // TODO: Call AddMarkers only once.
            SvgMarkerExtensions.AddMarkers(svgGroup);

            CreateChildren(svgGroup, skOwnerBounds, root, this, ignoreAttributes);

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgGroup);

            TransformedBounds = SKRect.Empty;

            CreateTransformedBounds();

            Transform = SvgTransformsExtensions.ToSKMatrix(svgGroup.Transforms);

            Fill = null;
            Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);

            ClipPath = IgnoreAttributes.HasFlag(Attributes.ClipPath) ? null : SvgClippingExtensions.GetSvgVisualElementClipPath(svgGroup, TransformedBounds, new HashSet<Uri>(), _disposable);
            MaskDrawable = IgnoreAttributes.HasFlag(Attributes.Mask) ? null : SvgClippingExtensions.GetSvgVisualElementMask(svgGroup, TransformedBounds, new HashSet<Uri>(), _disposable);
            if (MaskDrawable != null)
            {
                CreateMaskPaints();
            }
            Opacity = IgnoreAttributes.HasFlag(Attributes.Opacity) ? null : SvgPaintingExtensions.GetOpacitySKPaint(svgGroup, _disposable);
            Filter = IgnoreAttributes.HasFlag(Attributes.Filter) ? null : SvgFiltersExtensions.GetFilterSKPaint(svgGroup, TransformedBounds, this, _disposable);
        }
    }
}
