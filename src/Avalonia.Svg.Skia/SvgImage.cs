using Avalonia.Media;
using Avalonia.Metadata;
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
    /// Defines the <see cref="Style"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> StyleProperty =
        AvaloniaProperty.Register<SvgImage, string?>(nameof(Style));

    /// <summary>
    /// Defines the <see cref="CurrentStyle"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> CurrentStyleProperty =
        AvaloniaProperty.Register<SvgImage, string?>(nameof(CurrentStyle));

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
    public string? Style
    {
        get => GetValue(StyleProperty);
        set => SetValue(StyleProperty, value);
    }

    /// <summary>
    /// Gets or sets the <see cref="SvgSource"/> current style.
    /// </summary>
    public string? CurrentStyle
    {
        get => GetValue(CurrentStyleProperty);
        set => SetValue(CurrentStyleProperty, value);
    }

    /// <inheritdoc/>
    public Size Size =>
        Source?.Picture is { } ? new Size(Source.Picture.CullRect.Width, Source.Picture.CullRect.Height) : default;

    /// <inheritdoc/>
    void IImage.Draw(DrawingContext context, Rect sourceRect, Rect destRect)
    {
        var source = Source;

		var style = string.Concat(Style, ' ', CurrentStyle);
        if (source?.Parameters?.Style != style)
        {
            source?.ReLoad(new SvgParameters(null, style));
        }

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
        using (context.PushTransform(translateMatrix * scaleMatrix))
        {
            context.Custom(
                new SvgCustomDrawOperation(
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
            // TODO: Invalidate IImage
        }
    }
}
