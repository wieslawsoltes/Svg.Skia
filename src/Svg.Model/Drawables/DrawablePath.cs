using System;
using System.Collections.Generic;
using ShimSkiaSharp;

namespace Svg.Model.Drawables;

public abstract class DrawablePath(IAssetLoader assetLoader, HashSet<Uri>? references)
    : DrawableBase(assetLoader, references), IMarkerHost
{
    public SKPath? Path { get; set; }
    public List<DrawableBase>? MarkerDrawables { get; set; }

    void IMarkerHost.AddMarker(DrawableBase drawable)
    {
        MarkerDrawables ??= [];
        MarkerDrawables.Add(drawable);
    }

    public override void OnDraw(SKCanvas canvas, DrawAttributes ignoreAttributes, DrawableBase? until)
    {
        if (until is { } && this == until)
        {
            return;
        }

        if (Fill is { } && Path is { })
        {
            canvas.DrawPath(Path, Fill);
        }

        if (Stroke is { } && Path is { })
        {
            canvas.DrawPath(Path, Stroke);
        }

        if (MarkerDrawables is { })
        {
            foreach (var drawable in MarkerDrawables)
            {
                drawable.Draw(canvas, ignoreAttributes, until, true);
            }
        }
    }

    public override void PostProcess(SKRect? viewport, SKMatrix totalMatrix)
    {
        base.PostProcess(viewport, totalMatrix);

        if (MarkerDrawables is { })
        {
            foreach (var drawable in MarkerDrawables)
            {
                drawable.PostProcess(viewport, TotalTransform);
            }
        }
    }
}
