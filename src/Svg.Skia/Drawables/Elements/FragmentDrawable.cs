// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public class FragmentDrawable : DrawableContainer
    {
        public FragmentDrawable(SvgFragment svgFragment, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes = IgnoreAttributes.None)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = true;

            var parent = svgFragment.Parent;

            float x = parent == null ? 0f : svgFragment.X.ToDeviceValue(UnitRenderingType.Horizontal, svgFragment, skOwnerBounds);
            float y = parent == null ? 0f : svgFragment.Y.ToDeviceValue(UnitRenderingType.Vertical, svgFragment, skOwnerBounds);

            var skSize = SvgExtensions.GetDimensions(svgFragment);

            if (skOwnerBounds.IsEmpty)
            {
                skOwnerBounds = SKRect.Create(x, y, skSize.Width, skSize.Height);
            }

            foreach (var svgElement in svgFragment.Children)
            {
                var drawable = DrawableFactory.Create(svgElement, skOwnerBounds, ignoreAttributes);
                if (drawable != null)
                {
                    ChildrenDrawables.Add(drawable);
                    _disposable.Add(drawable);
                }
            }

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgFragment);

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

            Transform = SvgTransformsExtensions.ToSKMatrix(svgFragment.Transforms);
            var skMatrixViewBox = SvgTransformsExtensions.ToSKMatrix(svgFragment.ViewBox, svgFragment.AspectRatio, x, y, skSize.Width, skSize.Height);
            SKMatrix.PreConcat(ref Transform, ref skMatrixViewBox);

            switch (svgFragment.Overflow)
            {
                case SvgOverflow.Auto:
                case SvgOverflow.Visible:
                case SvgOverflow.Inherit:
                    break;
                default:
                    if (skSize.IsEmpty)
                    {
                        Clip = SKRect.Create(
                            0f,
                            0f,
                            Math.Abs(TransformedBounds.Left) + TransformedBounds.Width,
                            Math.Abs(TransformedBounds.Top) + TransformedBounds.Height);
                    }
                    else
                    {
                        Clip = SKRect.Create(x, y, skSize.Width, skSize.Height);
                    }
                    break;
            }

            var clipPathUris = new HashSet<Uri>();
            var svgClipPath = svgFragment.GetUriElementReference<SvgClipPath>("clip-path", clipPathUris);
            if (svgClipPath != null && svgClipPath.Children != null)
            {
                ClipPath = SvgClippingExtensions.GetClipPath(svgClipPath, TransformedBounds, clipPathUris, _disposable);
            }
            else
            {
                ClipPath = null;
            }

            MaskDrawable = null;
            Opacity = ignoreAttributes.HasFlag(IgnoreAttributes.Opacity) ? null : SvgPaintingExtensions.GetOpacitySKPaint(svgFragment, _disposable);
            Filter = null;

            Fill = null;
            Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);
        }
    }
}
