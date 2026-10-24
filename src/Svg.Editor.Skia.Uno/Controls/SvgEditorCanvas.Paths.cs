using System.Diagnostics;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using Svg.Editor.Core;
using Svg.Editor.Svg;
using Svg.Pathing;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using Windows.System;
using Shim = ShimSkiaSharp;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorCanvas
{
    public int ActiveNodeIndex => _vertexOriginal is not null ? _vertexIndex : _nodePreview.ActivePoint;

    public void EditActiveNode(bool smooth)
    {
        if (_vertexOriginal is not null)
        {
            if (smooth)
                throw new InvalidOperationException("Convert the polygon or polyline to a path before smoothing a vertex.");
            return;
        }

        ApplyToActiveNode(smooth ? "Make path node smooth" : "Make path node a corner", paths =>
        {
            if (smooth)
            {
                paths.MakeSmooth(paths.ActivePoint);
            }
            else
            {
                paths.MakeCorner(paths.ActivePoint);
            }
        });
    }

    public void DeleteActiveNode()
    {
        if (_vertexOriginal is not null)
            DeleteActiveVertex();
        else
        {
            var nextIndex = -1;
            ApplyToActiveNode("Delete path node", paths =>
            {
                paths.RemoveActivePoint();
                nextIndex = paths.ActivePoint;
            });
            _nodePreview.ActivePoint = Math.Min(nextIndex, _nodePreview.PathPoints.Count - 1);
            Invalidate();
        }
    }

    private void ApplyToActiveNode(string label, Action<PathService> mutation)
    {
        var original = _nodeOriginal;
        var index = _nodePreview.ActivePoint;
        if (!CanEditSelection || Controller is null || original is null || SvgEditorController.IsEditLocked(original)
            || index < 0 || Controller.GetSceneNode(original) is not { } scene)
        {
            return;
        }

        Controller.Execute(label, () =>
        {
            try
            {
                Controller.Paths.Start(original, scene);
                Controller.Paths.ActivePoint = index;
                mutation(Controller.Paths);
            }
            finally
            {
                Controller.Paths.Stop();
            }
        });
    }

    private void CompleteCreation()
    {
        if (Controller?.Document is null)
        {
            return;
        }

        if (IsPenTool(Tool))
        {
            var tangent = _moved ? Subtract(_currentWorld, _startWorld) : default;
            if (_penNodes.Count == 0 || SKPoint.Distance(_penNodes[^1].Position, _startWorld) * Zoom >= 1)
            {
                _penNodes.Add(new PenNode(_startWorld, tangent));
            }
            else if (_moved)
            {
                _penNodes[^1] = new PenNode(_startWorld, tangent);
            }
            _currentWorld = _startWorld;
            return;
        }

        if (Tool == SvgEditorToolKind.Freehand)
        {
            if (_freehandPoints.Count > 1)
            {
                var path = new SvgPath { PathData = new SvgPathSegmentList() };
                var points = Simplify(_freehandPoints, (float)(0.65 / Zoom));
                path.PathData.Add(new SvgMoveToSegment(false, PointF(points[0])));
                for (var i = 1; i < points.Count; i++)
                {
                    path.PathData.Add(new SvgLineSegment(false, PointF(points[i])));
                }

                Controller.AddElement(path, "Draw freehand path");
            }

            _freehandPoints.Clear();
            _freehandPreview?.Reset();
            return;
        }

        if (!_moved && Tool is not (SvgEditorToolKind.Text or SvgEditorToolKind.Symbol or SvgEditorToolKind.TextPath))
        {
            return;
        }

        Controller.CreateElement((ToolService.Tool)(int)Tool, _startWorld, _currentWorld);
    }

    private void CommitPenPath(bool closed)
    {
        if (Controller?.Document is null || _penNodes.Count < 2)
        {
            _penNodes.Clear();
            return;
        }

        SvgVisualElement element;
        if (Tool is SvgEditorToolKind.Polygon or SvgEditorToolKind.Polyline)
        {
            var points = new SvgPointCollection();
            foreach (var node in _penNodes)
            {
                points.Add(new SvgUnit(node.Position.X));
                points.Add(new SvgUnit(node.Position.Y));
            }

            element = Tool == SvgEditorToolKind.Polygon || closed
                ? new SvgPolygon { Points = points } : new SvgPolyline { Points = points };
        }
        else
        {
            var segments = new SvgPathSegmentList
            {
                new SvgMoveToSegment(false, PointF(_penNodes[0].Position))
            };
            for (var i = 1; i < _penNodes.Count; i++)
            {
                AddPenSegment(segments, _penNodes[i - 1], _penNodes[i]);
            }

            if (closed)
            {
                AddPenSegment(segments, _penNodes[^1], _penNodes[0]);
                segments.Add(new SvgClosePathSegment(false));
            }

            element = new SvgPath { PathData = segments };
        }

        _penNodes.Clear();
        Controller.AddElement(element, closed ? "Draw closed path" : "Draw path");
        Invalidate();
    }

    private void AddPenSegment(SvgPathSegmentList segments, PenNode previous, PenNode next)
    {
        switch (Tool)
        {
            case SvgEditorToolKind.PathCubic:
                segments.Add(new SvgCubicCurveSegment(false, PointF(Add(previous.Position, previous.Tangent)),
                    PointF(Subtract(next.Position, next.Tangent)), PointF(next.Position)));
                break;
            case SvgEditorToolKind.PathQuadratic:
                segments.Add(new SvgQuadraticCurveSegment(false,
                    PointF(Add(previous.Position, previous.Tangent)), PointF(next.Position)));
                break;
            case SvgEditorToolKind.PathArc:
                var radius = Math.Max(1, SKPoint.Distance(previous.Position, next.Position) / 2);
                segments.Add(new SvgArcSegment(radius, radius, 0, SvgArcSize.Small, SvgArcSweep.Positive,
                    false, PointF(next.Position)));
                break;
            case SvgEditorToolKind.PathMove:
                segments.Add(new SvgMoveToSegment(false, PointF(next.Position)));
                break;
            default:
                segments.Add(new SvgLineSegment(false, PointF(next.Position)));
                break;
        }
    }

    private SKPath CreatePenPreview()
    {
        using var path = new SKPathBuilder();
        if (_penNodes.Count == 0)
        {
            path.MoveTo(_startWorld);
            return path.Detach();
        }

        path.MoveTo(_penNodes[0].Position);
        for (var i = 1; i < _penNodes.Count; i++)
        {
            Append(_penNodes[i - 1], _penNodes[i]);
        }

        var provisional = _gesture == Gesture.Create
            ? new PenNode(_startWorld, Subtract(_currentWorld, _startWorld))
            : new PenNode(_currentWorld, default);
        Append(_penNodes[^1], provisional);
        return path.Detach();

        void Append(PenNode previous, PenNode next)
        {
            if (Tool == SvgEditorToolKind.PathCubic)
            {
                path.CubicTo(Add(previous.Position, previous.Tangent),
                    Subtract(next.Position, next.Tangent), next.Position);
            }
            else if (Tool == SvgEditorToolKind.PathQuadratic)
            {
                path.QuadTo(Add(previous.Position, previous.Tangent), next.Position);
            }
            else if (Tool == SvgEditorToolKind.PathArc)
            {
                var radius = Math.Max(1, SKPoint.Distance(previous.Position, next.Position) / 2);
                path.ArcTo(radius, radius, 0, SKPathArcSize.Small, SKPathDirection.Clockwise,
                    next.Position.X, next.Position.Y);
            }
            else if (Tool == SvgEditorToolKind.PathMove)
            {
                path.MoveTo(next.Position);
            }
            else
            {
                path.LineTo(next.Position);
            }
        }
    }

    private bool TryBeginNode(SKPoint view)
    {
        if (!CanEditSelection)
        {
            return false;
        }

        if (TryBeginVertex(view))
        {
            return true;
        }

        if (!_nodePreview.IsEditing)
        {
            return false;
        }

        var index = _nodePreview.HitPoint(Viewport.ViewToWorld(view), 12, (float)Zoom);
        if (index < 0)
        {
            return false;
        }

        _nodeIndex = index;
        _nodePreview.ActivePoint = index;
        _gesture = Gesture.Node;
        return true;
    }

    private void CommitNodeMove()
    {
        if (Controller is null || _nodeOriginal is null || SvgEditorController.IsEditLocked(_nodeOriginal) || _nodeIndex < 0
            || Controller.GetSceneNode(_nodeOriginal) is not { } scene)
        {
            return;
        }

        var target = _nodePreview.PathInverse.MapPoint(new Shim.SKPoint(_currentWorld.X, _currentWorld.Y));
        Controller.Execute("Move path node", () =>
        {
            try
            {
                Controller.Paths.Start(_nodeOriginal, scene);
                Controller.Paths.ActivePoint = _nodeIndex;
                Controller.Paths.MoveActivePoint(target);
            }
            finally
            {
                Controller.Paths.Stop();
            }
        });
    }

    private void RefreshNodePreview()
    {
        if (_gesture is Gesture.Node or Gesture.Vertex)
        {
            return;
        }

        RefreshVertexPreview();
        var previousPath = _nodeOriginal;
        var previousIndex = _nodePreview.ActivePoint;
        _nodePreview.Stop();
        _nodeOriginal = null;
        _nodeOutline?.Dispose();
        _nodeOutline = null;
        if (Tool == SvgEditorToolKind.PathSelect && CanEditSelection && Controller?.SelectedElements.Count == 1
            && Controller.SelectedElements[0] is SvgPath path && Controller.GetSceneNode(path) is { } scene)
        {
            // PathService normalizes relative segments. Normalize a clone for inspection so merely
            // selecting a path never changes the saved SVG or silently bypasses undo history.
            _nodeOriginal = path;
            _nodePreview.Start((SvgPath)path.DeepCopy(), scene);
            if (ReferenceEquals(previousPath, path) && previousIndex < _nodePreview.PathPoints.Count)
            {
                _nodePreview.ActivePoint = previousIndex;
            }

            RebuildNodeOutline();
        }
    }

    private void RebuildNodeOutline()
    {
        _nodeOutline?.Dispose();
        _nodeOutline = _nodePreview.EditPath is { } path
            ? SKPath.ParseSvgPathData(path.PathData.ToString()) : null;
    }

    private static List<SKPoint> Simplify(IReadOnlyList<SKPoint> points, float tolerance)
    {
        if (points.Count < 3)
        {
            return points.ToList();
        }

        // Iterative Ramer-Douglas-Peucker bounds history size without recursive stack growth.
        var keep = new bool[points.Count];
        keep[0] = keep[^1] = true;
        var pending = new Stack<(int Start, int End)>();
        pending.Push((0, points.Count - 1));
        var threshold = tolerance * tolerance;
        while (pending.Count > 0)
        {
            var (start, end) = pending.Pop();
            var vector = Subtract(points[end], points[start]);
            var length = vector.X * vector.X + vector.Y * vector.Y;
            var maxDistance = threshold;
            var split = -1;
            for (var i = start + 1; i < end; i++)
            {
                var relative = Subtract(points[i], points[start]);
                var t = length > 0 ? Math.Clamp((relative.X * vector.X + relative.Y * vector.Y) / length, 0, 1) : 0;
                var x = relative.X - t * vector.X;
                var y = relative.Y - t * vector.Y;
                var distance = x * x + y * y;
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    split = i;
                }
            }

            if (split >= 0)
            {
                keep[split] = true;
                pending.Push((start, split));
                pending.Push((split, end));
            }
        }

        var result = new List<SKPoint>();
        for (var i = 0; i < points.Count; i++)
        {
            if (keep[i])
            {
                result.Add(points[i]);
            }
        }

        return result;
    }

}
