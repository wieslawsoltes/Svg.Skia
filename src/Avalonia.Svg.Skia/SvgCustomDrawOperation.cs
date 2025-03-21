﻿using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Svg.Skia;

namespace Avalonia.Svg.Skia;

public class SvgCustomDrawOperation : ICustomDrawOperation
{
    private readonly SKSvg? _svg;

    public SvgCustomDrawOperation(Rect bounds, SKSvg? svg)
    {
        _svg = svg;
        Bounds = bounds;
    }

    public void Dispose()
    {
    }

    public Rect Bounds { get; }

    public bool HitTest(Point p) => true;

    public bool Equals(ICustomDrawOperation? other) => false;

    public void Render(ImmediateDrawingContext context)
    {
        if (_svg == null)
        {
            return;
        }

        var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (leaseFeature is null)
        {
            return;
        }

        using var lease = leaseFeature.Lease();
        var canvas = lease?.SkCanvas;
        if (canvas is null)
        {
            return;
        }

        lock (_svg.Sync)
        {
            var picture = _svg.Picture;
            if (picture is null)
            {
                return;
            }

            canvas.Save();
            canvas.DrawPicture(picture);
            canvas.Restore();
        }
    }
}
