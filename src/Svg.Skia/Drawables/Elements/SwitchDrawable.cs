// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

namespace Svg.Skia
{
    public class SwitchDrawable : Drawable
    {
        public Drawable? FirstChild;

        public SwitchDrawable(SvgSwitch svgSwitch, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(svgSwitch, root, parent)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgSwitch, IgnoreAttributes) && HasFeatures(svgSwitch, IgnoreAttributes);

            if (!IsDrawable)
            {
                return;
            }

            foreach (var child in svgSwitch.Children)
            {
                if (!IsKnownElement(child))
                {
                    continue;
                }

                bool hasRequiredFeatures = HasRequiredFeatures(child);
                bool hasRequiredExtensions = HasRequiredExtensions(child);
                bool hasSystemLanguage = HasSystemLanguage(child);

                if (hasRequiredFeatures && hasRequiredExtensions && hasSystemLanguage)
                {
                    //var ignoreAttributesSwitch = ignoreAttributes 
                    //    | Attributes.Visibility
                    //    | Attributes.Display
                    //    | Attributes.RequiredFeatures
                    //    | Attributes.RequiredExtensions
                    //    | Attributes.SystemLanguage;

                    var drawable = DrawableFactory.Create(child, skOwnerBounds, root, parent, ignoreAttributes);
                    if (drawable != null)
                    {
                        FirstChild = drawable;
                        _disposable.Add(FirstChild);
                    }
                    break;
                }
            }

            if (FirstChild == null)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgSwitch);

            TransformedBounds = FirstChild.TransformedBounds;

            Transform = SvgTransformsExtensions.ToSKMatrix(svgSwitch.Transforms);

            Fill = null;
            Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);
        }

        public override void OnDraw(SKCanvas canvas, Attributes ignoreAttributes, Drawable? until)
        {
            if (until != null && this == until)
            {
                return;
            }

            FirstChild?.Draw(canvas, ignoreAttributes, until);
        }

        public override void PostProcess()
        {
            base.PostProcess();
            FirstChild?.PostProcess();
        }

        public override Drawable? HitTest(SKPoint skPoint)
        {
            var result = FirstChild?.HitTest(skPoint);
            if (result != null)
            {
                return result;
            }
            return base.HitTest(skPoint);
        }
    }
}
