// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;

namespace Avalonia.Svg.Skia;

public class SvgSourceCustomDrawOperation : ICustomDrawOperation
{
    private readonly SvgSource? _svg;

    public SvgSourceCustomDrawOperation(Rect bounds, SvgSource? svg)
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

        if (!_svg.BeginRender())
        {
            return;
        }

        try
        {
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

            if (_svg.Svg is { } skSvg)
            {
                skSvg.Draw(canvas);
                return;
            }

            var picture = _svg.Picture;
            if (picture is null)
            {
                return;
            }

            canvas.Save();
            canvas.DrawPicture(picture);
            canvas.Restore();
        }
        finally
        {
            _svg.EndRender();
        }
    }
}
