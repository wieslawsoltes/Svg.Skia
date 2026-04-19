using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Rendering;
using Avalonia.VisualTree;
using ShimSkiaSharp;
using Svg;
using Svg.Skia;

namespace SvgML;

public abstract partial class element : ICustomHitTest
{
    private readonly List<SvgSceneNode> _sceneNodes = new();

    /// <summary>
    /// Gets the SVG element backing this control.
    /// </summary>
    public SvgElement? SvgElement { get; private set; }

    /// <summary>
    /// Gets retained scene nodes associated with this control.
    /// </summary>
    public IReadOnlyList<SvgSceneNode> SceneNodes => _sceneNodes;

    /// <summary>
    /// Gets the geometry bounds in picture coordinates.
    /// </summary>
    public Rect GeometryBounds { get; private set; }

    /// <summary>
    /// Gets the transformed bounds in picture coordinates.
    /// </summary>
    public Rect TransformedBounds { get; private set; }

    /// <summary>
    /// Gets the local transform applied to the element.
    /// </summary>
    public Matrix TransformMatrix { get; private set; } = Matrix.Identity;

    /// <summary>
    /// Gets the accumulated transform for the element.
    /// </summary>
    public Matrix TotalTransformMatrix { get; private set; } = Matrix.Identity;

    internal string SvgElementName => SvgTag;

    internal void UpdateSvgData(
        SvgElement? svgElement,
        IReadOnlyList<SvgSceneNode> sceneNodes,
        Rect geometryBounds,
        Rect transformedBounds,
        Matrix transformMatrix,
        Matrix totalTransformMatrix)
    {
        SvgElement = svgElement;
        GeometryBounds = geometryBounds;
        TransformedBounds = transformedBounds;
        TransformMatrix = transformMatrix;
        TotalTransformMatrix = totalTransformMatrix;

        _sceneNodes.Clear();
        if (sceneNodes is { Count: > 0 })
        {
            _sceneNodes.AddRange(sceneNodes);
        }
    }

    internal void ClearSvgData()
    {
        SvgElement = null;
        GeometryBounds = default;
        TransformedBounds = default;
        TransformMatrix = Matrix.Identity;
        TotalTransformMatrix = Matrix.Identity;
        _sceneNodes.Clear();
    }

    /// <summary>
    /// Performs hit testing against the element in picture coordinates.
    /// </summary>
    public bool HitTest(SKPoint point)
    {
        svg? svgAncestor = this as svg ?? this.FindAncestorOfType<svg>();
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

    /// <summary>
    /// Performs hit testing against the element using a rectangle in picture coordinates.
    /// </summary>
    public bool HitTest(SKRect rect)
    {
        svg? svgAncestor = this as svg ?? this.FindAncestorOfType<svg>();
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

    /// <summary>
    /// Transforms the picture-space bounds into control coordinates using the provided matrix.
    /// </summary>
    public Rect GetControlBounds(Matrix pictureToControl)
    {
        if (TransformedBounds.Width <= 0 || TransformedBounds.Height <= 0)
        {
            return default;
        }

        var tl = pictureToControl.Transform(TransformedBounds.TopLeft);
        var tr = pictureToControl.Transform(TransformedBounds.TopRight);
        var br = pictureToControl.Transform(TransformedBounds.BottomRight);
        var bl = pictureToControl.Transform(TransformedBounds.BottomLeft);

        var minX = Math.Min(Math.Min(tl.X, tr.X), Math.Min(br.X, bl.X));
        var minY = Math.Min(Math.Min(tl.Y, tr.Y), Math.Min(br.Y, bl.Y));
        var maxX = Math.Max(Math.Max(tl.X, tr.X), Math.Max(br.X, bl.X));
        var maxY = Math.Max(Math.Max(tl.Y, tr.Y), Math.Max(br.Y, bl.Y));

        return new Rect(new Point(minX, minY), new Point(maxX, maxY));
    }

    bool ICustomHitTest.HitTest(Point point)
    {
        svg? svgAncestor;
        Point svgSpacePoint;

        if (this is svg svgRoot)
        {
            svgAncestor = svgRoot;
            svgSpacePoint = point;
        }
        else
        {
            svgAncestor = this.FindAncestorOfType<svg>();
            if (svgAncestor is null)
            {
                return false;
            }

            var translated = this.TranslatePoint(point, svgAncestor);
            if (translated is null)
            {
                return false;
            }

            svgSpacePoint = translated.Value;
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
