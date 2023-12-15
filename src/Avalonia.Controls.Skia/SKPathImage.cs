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
using Avalonia.Metadata;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace Avalonia.Controls.Skia;

/// <summary>
/// An <see cref="IImage"/> that uses a <see cref="SKPath"/> for content.
/// </summary>
public class SKPathImage : AvaloniaObject, IImage
{
    /// <summary>
    /// Defines the <see cref="Source"/> property.
    /// </summary>
    public static readonly StyledProperty<SKPath?> SourceProperty =
        AvaloniaProperty.Register<SKPathImage, SKPath?>(nameof(Source));

    /// <summary>
    /// Defines the <see cref="Paint"/> property.
    /// </summary>
    public static readonly StyledProperty<SKPaint?> PaintProperty =
        AvaloniaProperty.Register<SKPathImage, SKPaint?>(nameof(Paint));

    /// <summary>
    /// Gets or sets the <see cref="SKPath"/> content.
    /// </summary>
    [Content]
    public SKPath? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>
    /// Gets or sets the <see cref="SKPaint"/> paint.
    /// </summary>
    public SKPaint? Paint
    {
        get => GetValue(PaintProperty);
        set => SetValue(PaintProperty, value);
    }

    /// <inheritdoc/>
    public Size Size => Source is { } ? new Size(Source.Bounds.Width, Source.Bounds.Height) : default;

    /// <inheritdoc/>
    void IImage.Draw(DrawingContext context, Rect sourceRect, Rect destRect)
    {
        var source = Source;
        if (source is null)
        {
            return;
        }
        var bounds = source.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        var paint = Paint;
        if (paint is null)
        {
            return;
        }
        var scaleMatrix = Matrix.CreateScale(destRect.Width / sourceRect.Width, destRect.Height / sourceRect.Height);
        var translateMatrix = Matrix.CreateTranslation(-sourceRect.X + destRect.X - bounds.Top, -sourceRect.Y + destRect.Y - bounds.Left);
        using (context.PushClip(destRect))
        using (context.PushTransform(translateMatrix * scaleMatrix))
        {
            context.Custom(new SKPathDrawOperation(new Rect(0, 0, bounds.Width, bounds.Height), source, paint));
        }
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            // TODO: Invalidate IImage
        }
    }
}
