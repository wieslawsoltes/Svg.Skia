// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using SkiaSharp;

namespace Svg.Skia
{
    public class GroupDrawable : DrawableContainer
    {
        public GroupDrawable(SvgGroup svgGroup, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(svgGroup, root, parent)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgGroup, IgnoreAttributes) && HasFeatures(svgGroup, IgnoreAttributes);

            // NOTE: Call AddMarkers only once.
            SvgMarkerExtensions.AddMarkers(svgGroup);

            CreateChildren(svgGroup, skOwnerBounds, root, this, ignoreAttributes);

            // TODO: Check if children are explicitly set to be visible.
            //foreach (var child in ChildrenDrawables)
            //{
            //    if (child.IsDrawable)
            //    {
            //        IsDrawable = true;
            //        break;
            //    }
            //}

            if (!IsDrawable)
            {
                return;
            }

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgGroup);

            TransformedBounds = SKRect.Empty;

            CreateTransformedBounds();

            Transform = SvgTransformsExtensions.ToSKMatrix(svgGroup.Transforms);

            Fill = null;
            Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);
        }
    }
}
