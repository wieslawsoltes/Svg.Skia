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
