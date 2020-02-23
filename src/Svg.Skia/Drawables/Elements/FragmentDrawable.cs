// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public class FragmentDrawable : DrawableContainer
    {
        public FragmentDrawable(SvgFragment svgFragment, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(svgFragment, root, parent)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = HasFeatures(svgFragment, IgnoreAttributes);

            if (!IsDrawable)
            {
                return;
            }

            var svgFragmentParent = svgFragment.Parent;

            float x = svgFragmentParent == null ? 0f : svgFragment.X.ToDeviceValue(UnitRenderingType.Horizontal, svgFragment, skOwnerBounds);
            float y = svgFragmentParent == null ? 0f : svgFragment.Y.ToDeviceValue(UnitRenderingType.Vertical, svgFragment, skOwnerBounds);

            var skSize = SvgExtensions.GetDimensions(svgFragment);

            if (skOwnerBounds.IsEmpty)
            {
                skOwnerBounds = SKRect.Create(x, y, skSize.Width, skSize.Height);
            }

            CreateChildren(svgFragment, skOwnerBounds, this, this, ignoreAttributes);

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgFragment);

            TransformedBounds = skOwnerBounds;

            CreateTransformedBounds();

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
                        Overflow = SKRect.Create(
                            x,
                            y,
                            Math.Abs(TransformedBounds.Left) + TransformedBounds.Width,
                            Math.Abs(TransformedBounds.Top) + TransformedBounds.Height);
                    }
                    else
                    {
                        Overflow = SKRect.Create(x, y, skSize.Width, skSize.Height);
                    }
                    break;
            }

            var clipPathUris = new HashSet<Uri>();
            var svgClipPath = svgFragment.GetUriElementReference<SvgClipPath>("clip-path", clipPathUris);
            if (svgClipPath != null && svgClipPath.Children != null)
            {
                ClipPath = IgnoreAttributes.HasFlag(Attributes.ClipPath) ? null : SvgClippingExtensions.GetClipPath(svgClipPath, TransformedBounds, clipPathUris, _disposable);
            }
            else
            {
                ClipPath = null;
            }

            Fill = null;
            Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);
        }

        public override void PostProcess()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            var enableOpacity = !IgnoreAttributes.HasFlag(Attributes.Opacity);

            ClipPath = null;
            MaskDrawable = null;

            if (enableOpacity == true)
            {
                Opacity = SvgPaintingExtensions.GetOpacitySKPaint(element, _disposable);
            }
            else
            {
                Opacity = null;
            }

            Filter = null;

            foreach (var child in ChildrenDrawables)
            {
                child.PostProcess();
            }
        }
    }
}
