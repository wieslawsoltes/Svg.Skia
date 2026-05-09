// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using Avalonia.Media;
using Avalonia.Metadata;
using Svg.Model;
using DrawingColor = System.Drawing.Color;

namespace Avalonia.Svg.Skia;

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
    /// Defines the <see cref="CurrentColor"/> property.
    /// </summary>
    public static readonly StyledProperty<Color?> CurrentColorProperty =
        AvaloniaProperty.Register<SvgImage, Color?>(nameof(CurrentColor));

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

    /// <summary>
    /// Gets or sets the default SVG currentColor value.
    /// </summary>
    public Color? CurrentColor
    {
        get => GetValue(CurrentColorProperty);
        set => SetValue(CurrentColorProperty, value);
    }

    /// <inheritdoc/>
    public Size Size =>
        Source?.Picture is { } ? new Size(Source.Picture.CullRect.Width, Source.Picture.CullRect.Height) : default;

    /// <inheritdoc/>
    void IImage.Draw(DrawingContext context, Rect sourceRect, Rect destRect)
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
        using (context.PushTransform(scaleMatrix * translateMatrix))
        {
            context.Custom(
                new SvgSourceCustomDrawOperation(
                    new Rect(0, 0, bounds.Width, bounds.Height),
                    source));
        }
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SourceProperty)
        {
            if (HasStyleOverrides())
            {
                ApplyStyleParametersToSource();
            }

            // TODO: Invalidate IImage
            RaiseInvalidated(EventArgs.Empty);
        }

        if (change.Property == CssProperty ||
            change.Property == CurrentCssProperty ||
            change.Property == CurrentColorProperty)
        {
            if (ApplyStyleParametersToSource())
            {
                RaiseInvalidated(EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Creates a deep clone of this <see cref="SvgImage"/> with an independent source.
    /// </summary>
    /// <returns>A new <see cref="SvgImage"/> instance.</returns>
    public SvgImage Clone()
    {
        var source = Source?.Clone();
        return new SvgImage
        {
            Css = Css,
            CurrentCss = CurrentCss,
            CurrentColor = CurrentColor,
            Source = source
        };
    }

    private bool ApplyStyleParametersToSource()
    {
        if (Source is not { } source)
        {
            return false;
        }

        var parameters = BuildParameters(source, Css, CurrentCss, CurrentColor);
        if (HasSameParameters(source.Parameters, parameters))
        {
            return false;
        }

        source.ReLoad(parameters);
        return true;
    }

    private bool HasStyleOverrides()
    {
        return !string.IsNullOrWhiteSpace(Css) ||
               !string.IsNullOrWhiteSpace(CurrentCss) ||
               CurrentColor is not null;
    }

    private static SvgParameters? BuildParameters(
        SvgSource source,
        string? css,
        string? currentCss,
        Color? currentColor)
    {
        var entities = source.Parameters?.Entities ?? source.Entities;
        var baseCss = string.IsNullOrWhiteSpace(css) && string.IsNullOrWhiteSpace(currentCss)
            ? source.Parameters?.Css ?? source.Css
            : null;
        var combinedCss = CombineCss(baseCss, css, currentCss);
        var drawingColor = ToDrawingColor(currentColor) ?? source.Parameters?.CurrentColor ?? ToDrawingColor(source.CurrentColor);
        return entities is null && string.IsNullOrWhiteSpace(combinedCss) && drawingColor is null
            ? null
            : new SvgParameters(entities, combinedCss, drawingColor);
    }

    private static bool HasSameParameters(SvgParameters? left, SvgParameters? right)
    {
        return ReferenceEquals(left?.Entities, right?.Entities) &&
               HasSameCss(left?.Css, right?.Css) &&
               Nullable.Equals(left?.CurrentColor, right?.CurrentColor);
    }

    private static bool HasSameCss(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right))
        {
            return true;
        }

        return string.Equals(left, right, StringComparison.Ordinal);
    }

    private static string? CombineCss(string? baseCss, string? css, string? currentCss)
    {
        var combinedCss = CombineCss(css, currentCss);
        return CombineCss(baseCss, combinedCss);
    }

    private static string? CombineCss(string? css, string? currentCss)
    {
        if (string.IsNullOrWhiteSpace(css))
        {
            return string.IsNullOrWhiteSpace(currentCss) ? null : currentCss;
        }

        if (string.IsNullOrWhiteSpace(currentCss))
        {
            return css;
        }

        return string.Concat(css, ' ', currentCss);
    }

    private static DrawingColor? ToDrawingColor(Color? color)
    {
        return color is { } value
            ? DrawingColor.FromArgb(value.A, value.R, value.G, value.B)
            : null;
    }

    /// <summary>
    /// Raises the <see cref="Invalidated"/> event.
    /// </summary>
    /// <param name="e">The event args.</param>
    protected void RaiseInvalidated(EventArgs e) => Invalidated?.Invoke(this, e);
}
