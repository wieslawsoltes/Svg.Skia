﻿// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
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
        var translateMatrix = Matrix.CreateTranslation(-sourceRect.X + destRect.X - bounds.Left, -sourceRect.Y + destRect.Y - bounds.Top);
        using (context.PushClip(destRect))
        using (context.PushTransform(scaleMatrix * translateMatrix))
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
