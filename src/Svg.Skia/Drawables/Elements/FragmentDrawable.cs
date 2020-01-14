// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using SkiaSharp;

namespace Svg.Skia
{
    public class FragmentDrawable : DrawableContainer
    {
        public FragmentDrawable(SvgFragment svgFragment, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            IgnoreDisplay = ignoreDisplay;
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
                var drawable = DrawableFactory.Create(svgElement, skOwnerBounds, ignoreDisplay);
                if (drawable != null)
                {
                    ChildrenDrawables.Add(drawable);
                    _disposable.Add(drawable);
                }
            }

            IsAntialias = SKPaintUtil.IsAntialias(svgFragment);

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

            Transform = SKMatrixUtil.GetSKMatrix(svgFragment.Transforms);
            var skMatrixViewBox = SKMatrixUtil.GetSvgViewBoxTransform(svgFragment.ViewBox, svgFragment.AspectRatio, x, y, skSize.Width, skSize.Height);
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
                        ClipRect = SKRect.Create(
                            0f,
                            0f,
                            Math.Abs(TransformedBounds.Left) + TransformedBounds.Width,
                            Math.Abs(TransformedBounds.Top) + TransformedBounds.Height);
                    }
                    else
                    {
                        ClipRect = SKRect.Create(x, y, skSize.Width, skSize.Height);
                    }
                    break;
            }

            PathClip = null;
            PictureMask = null;
            PaintOpacity = SKPaintUtil.GetOpacitySKPaint(svgFragment, _disposable);
            PaintFilter = null;

            PaintFill = null;
            PaintStroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);
        }
    }
}
