﻿using System;
#if !USE_PICTURE
using SkiaSharp;
using CropRect = SkiaSharp.SKImageFilter.CropRect;
#endif
#if USE_PICTURE
using SKCanvas = Svg.Picture.Canvas;
using SKRect = Svg.Picture.Rect;
#endif

namespace Svg.Skia
{
    internal sealed class SwitchDrawable : DrawableBase
    {
        public DrawableBase? FirstChild;

        private SwitchDrawable()
            : base()
        {
        }

        public static SwitchDrawable Create(SvgSwitch svgSwitch, SKRect skOwnerBounds, DrawableBase? parent, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new SwitchDrawable
            {
                Element = svgSwitch,
                Parent = parent,

                IgnoreAttributes = ignoreAttributes
            };
            drawable.IsDrawable = drawable.CanDraw(svgSwitch, drawable.IgnoreAttributes) && drawable.HasFeatures(svgSwitch, drawable.IgnoreAttributes);

            if (!drawable.IsDrawable)
            {
                return drawable;
            }

            foreach (var child in svgSwitch.Children)
            {
                if (!child.IsKnownElement())
                {
                    continue;
                }

                bool hasRequiredFeatures = child.HasRequiredFeatures();
                bool hasRequiredExtensions = child.HasRequiredExtensions();
                bool hasSystemLanguage = child.HasSystemLanguage();

                if (hasRequiredFeatures && hasRequiredExtensions && hasSystemLanguage)
                {
                    var childDrawable = DrawableFactory.Create(child, skOwnerBounds, parent, ignoreAttributes);
                    if (childDrawable != null)
                    {
                        drawable.FirstChild = childDrawable;
                        drawable.Disposable.Add(drawable.FirstChild);
                    }
                    break;
                }
            }

            if (drawable.FirstChild == null)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            drawable.IsAntialias = SvgExtensions.IsAntialias(svgSwitch);

            drawable.TransformedBounds = drawable.FirstChild.TransformedBounds;

            drawable.Transform = SvgExtensions.ToSKMatrix(svgSwitch.Transforms);

            drawable.Fill = null;
            drawable.Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            return drawable;
        }

        public override void OnDraw(SKCanvas canvas, Attributes ignoreAttributes, DrawableBase? until)
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
    }
}
