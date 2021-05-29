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

        protected void CreateChildren(SvgElement svgElement, SKRect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, Attributes ignoreAttributes)
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

        protected void CreateGeometryBounds()
        {
            foreach (var drawable in ChildrenDrawables)
            {
                if (GeometryBounds.IsEmpty)
                {
                    GeometryBounds = drawable.GeometryBounds;
                }
                else
                {
                    if (!drawable.GeometryBounds.IsEmpty)
                    {
                        GeometryBounds = SKRect.Union(GeometryBounds, drawable.GeometryBounds);
                    }
                }
            }
        }

        public override void OnDraw(SKCanvas canvas, Attributes ignoreAttributes, DrawableBase? until)
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
                drawable.Draw(canvas, ignoreAttributes, until, true);
            }
        }

        public override void PostProcess(SKRect? viewport)
        {
            base.PostProcess(viewport);

            foreach (var child in ChildrenDrawables)
            {
                child.PostProcess(viewport);
            }
        }
    }
}
