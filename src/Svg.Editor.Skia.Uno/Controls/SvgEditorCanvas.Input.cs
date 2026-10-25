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
    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            HandlePointerPressed(e);
        }
        catch (Exception exception)
        {
            HandleInteractionFailure(exception);
            e.Handled = true;
        }
    }

    private void HandlePointerPressed(PointerRoutedEventArgs e)
    {
        if (_disposed || Controller?.Document is null)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        var view = new SKPoint((float)point.Position.X, (float)point.Position.Y);
        if (e.Pointer.PointerDeviceType == PointerDeviceType.Touch)
        {
            // A resting palm must not cancel an active stylus or mouse stroke.
            if (_activePointer is not null && _activePointerDevice != PointerDeviceType.Touch)
            {
                e.Handled = true;
                return;
            }
            var startedNavigation = _touchNavigation.AddContact(e.Pointer.PointerId, view);
            if (startedNavigation)
            {
                CancelDrawingPreview();
                _gesture = Gesture.Pinch;
                _suppressTouchDoubleTap = true;
                RefreshNodePreview();
                UpdateCursor();
            }
            if (_touchNavigation.IsNavigating)
            {
                CapturePointer(e.Pointer);
                e.Handled = true;
                Invalidate();
                return;
            }
            _suppressTouchDoubleTap = false;
        }
        else if (_touchNavigation.IsNavigating)
        {
            e.Handled = true;
            return;
        }

        InteractionStarting?.Invoke(this, EventArgs.Empty);

        if (point.Properties.IsRightButtonPressed)
        {
            FinishPath();
            e.Handled = true;
            return;
        }

        if (_activePointer is not null)
        {
            return;
        }

        _activePointer = e.Pointer.PointerId;
        _activePointerDevice = e.Pointer.PointerDeviceType;
        _startView = _lastView = view;
        _startWorld = _currentWorld = Snap(Viewport.ViewToWorld(view));
        _shift = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift);
        _additive = _shift || e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control)
            || Tool == SvgEditorToolKind.MultiSelect;
        _moved = false;
        _previewTransform = SKMatrix.Identity;
        CapturePointer(e.Pointer);
        e.Handled = true;

        if (PanMode || point.Properties.IsMiddleButtonPressed)
        {
            _gesture = Gesture.Pan;
            UpdateCursor();
            return;
        }

        if (IsNodeTool(Tool) && TryBeginNode(view))
        {
            return;
        }

        if (IsSelectionTool(Tool))
        {
            var world = Viewport.ViewToWorld(view);
            _handle = HitSelectionHandle(view);
            if (_handle >= 0 && !IsNodeTool(Tool) && Controller.SelectionBounds is { } bounds)
            {
                _startBounds = bounds;
                BeginSelectionGesture(_handle == 8 ? Gesture.Rotate : Gesture.Resize);
                return;
            }

            var selectedBounds = Controller.SelectionBounds;
            // A drag within a multiple selection moves the group; additive clicks still hit-test.
            if (!_additive && CanEditSelection && Controller.SelectedElements.Count > 1
                && selectedBounds?.Contains(world) == true)
            {
                BeginSelectionGesture(Gesture.Move);
                return;
            }

            Controller.SelectAt(world, _additive);
            if (CanEditSelection && Controller.SelectionBounds?.Contains(world) == true)
            {
                BeginSelectionGesture(Gesture.Move);
            }
            else
            {
                _gesture = Gesture.Marquee;
            }

            return;
        }

        if (IsPenTool(Tool) && _penNodes.Count > 1
            && SKPoint.Distance(Viewport.WorldToView(_penNodes[0].Position), view) < 9)
        {
            CommitPenPath(true);
            _activePointer = null;
            _activePointerDevice = null;
            ReleasePointerCapture(e.Pointer);
            return;
        }

        _gesture = Gesture.Create;
        if (Tool == SvgEditorToolKind.Freehand)
        {
            _freehandPoints.Clear();
            _freehandPoints.Add(_startWorld);
            _freehandPreview ??= new SKPathBuilder();
            _freehandPreview.Reset();
            _freehandPreview.MoveTo(_startWorld);
        }

        Invalidate();
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            HandlePointerMoved(e);
        }
        catch (Exception exception)
        {
            HandleInteractionFailure(exception);
            e.Handled = true;
        }
    }

    private void HandlePointerMoved(PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        var view = new SKPoint((float)point.Position.X, (float)point.Position.Y);
        PointerPosition = Viewport.ViewToWorld(view);
        PointerPositionChanged?.Invoke(this, EventArgs.Empty);
        if (_touchNavigation.MoveContact(e.Pointer.PointerId, view, Viewport))
        {
            e.Handled = true;
            return;
        }

        if (_activePointer != e.Pointer.PointerId)
        {
            if (_penNodes.Count > 0)
            {
                _currentWorld = Snap(PointerPosition);
                Invalidate();
            }

            return;
        }

        _shift = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift);
        UpdateGesture(view);
        e.Handled = true;
    }

    private void UpdateGesture(SKPoint view)
    {
        var previousView = _lastView;
        _lastView = view;
        _moved |= SKPoint.Distance(_startView, view) >= 2;
        _currentWorld = Constrain(Snap(Viewport.ViewToWorld(view)));
        var delta = Subtract(_currentWorld, _startWorld);
        switch (_gesture)
        {
            case Gesture.Pan:
                Viewport.Pan(view.X - previousView.X, view.Y - previousView.Y);
                break;
            case Gesture.Move:
                _previewTransform = SKMatrix.CreateTranslation(delta.X, delta.Y);
                break;
            case Gesture.Resize:
                _previewTransform = CreateResizeTransform(delta);
                break;
            case Gesture.Rotate:
                var center = new SKPoint(_startBounds.MidX, _startBounds.MidY);
                var startAngle = Math.Atan2(_startWorld.Y - center.Y, _startWorld.X - center.X);
                var currentAngle = Math.Atan2(_currentWorld.Y - center.Y, _currentWorld.X - center.X);
                var angle = (float)((currentAngle - startAngle) * 180 / Math.PI);
                if (_shift)
                {
                    angle = MathF.Round(angle / 15) * 15;
                }

                _previewTransform = SKMatrix.CreateRotationDegrees(angle, center.X, center.Y);
                break;
            case Gesture.Node:
                var local = _nodePreview.PathInverse.MapPoint(new Shim.SKPoint(_currentWorld.X, _currentWorld.Y));
                _nodePreview.MoveActivePoint(local);
                RebuildNodeOutline();
                break;
            case Gesture.Vertex when _vertexIndex >= 0:
                _vertexPoints[_vertexIndex] = _vertexInverse.MapPoint(_currentWorld);
                break;
            case Gesture.Create when Tool == SvgEditorToolKind.Freehand:
                if (_freehandPoints.Count == 0 || SKPoint.Distance(_freehandPoints[^1], _currentWorld) * Zoom >= 1.5)
                {
                    _freehandPoints.Add(_currentWorld);
                    _freehandPreview?.LineTo(_currentWorld);
                }

                break;
        }

        Invalidate();
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            HandlePointerReleased(e);
        }
        catch (Exception exception)
        {
            HandleInteractionFailure(exception);
            e.Handled = true;
        }
    }

    private void HandlePointerReleased(PointerRoutedEventArgs e)
    {
        if (_touchNavigation.IsNavigating)
        {
            var finalPoint = e.GetCurrentPoint(this).Position;
            _touchNavigation.MoveContact(e.Pointer.PointerId, new SKPoint((float)finalPoint.X, (float)finalPoint.Y), Viewport);
        }
        if (_touchNavigation.RemoveContact(e.Pointer.PointerId))
        {
            _gesture = _touchNavigation.IsNavigating ? Gesture.Pinch : Gesture.None;
            ReleasePointerCapture(e.Pointer);
            UpdateCursor();
            e.Handled = true;
            return;
        }

        if (_activePointer != e.Pointer.PointerId)
        {
            ReleasePointerCapture(e.Pointer);
            return;
        }

        var point = e.GetCurrentPoint(this);
        UpdateGesture(new SKPoint((float)point.Position.X, (float)point.Position.Y));
        var completedGesture = _gesture;
        _gesture = Gesture.None;
        _activePointer = null;
        _activePointerDevice = null;
        ReleasePointerCapture(e.Pointer);
        e.Handled = true;

        if (Controller is not null)
        {
            switch (completedGesture)
            {
                case Gesture.Move when _moved:
                case Gesture.Resize when _moved:
                case Gesture.Rotate when _moved:
                    Controller.TransformSelection(_previewTransform);
                    break;
                case Gesture.Marquee when _moved:
                    Controller.SelectInRect(RectBetween(_startWorld, Viewport.ViewToWorld(_lastView)), _additive);
                    break;
                case Gesture.Create:
                    CompleteCreation();
                    break;
                case Gesture.Node when _moved:
                    CommitNodeMove();
                    break;
                case Gesture.Vertex when _moved:
                    CommitVertexMove();
                    break;
            }
        }

        _previewTransform = SKMatrix.Identity;
        _moved = false;
        ReleaseSelectionGhost();
        RefreshNodePreview();
        UpdateCursor();
        Invalidate();
    }

    private int HitSelectionHandle(SKPoint view)
    {
        if (!CanEditSelection || Controller?.SelectionBounds is not { } bounds)
        {
            return -1;
        }

        for (var i = 0; i < 8; i++)
        {
            if (SKPoint.Distance(Viewport.WorldToView(GetHandle(bounds, i)), view) <= 7)
            {
                return i;
            }
        }

        var top = Viewport.WorldToView(GetHandle(bounds, 1));
        return SKPoint.Distance(new SKPoint(top.X, top.Y - 24), view) <= 8 ? 8 : -1;
    }

    private SKMatrix CreateResizeTransform(SKPoint delta)
    {
        var anchor = GetHandle(_startBounds, (_handle + 4) % 8);
        var dragged = GetHandle(_startBounds, _handle);
        var width = dragged.X - anchor.X;
        var height = dragged.Y - anchor.Y;
        var sx = Math.Abs(width) > 0.0001 ? (width + delta.X) / width : 1;
        var sy = Math.Abs(height) > 0.0001 ? (height + delta.Y) / height : 1;
        if (_handle is 1 or 5)
        {
            sx = 1;
        }
        else if (_handle is 3 or 7)
        {
            sy = 1;
        }

        if (_shift)
        {
            sx = sy = Math.Abs(sx - 1) > Math.Abs(sy - 1) ? sx : sy;
        }

        return SKMatrix.CreateScale(Math.Max(0.001f, sx), Math.Max(0.001f, sy), anchor.X, anchor.Y);
    }

    private SKPoint Constrain(SKPoint point)
    {
        if (!_shift || _gesture is Gesture.Rotate or Gesture.Node or Gesture.Vertex)
        {
            return point;
        }

        var delta = Subtract(point, _startWorld);
        if (_gesture == Gesture.Move)
        {
            return Math.Abs(delta.X) >= Math.Abs(delta.Y)
                ? new SKPoint(point.X, _startWorld.Y) : new SKPoint(_startWorld.X, point.Y);
        }

        if (_gesture == Gesture.Create && Tool is SvgEditorToolKind.Rect or SvgEditorToolKind.Ellipse
            or SvgEditorToolKind.Circle or SvgEditorToolKind.Image)
        {
            var size = Math.Max(Math.Abs(delta.X), Math.Abs(delta.Y));
            return new SKPoint(_startWorld.X + MathF.CopySign(size, delta.X), _startWorld.Y + MathF.CopySign(size, delta.Y));
        }

        if (_gesture == Gesture.Create && Tool == SvgEditorToolKind.Line)
        {
            var angle = Math.Atan2(delta.Y, delta.X);
            angle = Math.Round(angle / (Math.PI / 4)) * Math.PI / 4;
            var distance = Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
            return new SKPoint(_startWorld.X + (float)(Math.Cos(angle) * distance),
                _startWorld.Y + (float)(Math.Sin(angle) * distance));
        }

        return point;
    }

    private SKPoint Snap(SKPoint point) => Controller?.Selection.SnapToGrid == true
        ? new SKPoint(Controller.Selection.Snap(point.X), Controller.Selection.Snap(point.Y)) : point;

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (_activePointer is not null || _touchNavigation.IsNavigating)
        {
            // Keep the gesture's document-space anchors stable until the captured drag ends.
            e.Handled = true;
            return;
        }

        var point = e.GetCurrentPoint(this);
        var delta = point.Properties.MouseWheelDelta;
        var anchor = new SKPoint((float)point.Position.X, (float)point.Position.Y);
        if (point.Properties.IsHorizontalMouseWheel || e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift))
        {
            Viewport.Pan(delta * 0.4, 0);
        }
        else
        {
            Viewport.ZoomAt(Math.Exp(delta * 0.0015), anchor);
        }

        e.Handled = true;
    }

    private void OnPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_activePointer == e.Pointer.PointerId || _touchNavigation.Contains(e.Pointer.PointerId))
        {
            CancelGesture();
            e.Handled = true;
        }
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (_activePointer == e.Pointer.PointerId || _touchNavigation.Contains(e.Pointer.PointerId))
        {
            // Unexpected capture loss cancels the whole gesture, including any
            // remaining contact. Normal releases remove the contact first.
            CancelGesture();
        }
    }

    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (_touchNavigation.IsNavigating || (e.PointerDeviceType == PointerDeviceType.Touch && _suppressTouchDoubleTap))
        {
            e.Handled = true;
            return;
        }

        if (IsNodeTool(Tool) && _vertexOriginal is not null)
        {
            try
            {
                var point = e.GetPosition(this);
                e.Handled = InsertVertexAt(Viewport.ViewToWorld(new SKPoint((float)point.X, (float)point.Y)));
            }
            catch (Exception exception)
            {
                HandleInteractionFailure(exception);
                e.Handled = true;
            }

            return;
        }

        if (IsPenTool(Tool))
        {
            try
            {
                FinishPath();
            }
            catch (Exception exception)
            {
                HandleInteractionFailure(exception);
            }

            e.Handled = true;
        }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Space:
                PanMode = true;
                e.Handled = true;
                break;
            case VirtualKey.Escape:
                CancelGesture();
                e.Handled = true;
                break;
            case VirtualKey.Enter when _penNodes.Count > 0:
                try
                {
                    FinishPath();
                }
                catch (Exception exception)
                {
                    HandleInteractionFailure(exception);
                }

                e.Handled = true;
                break;
        }
    }

    private void OnKeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Space)
        {
            PanMode = false;
            e.Handled = true;
        }
    }

}
