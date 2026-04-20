using System.Numerics;
using Microsoft.UI.Xaml;
using Svg;
using Svg.Skia;
using Windows.Foundation;
using ShimPoint = ShimSkiaSharp.SKPoint;
using ShimRect = ShimSkiaSharp.SKRect;

namespace SvgML;

public abstract partial class element
{
    private readonly List<SvgSceneNode> _sceneNodes = new();

    public SvgElement? SvgElement { get; private set; }

    public IReadOnlyList<SvgSceneNode> SceneNodes => _sceneNodes;

    public Rect GeometryBounds { get; private set; }

    public Rect TransformedBounds { get; private set; }

    public new Matrix3x2 TransformMatrix { get; private set; } = Matrix3x2.Identity;

    public Matrix3x2 TotalTransformMatrix { get; private set; } = Matrix3x2.Identity;

    internal string SvgElementName => SvgTag;

    internal void UpdateSvgData(
        SvgElement? svgElement,
        IReadOnlyList<SvgSceneNode> sceneNodes,
        Rect geometryBounds,
        Rect transformedBounds,
        Matrix3x2 transformMatrix,
        Matrix3x2 totalTransformMatrix)
    {
        SvgElement = svgElement;
        GeometryBounds = geometryBounds;
        TransformedBounds = transformedBounds;
        TransformMatrix = transformMatrix;
        TotalTransformMatrix = totalTransformMatrix;

        _sceneNodes.Clear();
        if (sceneNodes.Count > 0)
        {
            _sceneNodes.AddRange(sceneNodes);
        }
    }

    internal void ClearSvgData()
    {
        SvgElement = null;
        GeometryBounds = default;
        TransformedBounds = default;
        TransformMatrix = Matrix3x2.Identity;
        TotalTransformMatrix = Matrix3x2.Identity;
        _sceneNodes.Clear();
    }

    public bool HitTest(ShimPoint point)
    {
        var svgAncestor = RootSvg ?? this as svg;
        if (svgAncestor is null)
        {
            return false;
        }

        if (ReferenceEquals(svgAncestor, this))
        {
            return svgAncestor.HitTestElements(point).Any();
        }

        return svgAncestor.HitTestElements(point).Any(e => ReferenceEquals(e, this));
    }

    public bool HitTest(ShimRect rect)
    {
        var svgAncestor = RootSvg ?? this as svg;
        if (svgAncestor is null)
        {
            return false;
        }

        if (ReferenceEquals(svgAncestor, this))
        {
            return svgAncestor.HitTestElements(rect).Any();
        }

        return svgAncestor.HitTestElements(rect).Any(e => ReferenceEquals(e, this));
    }

    public Rect GetControlBounds(Matrix3x2 pictureToControl)
    {
        if (TransformedBounds.Width <= 0 || TransformedBounds.Height <= 0)
        {
            return default;
        }

        var topLeft = Vector2.Transform(new Vector2((float)TransformedBounds.Left, (float)TransformedBounds.Top), pictureToControl);
        var topRight = Vector2.Transform(new Vector2((float)TransformedBounds.Right, (float)TransformedBounds.Top), pictureToControl);
        var bottomRight = Vector2.Transform(new Vector2((float)TransformedBounds.Right, (float)TransformedBounds.Bottom), pictureToControl);
        var bottomLeft = Vector2.Transform(new Vector2((float)TransformedBounds.Left, (float)TransformedBounds.Bottom), pictureToControl);

        var minX = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomRight.X, bottomLeft.X));
        var minY = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomRight.Y, bottomLeft.Y));
        var maxX = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomRight.X, bottomLeft.X));
        var maxY = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomRight.Y, bottomLeft.Y));

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    public bool HitTest(Point point)
    {
        var svgAncestor = RootSvg ?? this as svg;
        if (svgAncestor is null)
        {
            return false;
        }

        var svgSpacePoint = point;
        if (!ReferenceEquals(svgAncestor, this))
        {
            var transform = TransformToVisual(svgAncestor);
            if (transform is null)
            {
                return false;
            }

            svgSpacePoint = transform.TransformPoint(point);
        }

        if (!svgAncestor.TryGetPicturePoint(svgSpacePoint, out var picturePoint))
        {
            return false;
        }

        if (ReferenceEquals(svgAncestor, this))
        {
            return svgAncestor.HitTestElements(picturePoint).Any();
        }

        return svgAncestor.HitTestElements(picturePoint).Any(e => ReferenceEquals(e, this));
    }
}
