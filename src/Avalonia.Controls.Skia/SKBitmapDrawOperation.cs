/*
 * Svg.Skia SVG rendering library.
 * Copyright (C) 2023  Wiesław Šoltés
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as
 * published by the Free Software Foundation, either version 3 of the
 * License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace Avalonia.Controls.Skia;

public class SKBitmapDrawOperation : ICustomDrawOperation
{
    private readonly SKBitmap? _bitmap;
    private readonly Rect _bounds;

    public SKBitmapDrawOperation(Rect bounds, SKBitmap? bitmap)
    {
        _bitmap = bitmap;
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
        if (canvas is { } && _bitmap is { })
        {
            canvas.DrawBitmap(
                _bitmap,
                SKRect.Create(0, 0, _bitmap.Width, _bitmap.Height),
                SKRect.Create((float)_bounds.Left, (float)_bounds.Top, (float)_bounds.Width, (float)_bounds.Height));
        }
    }
}
