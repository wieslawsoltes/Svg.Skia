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
using SkiaSharp;

namespace Avalonia.Controls.Skia;

/// <summary>
/// SKPicture control.
/// </summary>
public class SKPictureControl : Control
{
    /// <summary>
    /// Defines the <see cref="Picture"/> property.
    /// </summary>
    public static readonly StyledProperty<SKPicture?> PictureProperty =
        AvaloniaProperty.Register<SKPictureControl, SKPicture?>(nameof(Picture));

    /// <summary>
    /// Defines the <see cref="Stretch"/> property.
    /// </summary>
    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<SKPictureControl, Stretch>(nameof(Stretch), Stretch.Uniform);

    /// <summary>
    /// Defines the <see cref="StretchDirection"/> property.
    /// </summary>
    public static readonly StyledProperty<StretchDirection> StretchDirectionProperty =
        AvaloniaProperty.Register<SKPictureControl, StretchDirection>(nameof(StretchDirection), StretchDirection.Both);

    /// <summary>
    /// Gets or sets the <see cref="SKPicture"/> picture.
    /// </summary>
    [Content]
    public SKPicture? Picture
    {
        get => GetValue(PictureProperty);
        set => SetValue(PictureProperty, value);
    }

    /// <summary>
    /// Gets or sets a value controlling how the image will be stretched.
    /// </summary>
    public Stretch Stretch
    {
        get { return GetValue(StretchProperty); }
        set { SetValue(StretchProperty, value); }
    }

    /// <summary>
    /// Gets or sets a value controlling in what direction the image will be stretched.
    /// </summary>
    public StretchDirection StretchDirection
    {
        get { return GetValue(StretchDirectionProperty); }
        set { SetValue(StretchDirectionProperty, value); }
    }

    static SKPictureControl()
    {
        AffectsRender<SKPictureControl>(PictureProperty, StretchProperty, StretchDirectionProperty);
        AffectsMeasure<SKPictureControl>(PictureProperty, StretchProperty, StretchDirectionProperty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var picture = Picture;
        if (picture is null)
        {
            return new Size();
        }

        var sourceSize = new Size(picture.CullRect.Width, picture.CullRect.Height);
        return Stretch.CalculateSize(availableSize, sourceSize, StretchDirection);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var picture = Picture;
        if (picture is null)
        {
            return new Size();
        }

        var sourceSize = new Size(picture.CullRect.Width, picture.CullRect.Height);
        return Stretch.CalculateSize(finalSize, sourceSize);
    }

    public override void Render(DrawingContext context)
    {
        var picture = Picture;
        if (picture is null)
        {
            return;
        }

        var viewPort = new Rect(Bounds.Size);
        var sourceSize = new Size(picture.CullRect.Width, picture.CullRect.Height);
        if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
        {
            return;
        }

        var scale = Stretch.CalculateScaling(Bounds.Size, sourceSize, StretchDirection);
        var scaledSize = sourceSize * scale;
        var destRect = viewPort
            .CenterRect(new Rect(scaledSize))
            .Intersect(viewPort);
        var sourceRect = new Rect(sourceSize)
            .CenterRect(new Rect(destRect.Size / scale));

        var bounds = picture.CullRect;
        var scaleMatrix = Matrix.CreateScale(
            destRect.Width / sourceRect.Width,
            destRect.Height / sourceRect.Height);
        var translateMatrix = Matrix.CreateTranslation(
            -sourceRect.X + destRect.X - bounds.Top,
            -sourceRect.Y + destRect.Y - bounds.Left);

        if (bounds.IsEmpty || destRect == default)
        {
            return;
        }

        using (context.PushClip(destRect))
        using (context.PushTransform(translateMatrix * scaleMatrix))
        {
            context.Custom(
                new SKPictureDrawOperation(
                    new Rect(0, 0, bounds.Width, bounds.Height),
                    picture));
        }
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PictureProperty)
        {
            InvalidateVisual();
        }
    }
}
