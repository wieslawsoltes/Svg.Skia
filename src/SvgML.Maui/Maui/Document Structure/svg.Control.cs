using System.Numerics;
using Microsoft.Maui.Graphics;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using Svg;
using Svg.Model;
using Svg.Skia;
using MauiPoint = Microsoft.Maui.Graphics.Point;
using MauiRect = Microsoft.Maui.Graphics.Rect;
using ShimPoint = ShimSkiaSharp.SKPoint;
using ShimRect = ShimSkiaSharp.SKRect;

namespace SvgML;

public partial class svg
{
    static svg()
    {
        Initialize();
    }

    public svg()
    {
        AttachToTree(parent: null, root: this);
        Loaded += OnLoaded;
    }

    internal void InvalidateSvgTree()
    {
        if (!IsLoaded)
        {
            return;
        }

        ReloadAndInvalidate();
    }

    public bool TryGetPicturePoint(MauiPoint point, out ShimPoint picturePoint)
    {
        picturePoint = default;

        if (!TryGetRenderInfo(out var renderInfo)
            || !renderInfo.TryMapToPicture(new SvgPoint(point.X, point.Y), out var mappedPoint))
        {
            return false;
        }

        picturePoint = new ShimPoint((float)mappedPoint.X, (float)mappedPoint.Y);
        return true;
    }

    public bool TryGetPictureRect(MauiRect rect, out ShimRect pictureRect)
    {
        pictureRect = default;

        if (!TryGetRenderInfo(out var renderInfo)
            || !renderInfo.TryMapToPicture(new SvgRect(rect.X, rect.Y, rect.Width, rect.Height), out var mappedRect))
        {
            return false;
        }

        pictureRect = new ShimRect(
            (float)mappedRect.Left,
            (float)mappedRect.Top,
            (float)mappedRect.Right,
            (float)mappedRect.Bottom);
        return true;
    }

    public IEnumerable<SvgElement> HitTestSvgElements(ShimPoint point)
    {
        if (_skSvg is null)
        {
            return Array.Empty<SvgElement>();
        }

        return _skSvg.HitTestElements(point);
    }

    public IEnumerable<SvgElement> HitTestSvgElements(ShimRect rect)
    {
        if (_skSvg is null)
        {
            return Array.Empty<SvgElement>();
        }

        return _skSvg.HitTestElements(rect);
    }

    public IEnumerable<SvgElement> HitTestSvgElements(MauiPoint point)
    {
        if (_skSvg is { } skSvg && TryGetPicturePoint(point, out var picturePoint))
        {
            return skSvg.HitTestElements(picturePoint);
        }

        return Array.Empty<SvgElement>();
    }

    public IEnumerable<SvgElement> HitTestSvgElements(MauiRect rect)
    {
        if (_skSvg is { } skSvg && TryGetPictureRect(rect, out var pictureRect))
        {
            return skSvg.HitTestElements(pictureRect);
        }

        return Array.Empty<SvgElement>();
    }

    public IEnumerable<element> HitTestElements(ShimPoint point)
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

    public IEnumerable<element> HitTestElements(ShimRect rect)
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

    public IEnumerable<element> HitTestElements(MauiPoint point)
    {
        if (!TryGetPicturePoint(point, out var picturePoint))
        {
            yield break;
        }

        foreach (var element in HitTestElements(picturePoint))
        {
            yield return element;
        }
    }

    public IEnumerable<element> HitTestElements(MauiRect rect)
    {
        if (!TryGetPictureRect(rect, out var pictureRect))
        {
            yield break;
        }

        foreach (var element in HitTestElements(pictureRect))
        {
            yield return element;
        }
    }

    public IEnumerable<SvgSceneNode> HitTestSceneNodes(ShimPoint point)
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

    public IEnumerable<SvgSceneNode> HitTestSceneNodes(ShimRect rect)
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

    public IEnumerable<SvgSceneNode> HitTestSceneNodes(MauiPoint point)
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

    public IEnumerable<SvgSceneNode> HitTestSceneNodes(MauiRect rect)
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

    public MauiRect GetControlBounds(element element)
    {
        if (element is null || !TryGetRenderInfo(out var renderInfo))
        {
            return default;
        }

        return element.GetControlBounds(renderInfo.Matrix);
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

    public element? GetElementForSvgElement(SvgElement? svgElement)
    {
        if (svgElement is null)
        {
            return null;
        }

        return _elementBySvgElement.TryGetValue(svgElement, out var control)
            ? control
            : null;
    }

    protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
    {
        var picture = _picture;
        if (picture is null)
        {
            return new Size();
        }

        var size = SvgRenderLayout.CalculateSize(
            new SvgSize(widthConstraint, heightConstraint),
            new SvgSize(picture.CullRect.Width, picture.CullRect.Height),
            Stretch,
            StretchDirection);

        return new Size(size.Width, size.Height);
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);
        Render(e.Surface.Canvas, e.Info.Width, e.Info.Height);
    }

    private void Render(SKCanvas canvas, int width, int height)
    {
        var picture = _picture;
        if (picture is null)
        {
            return;
        }

        if (!SvgRenderLayout.TryCreateRenderInfo(
                new SvgSize(width, height),
                new SvgRect(picture.CullRect.Left, picture.CullRect.Top, picture.CullRect.Width, picture.CullRect.Height),
                Stretch,
                StretchDirection,
                out var renderInfo))
        {
            return;
        }

        canvas.Save();
        canvas.ClipRect(ToSKRect(renderInfo.DestinationRect));
        var matrix = ToSKMatrix(renderInfo.Matrix);
        canvas.Concat(ref matrix);
        canvas.DrawPicture(picture);
        canvas.Restore();
    }

    protected override void OnPropertyChanged(string propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == nameof(Css))
        {
            ReloadAndInvalidate();
            return;
        }

        if (propertyName == nameof(CurrentCss))
        {
            ReloadAndInvalidate();
            return;
        }

        if (propertyName == nameof(Stretch) || propertyName == nameof(StretchDirection))
        {
            InvalidateMeasure();
            InvalidateSurface();
            return;
        }

        if (IsLoaded)
        {
            ReloadAndInvalidate();
        }
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        ReloadAndInvalidate();
    }

    private void ReloadAndInvalidate()
    {
        OnSourceChanged(this);
        InvalidateMeasure();
        InvalidateSurface();
    }

    private void OnSourceChanged(svg? source)
    {
        var parameters = BuildParameters(GetCss(this), GetCurrentCss(this));
        Load(source, parameters);
    }

    private bool TryGetRenderInfo(out SvgRenderInfo renderInfo)
    {
        renderInfo = default;

        var picture = _picture;
        if (picture is null)
        {
            return false;
        }

        return SvgRenderLayout.TryCreateRenderInfo(
            new SvgSize(Width, Height),
            new SvgRect(picture.CullRect.Left, picture.CullRect.Top, picture.CullRect.Width, picture.CullRect.Height),
            Stretch,
            StretchDirection,
            out renderInfo);
    }

    private static SKRect ToSKRect(SvgRect rect)
    {
        return new SKRect((float)rect.Left, (float)rect.Top, (float)rect.Right, (float)rect.Bottom);
    }

    private static SKMatrix ToSKMatrix(Matrix3x2 matrix)
    {
        return new SKMatrix
        {
            ScaleX = matrix.M11,
            SkewX = matrix.M21,
            TransX = matrix.M31,
            SkewY = matrix.M12,
            ScaleY = matrix.M22,
            TransY = matrix.M32,
            Persp0 = 0,
            Persp1 = 0,
            Persp2 = 1
        };
    }
}
