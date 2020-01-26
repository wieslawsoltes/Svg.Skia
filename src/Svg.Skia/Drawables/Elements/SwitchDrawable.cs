// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using SkiaSharp;

namespace Svg.Skia
{
    public class SwitchDrawable : Drawable
    {
        public SwitchDrawable(SvgSwitch svgSwitch, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(svgSwitch, root, parent)
        {
            // TODO: Implement drawable.
        }

        public override void OnDraw(SKCanvas canvas, Attributes ignoreAttributes, Drawable? until)
        {
            if (until != null && this == until)
            {
                return;
            }

            // TODO: Implement drawable Record().
        }
    }
}
