﻿using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace Avalonia.Controls.Skia;

public class SKPathDrawOperation : ICustomDrawOperation
{
    private readonly SKPath? _path;
    private readonly SKPaint? _paint;
    private readonly Rect _bounds;

    public SKPathDrawOperation(Rect bounds, SKPath? path, SKPaint? paint)
    {
        _path = path;
        _paint = paint;
        _bounds = bounds;
    }

    public void Dispose()
    {
    }

    public Rect Bounds => _bounds;

    public bool HitTest(Point p) => _bounds.Contains(p);

    public bool Equals(ICustomDrawOperation? other) => false;

    public void Render(ImmediateDrawingContext context)
    {
        var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (leaseFeature is null)
        {
            return;
        }
        using var lease = leaseFeature.Lease();
        var canvas = lease?.SkCanvas;
        if (canvas is null || _path is null)
        {
            return;
        }

        canvas.DrawPath(_path, _paint);
    }
}
