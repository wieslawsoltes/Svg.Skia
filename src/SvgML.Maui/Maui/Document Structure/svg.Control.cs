using SkiaSharp;
using SkiaSharp.Views.Maui;
using Svg.Model;
using Svg.Skia;

namespace SvgML;

/// <summary>
/// Svg control.
/// </summary>
public partial class svg
{
    static svg()
    {
        Initialize();

        // TODO:
        // ClipToBoundsProperty.OverrideDefaultValue(typeof(svg), true);
    }

    public svg()
    {
        Loaded += OnLoaded;
        PaintSurface += OnPaintSurface;
    }

    private static void OnCssPropertyAttachedPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is svg svg)
        {
            svg.InvalidateMeasure();
            svg.InvalidateSurface();
        }
    }

    // TODO:
    //*
    protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
    {
        // return base.MeasureOverride(widthConstraint, heightConstraint);
        if (_picture == null)
        {
            return new Size();
        }

        var sourceSize = _picture is { }
            ? new Size(_picture.CullRect.Width, _picture.CullRect.Height)
            : default;

        // return Stretch.CalculateSize(availableSize, sourceSize, StretchDirection);
        return sourceSize;
    }
    //*/

    // TODO:
    /*
    protected override Size ArrangeOverride(Rect bounds)
    {
        // return base.ArrangeOverride(bounds);

        if (_picture == null)
        {
            return new Size();
        }

        var sourceSize = _picture is { }
            ? new Size(_picture.CullRect.Width, _picture.CullRect.Height)
            : default;

        // TODO:
        // return Stretch.CalculateSize(finalSize, sourceSize);
        return sourceSize;
    }
    //*/

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        Render(e);
    }
    
    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);

        Render(e);
    }

    private void Render(SKCanvas canvas)
    {
        lock (Sync)
        {
            var picture = Picture;
            if (picture is null)
            {
                return;
            }

            canvas.Save();
            canvas.DrawPicture(picture);
            canvas.Restore();
        } 
    }

    private void Render(SKPaintSurfaceEventArgs e)
    {
        var source = _picture;
        if (source is null)
        {
            return;
        }

        // TODO: Copy code from Avalonia version: public override void Render(DrawingContext context)

        // TODO: SKPictureCustomDrawOperation
        Render(e.Surface.Canvas);
    }

    // TODO:
    /*
    public override void Render(DrawingContext context)
    {
        var source = _picture;
        if (source is null)
        {
            return;
        }

        var viewPort = new Rect(Bounds.Size);
        var sourceSize = new Size(source.CullRect.Width, source.CullRect.Height);
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

        var bounds = source.CullRect;
        var scaleMatrix = Matrix.CreateScale(
            destRect.Width / sourceRect.Width,
            destRect.Height / sourceRect.Height);
        var translateMatrix = Matrix.CreateTranslation(
            -sourceRect.X + destRect.X - bounds.Top,
            -sourceRect.Y + destRect.Y - bounds.Left);

        using var _ = ClipToBounds ? context.PushClip(destRect) : default;

        using (context.PushTransform(translateMatrix * scaleMatrix))
        {
            context.Custom(
                new SKPictureCustomDrawOperation(
                    new Rect(0, 0, bounds.Width, bounds.Height),
                    this));
        }
    }
    */

    protected override void Invalidate()
    {
        base.Invalidate();

        // TODO: Only invalidate SvgSource if its Svg property that changed.

        if (IsLoaded)
        {
            OnSourceChanged(this);
        }
    }

    protected override void OnPropertyChanged(string propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        
        if (propertyName == "Css")
        {
            OnCssChanged(GetCss(this));
        }

        if (propertyName == "CurrentCss")
        {
            OnCurrentCssChanged(GetCurrentCss(this));
        }

        if (propertyName == "ClipToBounds")
        {
            InvalidateSurface();
        }

        if (propertyName == "Stretch" || propertyName == "StretchDirection")
        {
            InvalidateMeasure();
            InvalidateSurface();
        }
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        OnSourceChanged(this);
    }

    private void OnCssChanged(string? css)
    {
        var source = this;
        var currentCss = GetCurrentCss(this);
        var parameters = new SvgParameters(null, string.Concat(css, ' ', currentCss));
        Load(source, parameters);
        InvalidateMeasure();
        InvalidateSurface();
    }

    private void OnCurrentCssChanged(string? currentCss)
    {
        var source = this;
        var css = GetCss(this);
        var parameters = new SvgParameters(null, string.Concat(css, ' ', currentCss));
        Load(source, parameters);
        InvalidateMeasure();
        InvalidateSurface();
    }

    private void OnSourceChanged(svg? source)
    {
        var css = GetCss(this);
        var currentCss = GetCurrentCss(this);
        var parameters = new SvgParameters(null, string.Concat(css, ' ', currentCss));
        Load(source, parameters);
        InvalidateMeasure();
        InvalidateSurface();
    }
}
