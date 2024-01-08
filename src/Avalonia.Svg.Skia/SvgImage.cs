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
                
    public static readonly StyledProperty<string> CSSProperty =
    AvaloniaProperty.Register<SvgImage, string>(nameof(CSS));

    public static readonly StyledProperty<string> CSSCurrentProperty =
        AvaloniaProperty.Register<SvgImage, string>(nameof(CSSCurrent));

    /// <summary>
    /// Gets or sets the <see cref="SvgSource"/> content.
    /// </summary>
    [Content]
    public SvgSource? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }
    
     public string CSS
    {
        get => GetValue(CSSProperty);
        set => SetValue(CSSProperty, value);
    }

    public string CSSCurrent
    {
        get => GetValue(CSSCurrentProperty);
        set => SetValue(CSSCurrentProperty, value);
    }

    /// <inheritdoc/>
    public Size Size =>
        Source?.Picture is { } ? new Size(Source.Picture.CullRect.Width, Source.Picture.CullRect.Height) : default;

    /// <inheritdoc/>
    void IImage.Draw(DrawingContext context, Rect sourceRect, Rect destRect)
    {
        var source = Source;
		var css = SvgSource.CombineCSS(CSS, CSSCurrent);
        if (source?.Entities?.CSS != css)
            source?.ReLoad(new SvgParameters() { CSS = css  });
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
