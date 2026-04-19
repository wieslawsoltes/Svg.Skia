using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Logging;
using Avalonia.Media;
using ShimSkiaSharp;
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

        ClipToBoundsProperty.OverrideDefaultValue(typeof(svg), true);

        AffectsRender<svg>(StretchProperty, StretchDirectionProperty);
        AffectsMeasure<svg>(StretchProperty, StretchDirectionProperty);

        CssProperty.Changed.AddClassHandler<Control>(OnCssPropertyAttachedPropertyChanged);
        CurrentCssProperty.Changed.AddClassHandler<Control>(OnCssPropertyAttachedPropertyChanged);
    }

    private static void OnCssPropertyAttachedPropertyChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
    {
        if (d is Control control)
        {
            control.InvalidateMeasure();
            control.InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_picture == null)
        {
            return new Size();
        }

        var sourceSize = _picture is { }
            ? new Size(_picture.CullRect.Width, _picture.CullRect.Height)
            : default;

        return Stretch.CalculateSize(availableSize, sourceSize, StretchDirection);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_picture == null)
        {
            return new Size();
        }

        var sourceSize = _picture is { }
            ? new Size(_picture.CullRect.Width, _picture.CullRect.Height)
            : default;

        return Stretch.CalculateSize(finalSize, sourceSize);
    }

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
            -sourceRect.X + destRect.X - bounds.Left,
            -sourceRect.Y + destRect.Y - bounds.Top);

        using var _ = ClipToBounds ? context.PushClip(destRect) : default;

        using (context.PushTransform(translateMatrix * scaleMatrix))
        {
            context.Custom(
                new SKPictureCustomDrawOperation(
                    new Rect(0, 0, bounds.Width, bounds.Height),
                    this));
        }
    }

    private bool TryGetPictureToControlMatrix(out Matrix matrix)
    {
        matrix = Matrix.Identity;

        var picture = _picture;
        if (picture is null)
        {
            return false;
        }

        var sourceBounds = picture.CullRect;
        var sourceSize = new Size(sourceBounds.Width, sourceBounds.Height);
        if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
        {
            return false;
        }

        var viewPort = new Rect(Bounds.Size);
        var scale = Stretch.CalculateScaling(Bounds.Size, sourceSize, StretchDirection);
        var scaledSize = sourceSize * scale;
        var destRect = viewPort
            .CenterRect(new Rect(scaledSize))
            .Intersect(viewPort);

        if (destRect.Width <= 0 || destRect.Height <= 0)
        {
            return false;
        }

        var sourceRect = new Rect(sourceSize)
            .CenterRect(new Rect(destRect.Size / scale));

        var scaleMatrix = Matrix.CreateScale(
            destRect.Width / sourceRect.Width,
            destRect.Height / sourceRect.Height);
        var translateMatrix = Matrix.CreateTranslation(
            -sourceRect.X + destRect.X - sourceBounds.Left,
            -sourceRect.Y + destRect.Y - sourceBounds.Top);

        matrix = translateMatrix * scaleMatrix;
        return true;
    }

    public bool TryGetPicturePoint(Point point, out SKPoint picturePoint)
    {
        picturePoint = default;

        if (!TryGetPictureToControlMatrix(out var matrix) || !matrix.TryInvert(out var inverse))
        {
            return false;
        }

        var local = inverse.Transform(point);
        picturePoint = new SKPoint((float)local.X, (float)local.Y);
        return true;
    }

    public bool TryGetPictureRect(Rect rect, out SKRect pictureRect)
    {
        pictureRect = default;

        if (!TryGetPictureToControlMatrix(out var matrix) || !matrix.TryInvert(out var inverse))
        {
            return false;
        }

        var topLeft = inverse.Transform(rect.TopLeft);
        var topRight = inverse.Transform(rect.TopRight);
        var bottomRight = inverse.Transform(rect.BottomRight);
        var bottomLeft = inverse.Transform(rect.BottomLeft);

        var minX = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomRight.X, bottomLeft.X));
        var minY = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomRight.Y, bottomLeft.Y));
        var maxX = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomRight.X, bottomLeft.X));
        var maxY = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomRight.Y, bottomLeft.Y));

        pictureRect = new SKRect((float)minX, (float)minY, (float)maxX, (float)maxY);
        return true;
    }

    public IEnumerable<SvgSceneNode> HitTestSceneNodes(SKPoint point)
    {
        if (_skSvg is null)
        {
            yield break;
        }

        foreach (var sceneNode in _skSvg.HitTestSceneNodes(point))
        {
            yield return sceneNode;
        }
    }

    public IEnumerable<SvgSceneNode> HitTestSceneNodes(SKRect rect)
    {
        if (_skSvg is null)
        {
            yield break;
        }

        foreach (var sceneNode in _skSvg.HitTestSceneNodes(rect))
        {
            yield return sceneNode;
        }
    }

    public IEnumerable<SvgSceneNode> HitTestSceneNodes(Point point)
    {
        if (!TryGetPicturePoint(point, out var picturePoint))
        {
            yield break;
        }

        foreach (var sceneNode in HitTestSceneNodes(picturePoint))
        {
            yield return sceneNode;
        }
    }

    public IEnumerable<SvgSceneNode> HitTestSceneNodes(Rect rect)
    {
        if (!TryGetPictureRect(rect, out var pictureRect))
        {
            yield break;
        }

        foreach (var sceneNode in HitTestSceneNodes(pictureRect))
        {
            yield return sceneNode;
        }
    }

    public IEnumerable<element> HitTestElements(SKPoint point)
    {
        if (_skSvg is null)
        {
            yield break;
        }

        var visited = new HashSet<element>();

        foreach (var svgElement in _skSvg.HitTestElements(point))
        {
            if (_elementBySvgElement.TryGetValue(svgElement, out var control) && visited.Add(control))
            {
                yield return control;
            }
        }
    }

    public IEnumerable<element> HitTestElements(SKRect rect)
    {
        if (_skSvg is null)
        {
            yield break;
        }

        var visited = new HashSet<element>();

        foreach (var svgElement in _skSvg.HitTestElements(rect))
        {
            if (_elementBySvgElement.TryGetValue(svgElement, out var control) && visited.Add(control))
            {
                yield return control;
            }
        }
    }

    public IEnumerable<element> HitTestElements(Point point)
    {
        if (!TryGetPicturePoint(point, out var picturePoint))
        {
            yield break;
        }

        foreach (var control in HitTestElements(picturePoint))
        {
            yield return control;
        }
    }

    public IEnumerable<element> HitTestElements(Rect rect)
    {
        if (!TryGetPictureRect(rect, out var pictureRect))
        {
            yield break;
        }

        foreach (var control in HitTestElements(pictureRect))
        {
            yield return control;
        }
    }

    public Rect GetControlBounds(element element)
    {
        if (element is null)
        {
            return default;
        }

        return TryGetPictureToControlMatrix(out var matrix)
            ? element.GetControlBounds(matrix)
            : default;
    }

    public element? GetElementForSceneNode(SvgSceneNode? sceneNode)
    {
        if (sceneNode is null)
        {
            return null;
        }

        return _elementBySceneNode.TryGetValue(sceneNode, out var control)
            ? control
            : null;
    }

    protected override void Invalidate()
    {
        base.Invalidate();

        // TODO: Only invalidate SvgSource if its Svg property that changed.

        if (IsLoaded)
        {
            OnSourceChanged(this);
        }
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == CssProperty)
        {
            var css = change.GetNewValue<string?>();
            OnCssChanged(css);
        }

        if (change.Property == CurrentCssProperty)
        {
            var currentCss = change.GetNewValue<string?>();
            OnCurrentCssChanged(currentCss);
        }

        if (change.Property == ClipToBoundsProperty)
        {
            InvalidateVisual();
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        OnSourceChanged(this);
    }

    private void OnCssChanged(string? css)
    {
        var source = this;
        var currentCss = GetCurrentCss(this);
        var parameters = new SvgParameters(null, string.Concat(css, ' ', currentCss));

        if (!Load(source, parameters))
        {
            Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this, "Failed to load svg image.");
        }

        InvalidateMeasure();
        InvalidateVisual();
    }

    private void OnCurrentCssChanged(string? currentCss)
    {
        var source = this;
        var css = GetCss(this);
        var parameters = new SvgParameters(null, string.Concat(css, ' ', currentCss));

        if (!Load(source, parameters))
        {
            Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this, "Failed to load svg image.");
        }

        InvalidateMeasure();
        InvalidateVisual();
    }

    private void OnSourceChanged(svg? source)
    {
        var css = GetCss(this);
        var currentCss = GetCurrentCss(this);
        var parameters = new SvgParameters(null, string.Concat(css, ' ', currentCss));

        if (!Load(source, parameters))
        {
            Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this, "Failed to load svg image.");
        }

        InvalidateMeasure();
        InvalidateVisual();
    }
}
