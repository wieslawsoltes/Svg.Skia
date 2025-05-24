using System;

using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace Avalonia.Controls.Skia;

/// <summary>
/// 
/// </summary>
public class SKCanvasDrawOperation : ICustomDrawOperation
{
    private readonly Rect _bounds;
    private readonly Action<SKCanvas> _invalidate;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="bounds"></param>
    /// <param name="invalidate"></param>
    public SKCanvasDrawOperation(Rect bounds, Action<SKCanvas> invalidate)
    {
        _bounds = bounds;
        _invalidate = invalidate;
    }

    /// <summary>
    /// 
    /// </summary>
    public void Dispose()
    {
    }

    /// <summary>
    /// 
    /// </summary>
    public Rect Bounds => _bounds;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="p"></param>
    /// <returns></returns>
    public bool HitTest(Point p) => _bounds.Contains(p);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool Equals(ICustomDrawOperation? other) => false;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    public void Render(ImmediateDrawingContext context)
    {
        var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (leaseFeature is null)
        {
            return;
        }
        using var lease = leaseFeature.Lease();
        var canvas = lease?.SkCanvas;
        if (canvas is { })
        {
            _invalidate(canvas);
        }
    }
}
