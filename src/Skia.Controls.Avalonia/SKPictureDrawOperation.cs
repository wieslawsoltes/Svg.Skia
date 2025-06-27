// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace Avalonia.Controls.Skia;

public class SKPictureDrawOperation : ICustomDrawOperation
{
    private readonly SKPicture? _picture;
    private readonly Rect _bounds;

    public SKPictureDrawOperation(Rect bounds, SKPicture? picture)
    {
        _picture = picture;
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
        if (canvas is null || _picture is null)
        {
            return;
        }

        canvas.Save();
        canvas.DrawPicture(_picture);
        canvas.Restore();
    }
}
