// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using Avalonia.Media;
using Avalonia.Metadata;
using SkiaSharp;

namespace Avalonia.Controls.Skia;

/// <summary>
/// SKPath control.
/// </summary>
public class SKPathControl : Control
{
    /// <summary>
    /// Defines the <see cref="Path"/> property.
    /// </summary>
    public static readonly StyledProperty<SKPath?> PathProperty =
        AvaloniaProperty.Register<SKPathControl, SKPath?>(nameof(Path));

    /// <summary>
    /// Defines the <see cref="Paint"/> property.
    /// </summary>
    public static readonly StyledProperty<SKPaint?> PaintProperty =
        AvaloniaProperty.Register<SKPathControl, SKPaint?>(nameof(Paint));

    /// <summary>
    /// Defines the <see cref="Stretch"/> property.
    /// </summary>
    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<SKPathControl, Stretch>(nameof(Stretch), Stretch.Uniform);

    /// <summary>
    /// Defines the <see cref="StretchDirection"/> property.
    /// </summary>
    public static readonly StyledProperty<StretchDirection> StretchDirectionProperty =
        AvaloniaProperty.Register<SKPathControl, StretchDirection>(nameof(StretchDirection), StretchDirection.Both);

    /// <summary>
    /// Gets or sets the <see cref="SKPath"/> path.
    /// </summary>
    [Content]
    public SKPath? Path
    {
        get => GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }

    /// <summary>
    /// Gets or sets the <see cref="SKPaint"/> paint.
    /// </summary>
    public SKPaint? Paint
    {
        get => GetValue(PaintProperty);
        set => SetValue(PaintProperty, value);
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

    static SKPathControl()
    {
        AffectsRender<SKPathControl>(PathProperty, StretchProperty, StretchDirectionProperty);
        AffectsMeasure<SKPathControl>(PathProperty, StretchProperty, StretchDirectionProperty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var path = Path;
        if (path is null)
        {
            return new Size();
        }

        var sourceSize = new Size(path.Bounds.Width, path.Bounds.Height);
        return Stretch.CalculateSize(availableSize, sourceSize, StretchDirection);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var path = Path;
        if (path is null)
        {
            return new Size();
        }

        var sourceSize = new Size(path.Bounds.Width, path.Bounds.Height);
        return Stretch.CalculateSize(finalSize, sourceSize);
    }

    public override void Render(DrawingContext context)
    {
        var path = Path;
        if (path is null)
        {
            return;
        }

        var paint = Paint;

        var viewPort = new Rect(Bounds.Size);
        var sourceSize = new Size(path.Bounds.Width, path.Bounds.Height);
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

        var bounds = path.Bounds;
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
            context.Custom(
                new SKPathDrawOperation(
                    new Rect(0, 0, bounds.Width, bounds.Height),
                    path,
                    paint));
        }
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PathProperty)
        {
            InvalidateVisual();
        }

        if (change.Property == PaintProperty)
        {
            InvalidateVisual();
        }
    }
}
