using System.Collections.Generic;
using Svg.Model.Primitives;

namespace Svg.Model.Drawables
{
    public abstract class DrawableContainer : DrawableBase
    {
        public List<DrawableBase> ChildrenDrawables { get; }

        protected DrawableContainer(IAssetLoader assetLoader)
            : base(assetLoader)
        {
            ChildrenDrawables = new List<DrawableBase>();
        }

        protected void CreateChildren(SvgElement svgElement, Rect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, Attributes ignoreAttributes)
        {
            foreach (var child in svgElement.Children)
            {
                var drawable = DrawableFactory.Create(child, skOwnerBounds, parent, assetLoader, ignoreAttributes);
                if (drawable is { })
                {
                    ChildrenDrawables.Add(drawable);
                }
            }
        }

        protected void CreateTransformedBounds()
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
                        TransformedBounds = Rect.Union(TransformedBounds, drawable.TransformedBounds);
                    }
                }
            }
        }

        public override void OnDraw(Canvas canvas, Attributes ignoreAttributes, DrawableBase? until)
        {
            if (until is { } && this == until)
            {
                return;
            }

            foreach (var drawable in ChildrenDrawables)
            {
                if (until is { } && drawable == until)
                {
                    break;
                }
                drawable.Draw(canvas, ignoreAttributes, until);
            }
        }

        public override void PostProcess(Rect? viewport)
        {
            base.PostProcess(viewport);

            foreach (var child in ChildrenDrawables)
            {
                child.PostProcess(viewport);
            }
        }
    }
}
