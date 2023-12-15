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
/// An <see cref="IImage"/> that uses a <see cref="SKPicture"/> for content.
/// </summary>
public class SKPictureImage : AvaloniaObject, IImage
{
    /// <summary>
    /// Defines the <see cref="Source"/> property.
    /// </summary>
    public static readonly StyledProperty<SKPicture?> SourceProperty =
        AvaloniaProperty.Register<SKPictureImage, SKPicture?>(nameof(Source));

    /// <summary>
    /// Gets or sets the <see cref="SKPicture"/> content.
    /// </summary>
    [Content]
    public SKPicture? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <inheritdoc/>
    public Size Size => Source is { } ? new Size(Source.CullRect.Width, Source.CullRect.Height) : default;

    /// <inheritdoc/>
    void IImage.Draw(DrawingContext context, Rect sourceRect, Rect destRect)
    {
        var source = Source;
        if (source is null)
        {
            return;
        }
        var bounds = source.CullRect;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        var scaleMatrix = Matrix.CreateScale(destRect.Width / sourceRect.Width, destRect.Height / sourceRect.Height);
        var translateMatrix = Matrix.CreateTranslation(-sourceRect.X + destRect.X - bounds.Top, -sourceRect.Y + destRect.Y - bounds.Left);
        using (context.PushClip(destRect))
        using (context.PushTransform(translateMatrix * scaleMatrix))
        {
            context.Custom(new SKPictureDrawOperation(new Rect(0, 0, bounds.Width, bounds.Height), source));
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
