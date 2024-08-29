using System;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Metadata;
using SkiaSharp;

namespace Avalonia.Controls.Skia;

/// <summary>
/// An <see cref="IImage"/> that uses a <see cref="SKBitmapImage"/> for content.
/// </summary>
public class SKBitmapImage : AvaloniaObject, IImage
{
    /// <summary>
    /// Defines the <see cref="Source"/> property.
    /// </summary>
    public static readonly StyledProperty<SKBitmap?> SourceProperty =
        AvaloniaProperty.Register<SKBitmapImage, SKBitmap?>(nameof(Source));

    /// <summary>
    /// Gets or sets the <see cref="SKBitmap"/> content.
    /// </summary>
    [Content]
    public SKBitmap? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <inheritdoc/>
    public Size Size => Source is { } ? new Size(Source.Width, Source.Height) : default;

    /// <inheritdoc/>
    void IImage.Draw(DrawingContext context, Rect sourceRect, Rect destRect)
    {
        var source = Source;
        if (source is null)
        {
            return;
        }
        if (source.Width <= 0 || source.Height <= 0)
        {
            return;
        }
        var bounds = SKRect.Create(0, 0, source.Width, source.Height);
        var scaleMatrix = Matrix.CreateScale(destRect.Width / sourceRect.Width, destRect.Height / sourceRect.Height);
        var translateMatrix = Matrix.CreateTranslation(-sourceRect.X + destRect.X - bounds.Top, -sourceRect.Y + destRect.Y - bounds.Left);
        using (context.PushClip(destRect))
        using (context.PushTransform(scaleMatrix * translateMatrix))
        {
            context.Custom(new SKBitmapDrawOperation(new Rect(0, 0, bounds.Width, bounds.Height), source));
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
