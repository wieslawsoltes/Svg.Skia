using System;
using Avalonia.Media;
using Avalonia.Metadata;
using SkiaSharp;

namespace Avalonia.Controls.Skia;

/// <summary>
/// SKBitmap control.
/// </summary>
public class SKBitmapControl : Control
{
    /// <summary>
    /// Defines the <see cref="Bitmap"/> property.
    /// </summary>
    public static readonly StyledProperty<SKBitmap?> BitmapProperty =
        AvaloniaProperty.Register<SKBitmapControl, SKBitmap?>(nameof(Bitmap));

    /// <summary>
    /// Defines the <see cref="Stretch"/> property.
    /// </summary>
    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<SKBitmapControl, Stretch>(nameof(Stretch), Stretch.Uniform);

    /// <summary>
    /// Defines the <see cref="StretchDirection"/> property.
    /// </summary>
    public static readonly StyledProperty<StretchDirection> StretchDirectionProperty =
        AvaloniaProperty.Register<SKBitmapControl, StretchDirection>(nameof(StretchDirection), StretchDirection.Both);

    /// <summary>
    /// Gets or sets the <see cref="SKBitmap"/> bitmap.
    /// </summary>
    [Content]
    public SKBitmap? Bitmap
    {
        get => GetValue(BitmapProperty);
        set => SetValue(BitmapProperty, value);
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

    static SKBitmapControl()
    {
        AffectsRender<SKBitmapControl>(BitmapProperty, StretchProperty, StretchDirectionProperty);
        AffectsMeasure<SKBitmapControl>(BitmapProperty, StretchProperty, StretchDirectionProperty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var bitmap = Bitmap;
        if (bitmap is null)
        {
            return new Size();
        }

        var sourceSize = new Size(bitmap.Width, bitmap.Height);
        return Stretch.CalculateSize(availableSize, sourceSize, StretchDirection);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var bitmap = Bitmap;
        if (bitmap is null)
        {
            return new Size();
        }

        var sourceSize = new Size(bitmap.Width, bitmap.Height);
        return Stretch.CalculateSize(finalSize, sourceSize);
    }

    public override void Render(DrawingContext context)
    {
        var bitmap = Bitmap;
        if (bitmap is null)
        {
            return;
        }

        var viewPort = new Rect(Bounds.Size);
        var sourceSize = new Size(bitmap.Width, bitmap.Height);
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

        var bounds = SKRect.Create(0, 0, bitmap.Width, bitmap.Height);
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
        using (context.PushTransform(scaleMatrix * translateMatrix))
        {
            context.Custom(new SKBitmapDrawOperation(new Rect(0, 0, bounds.Width, bounds.Height), bitmap));
        }
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == BitmapProperty)
        {
            InvalidateVisual();
        }
    }
}
