using System;
using System.Collections.Generic;
using ShimSkiaSharp;

namespace Svg.Model.Drawables;

public abstract class DrawableContainer(IAssetLoader assetLoader, HashSet<Uri>? references)
    : DrawableBase(assetLoader, references)
{
    public List<DrawableBase> ChildrenDrawables { get; } = [];

    protected void CreateChildren(SvgElement svgElement, SKRect skViewport, DrawableBase? parent, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes)
    {
        foreach (var child in svgElement.Children)
        {
            var drawable = DrawableFactory.Create(child, skViewport, parent, assetLoader, references, ignoreAttributes);
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

    public override void OnDraw(SKCanvas canvas, DrawAttributes ignoreAttributes, DrawableBase? until)
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

    public override void PostProcess(SKRect? viewport, SKMatrix totalMatrix)
    {
        base.PostProcess(viewport, totalMatrix);

        PostProcessChildren(viewport, TotalTransform);
    }

    protected override void PostProcessChildren(SKRect? clip, SKMatrix totalMatrix)
    {
        base.PostProcessChildren(clip, totalMatrix);

        foreach (var child in ChildrenDrawables)
        {
            child.PostProcess(clip, SKMatrix.Identity);
        }
    }

#if USE_DEBUG_DRAW_BOUNDS
    public override void DebugDrawBounds(SKCanvas canvas)
    {
        base.DebugDrawBounds(canvas);

        foreach (var child in ChildrenDrawables)
        {
            child.DebugDrawBounds(canvas);
        }
    }
#endif
}
