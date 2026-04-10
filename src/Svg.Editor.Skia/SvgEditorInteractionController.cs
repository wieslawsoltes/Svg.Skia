using System;
using System.Collections.Generic;
using Svg;
using Svg.Skia;
using Shim = ShimSkiaSharp;
using SK = SkiaSharp;

namespace Svg.Editor.Skia;

public class SvgEditorInteractionController
{
    public SvgEditorInteractionController()
        : this(new SelectionService(), new PathService())
    {
    }

    public SvgEditorInteractionController(SelectionService selectionService, PathService pathService)
    {
        SelectionService = selectionService ?? throw new ArgumentNullException(nameof(selectionService));
        PathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    }

    public SelectionService SelectionService { get; }
    public PathService PathService { get; }

    public bool SnapToGrid
    {
        get => SelectionService.SnapToGrid;
        set => SelectionService.SnapToGrid = value;
    }

    public double GridSize
    {
        get => SelectionService.GridSize;
        set => SelectionService.GridSize = value;
    }

    public bool IsPathEditing => PathService.IsEditing;
    public SvgPath? EditPath => PathService.EditPath;

    public SvgSceneNode? EditSceneNode
    {
        get => PathService.EditSceneNode;
        set => PathService.EditSceneNode = value;
    }

    public IReadOnlyList<PathPoint> PathPoints => PathService.PathPoints;

    public int ActivePathPoint
    {
        get => PathService.ActivePoint;
        set => PathService.ActivePoint = value;
    }

    public Shim.SKMatrix PathMatrix => PathService.PathMatrix;
    public Shim.SKMatrix PathInverse => PathService.PathInverse;

    public PathService.SegmentTool CurrentSegmentTool
    {
        get => PathService.CurrentSegmentTool;
        set => PathService.CurrentSegmentTool = value;
    }

    public BoundsInfo GetBoundsInfo(SvgSceneNode sceneNode, Func<float> getScale)
        => SelectionService.GetBoundsInfo(sceneNode, getScale);

    public int HitHandle(BoundsInfo bounds, SK.SKPoint point, float scale, out SK.SKPoint center)
        => SelectionService.HitHandle(bounds, point, scale, out center);

    public int HitPolyPoint(IList<Shim.SKPoint> points, Shim.SKMatrix matrix, SK.SKPoint point, float scale)
        => SelectionService.HitPolyPoint(points, matrix, point, scale);

    public float GetRotation(SvgVisualElement? element)
        => SelectionService.GetRotation(element);

    public void SetRotation(SvgVisualElement element, float angle, SK.SKPoint center)
        => SelectionService.SetRotation(element, angle, center);

    public (float X, float Y) GetTranslation(SvgVisualElement? element)
        => SelectionService.GetTranslation(element);

    public void SetTranslation(SvgVisualElement element, float x, float y)
        => SelectionService.SetTranslation(element, x, y);

    public (float X, float Y) GetScale(SvgVisualElement? element)
        => SelectionService.GetScale(element);

    public void SetScale(SvgVisualElement element, float x, float y)
        => SelectionService.SetScale(element, x, y);

    public (float X, float Y) GetSkew(SvgVisualElement? element)
        => SelectionService.GetSkew(element);

    public void SetSkew(SvgVisualElement element, float x, float y)
        => SelectionService.SetSkew(element, x, y);

    public void FlipHorizontal(SvgVisualElement element, SK.SKPoint center)
        => SelectionService.FlipHorizontal(element, center);

    public void FlipVertical(SvgVisualElement element, SK.SKPoint center)
        => SelectionService.FlipVertical(element, center);

    public float Snap(float value)
        => SelectionService.Snap(value);

    public void ResizeElement(
        SvgVisualElement element,
        int handle,
        float dx,
        float dy,
        SK.SKRect startRect,
        float startTransX,
        float startTransY,
        float startScaleX,
        float startScaleY)
        => SelectionService.ResizeElement(
            element,
            handle,
            dx,
            dy,
            startRect,
            startTransX,
            startTransY,
            startScaleX,
            startScaleY);

    public void StartPathEditing(SvgPath path, SvgSceneNode sceneNode)
        => PathService.Start(path, sceneNode);

    public void StopPathEditing()
        => PathService.Stop();

    public void SetPathEditTransform(Shim.SKMatrix matrix)
        => PathService.SetEditTransform(matrix);

    public void AddPathPoint(Shim.SKPoint point)
        => PathService.AddPoint(point);

    public void RemoveActivePathPoint()
        => PathService.RemoveActivePoint();

    public void MoveActivePathPoint(Shim.SKPoint point)
        => PathService.MoveActivePoint(point);

    public void MakePathPointSmooth(int index)
        => PathService.MakeSmooth(index);

    public void MakePathPointCorner(int index)
        => PathService.MakeCorner(index);

    public int HitPathPoint(SK.SKPoint point, float handleSize, float scale)
        => PathService.HitPoint(point, handleSize, scale);
}
