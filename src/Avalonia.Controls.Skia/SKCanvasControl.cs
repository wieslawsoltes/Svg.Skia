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
using SkiaSharp;

namespace Avalonia.Controls.Skia;

/// <summary>
/// SKCanvas control.
/// </summary>
public class SKCanvasControl : Control
{
    /// <summary>
    /// 
    /// </summary>
    public event EventHandler<SKCanvasEventArgs>? Draw;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    public override void Render(DrawingContext context)
    {
        var viewPort = new Rect(Bounds.Size);
        using var clip = ClipToBounds ? context.PushClip(viewPort) : default;
        context.Custom(
            new SKCanvasDrawOperation(
                new Rect(0, 0, viewPort.Width, viewPort.Height),
                RaiseOnDraw));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="canvas"></param>
    private void RaiseOnDraw(SKCanvas canvas)
    {
        var e = new SKCanvasEventArgs(canvas);
        OnDraw(e);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    protected virtual void OnDraw(SKCanvasEventArgs e)
    {
        Draw?.Invoke(this, e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ClipToBoundsProperty)
        {
            InvalidateVisual();
        }
    }
}
