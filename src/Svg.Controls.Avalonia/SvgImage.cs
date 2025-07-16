﻿// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Diagnostics;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Metadata;
using ShimSkiaSharp;
using SP = Svg.Model;

namespace Avalonia.Svg;

/// <summary>
/// An <see cref="IImage"/> that uses a <see cref="SvgSource"/> for content.
/// </summary>
public class SvgImage : AvaloniaObject, IImage
{
    /// <summary>
    /// Raised when the resource changes visually.
    /// </summary>
    public event EventHandler? Invalidated;

    /// <summary>
    /// Defines the <see cref="Source"/> property.
    /// </summary>
    public static readonly StyledProperty<SvgSource> SourceProperty =
        AvaloniaProperty.Register<SvgImage, SvgSource>(nameof(Source));

    /// <summary>
    /// Gets or sets the <see cref="SvgSource"/> content.
    /// </summary>
    [Content]
    public SvgSource Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <inheritdoc/>
    public Size Size =>
        Source?.Picture is { } ? new Size(Source.Picture.CullRect.Width, Source.Picture.CullRect.Height) : default;

    private SKPicture? _previousPicture = null;
    private AvaloniaPicture? _avaloniaPicture = null;

    /// <inheritdoc/>
    void IImage.Draw(
        DrawingContext context,
        Rect sourceRect,
        Rect destRect)
    {
        var source = Source;
        if (source is null || source.Picture is null)
        {
            _previousPicture = null;
            _avaloniaPicture?.Dispose();
            _avaloniaPicture = null;
            return;
        }

        if (Size.Width <= 0 || Size.Height <= 0)
        {
            return;
        }

        var bounds = source.Picture.CullRect;
        var scaleMatrix = Matrix.CreateScale(
            destRect.Width / sourceRect.Width,
            destRect.Height / sourceRect.Height);
        var translateMatrix = Matrix.CreateTranslation(
            -sourceRect.X + destRect.X - bounds.Top,
            -sourceRect.Y + destRect.Y - bounds.Left);
        using (context.PushClip(destRect))
        using (context.PushTransform(scaleMatrix * translateMatrix))
        {
            try
            {
                if (_avaloniaPicture is null || source.Picture != _previousPicture)
                {
                    _previousPicture = source.Picture;
                    _avaloniaPicture?.Dispose();
                    _avaloniaPicture = AvaloniaPicture.Record(source.Picture);
                }

                if (_avaloniaPicture is { })
                {
                    _avaloniaPicture.Draw(context);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{ex.Message}");
                Debug.WriteLine($"{ex.StackTrace}");
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SourceProperty)
        {
            // TODO: Invalidate IImage
            RaiseInvalidated(EventArgs.Empty);
        }
    }

    /// <summary>
    /// Raises the <see cref="Invalidated"/> event.
    /// </summary>
    /// <param name="e">The event args.</param>
    protected void RaiseInvalidated(EventArgs e) => Invalidated?.Invoke(this, e);
}
