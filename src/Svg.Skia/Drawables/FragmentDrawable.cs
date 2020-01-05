// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using SkiaSharp;

namespace Svg.Skia
{
    internal class FragmentDrawable : DrawableContainer
    {
        public FragmentDrawable(SvgFragment svgFragment, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            IgnoreDisplay = ignoreDisplay;
            IsDrawable = true;

            float x = svgFragment.X.ToDeviceValue(UnitRenderingType.Horizontal, svgFragment, skOwnerBounds);
            float y = svgFragment.Y.ToDeviceValue(UnitRenderingType.Vertical, svgFragment, skOwnerBounds);

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

            IsAntialias = SkiaUtil.IsAntialias(svgFragment);

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

            Transform = SkiaUtil.GetSKMatrix(svgFragment.Transforms);
            var skMatrixViewBox = SkiaUtil.GetSvgViewBoxTransform(svgFragment.ViewBox, svgFragment.AspectRatio, x, y, skSize.Width, skSize.Height);
            SKMatrix.PreConcat(ref Transform, ref skMatrixViewBox);

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);

            //switch (svgFragment.Overflow)
            //{
            //    case SvgOverflow.Auto:
            //    case SvgOverflow.Visible:
            //    case SvgOverflow.Inherit:
            //        break;
            //    default:
            //        if (skSize.IsEmpty)
            //        {
            //            _skClipRect = _skBounds;
            //        }
            //        else
            //        {
            //            _skClipRect = SKRect.Create(x, y, skSize.Width, skSize.Height);
            //        }
            //        break;
            //}

            PathClip = null;
            PaintOpacity = SkiaUtil.GetOpacitySKPaint(svgFragment, _disposable);
            PaintFilter = null;

            PaintFill = null;
            PaintStroke = null;
        }
    }
}
