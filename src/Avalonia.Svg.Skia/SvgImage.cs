﻿using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Visuals.Media.Imaging;
using Svg.Model;

namespace Avalonia.Svg.Skia;

/// <summary>
/// An <see cref="IImage"/> that uses a <see cref="SvgSource"/> for content.
/// </summary>
public class SvgImage : AvaloniaObject, IImage
{
    /// <summary>
    /// Defines the <see cref="Source"/> property.
    /// </summary>
    public static readonly StyledProperty<SvgSource?> SourceProperty =
        AvaloniaProperty.Register<SvgImage, SvgSource?>(nameof(Source));

    /// <summary>
    /// Defines the <see cref="Css"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> CssProperty =
        AvaloniaProperty.Register<SvgImage, string?>(nameof(Css));

    /// <summary>
    /// Defines the <see cref="CurrentCss"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> CurrentCssProperty =
        AvaloniaProperty.Register<SvgImage, string?>(nameof(CurrentCss));

    /// <summary>
    /// Gets or sets the <see cref="SvgSource"/> content.
    /// </summary>
    [Content]
    public SvgSource? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>
    /// Gets or sets the <see cref="SvgSource"/> style.
    /// </summary>
    public string? Css
    {
        get => GetValue(CssProperty);
        set => SetValue(CssProperty, value);
    }

    /// <summary>
    /// Gets or sets the <see cref="SvgSource"/> current style.
    /// </summary>
    public string? CurrentCss
    {
        get => GetValue(CurrentCssProperty);
        set => SetValue(CurrentCssProperty, value);
    }

    /// <inheritdoc/>
    public Size Size =>
        Source?.Picture is { } ? new Size(Source.Picture.CullRect.Width, Source.Picture.CullRect.Height) : default;

    /// <inheritdoc/>
    void IImage.Draw(DrawingContext context,
        Rect sourceRect,
        Rect destRect,
        BitmapInterpolationMode bitmapInterpolationMode)
    {
        var source = Source;

        if (source?.Picture is null)
        {
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
        using (context.PushPreTransform(translateMatrix * scaleMatrix))
        {
            context.Custom(
                new SvgCustomDrawOperation(
                    new Rect(0, 0, bounds.Width, bounds.Height),
                    source));
        }
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SourceProperty)
        {
            // TODO: Invalidate IImage
        }

        if (change.Property == CssProperty)
        {
            var css = string.Concat(change.NewValue.GetValueOrDefault<string>(), ' ', CurrentCss);

            if (Source?.Css != css)
            {
                Source?.ReLoad(new SvgParameters(null, css));
            }
        }

        if (change.Property == CurrentCssProperty)
        {
            var css = string.Concat(Css, ' ', change.NewValue.GetValueOrDefault<string>());

            if (Source?.Css != css)
            {
                Source?.ReLoad(new SvgParameters(null, css));
            }
        }
    }
}
