using System.Collections.Generic;
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
    internal abstract class DrawableContainer : DrawableBase
    {
        public readonly List<DrawableBase> ChildrenDrawables;

        protected DrawableContainer()
            : base()
        {
            ChildrenDrawables = new List<DrawableBase>();
        }

        protected virtual void CreateChildren(SvgElement svgElement, SKRect skOwnerBounds, DrawableBase? parent, Attributes ignoreAttributes)
        {
            foreach (var child in svgElement.Children)
            {
                var drawable = DrawableFactory.Create(child, skOwnerBounds, parent, ignoreAttributes);
                if (drawable != null)
                {
                    ChildrenDrawables.Add(drawable);
                    Disposable.Add(drawable);
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

        public override void OnDraw(SKCanvas canvas, Attributes ignoreAttributes, DrawableBase? until)
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
    }
}
