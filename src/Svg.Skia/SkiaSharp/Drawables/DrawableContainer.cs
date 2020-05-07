// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public abstract class DrawableContainer : Drawable
    {
        public List<Drawable> ChildrenDrawables = new List<Drawable>();

        public DrawableContainer(SvgElement? element, Drawable? root, Drawable? parent)
            : base(element, root, parent)
        {
        }

        protected virtual void CreateChildren(SvgElement svgElement, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes)
        {
            foreach (var child in svgElement.Children)
            {
                var drawable = DrawableFactory.Create(child, skOwnerBounds, root, parent, ignoreAttributes);
                if (drawable != null)
                {
                    ChildrenDrawables.Add(drawable);
                    _disposable.Add(drawable);
                }
            }
        }

        protected virtual void CreateTransformedBounds()
        {
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
        }

        public override void OnDraw(SKCanvas canvas, Attributes ignoreAttributes, Drawable? until)
        {
            if (until != null && this == until)
            {
                return;
            }

            foreach (var drawable in ChildrenDrawables)
            {
                if (until != null && drawable == until)
                {
                    break;
                }
                drawable.Draw(canvas, ignoreAttributes, until);
            }
        }

        public override void PostProcess()
        {
            base.PostProcess();

            foreach (var child in ChildrenDrawables)
            {
                child.PostProcess();
            }
        }

        public override Drawable? HitTest(SKPoint skPoint)
        {
            foreach (var drawable in ChildrenDrawables)
            {
                var result = drawable.HitTest(skPoint);
                if (result != null)
                {
                    return result;
                }
            }
            return base.HitTest(skPoint);
        }
    }
}
