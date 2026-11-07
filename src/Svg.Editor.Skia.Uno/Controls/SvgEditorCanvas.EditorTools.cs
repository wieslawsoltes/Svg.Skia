using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using Svg.Editor.Skia;
using Windows.System;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorCanvas
{
    private SvgEditorInteractionMode _interactionMode;
    private bool _zoomMode, _zoomOutMode, _zoomOutGesture;
    private bool _standalonePanning, _standalonePreviousPan;
    private bool _moveArtboard;
    /// <summary>Lets the host reuse its rich-text editor after text creation or a text double-click.</summary>
    public event EventHandler<SvgEditorTextEditRequestedEventArgs>? TextEditRequested;

    /// <summary>Hosts with a complete shortcut router opt out of the canvas's minimal key handling.</summary>
    public bool KeyboardShortcutsManagedExternally { get; set; }
    /// <summary>Temporarily overrides pointer input for zooming without changing the selected tool.</summary>
    public bool ZoomMode { get => _zoomMode; set { _zoomMode = value; UpdateCursor(); } }
    public bool ZoomOutMode { get => _zoomOutMode; set { _zoomOutMode = value; UpdateCursor(); } }
    public bool StrokePaintTarget { get; set; }
    public SvgBrushDefinition? ActiveBrush { get; set; }
    public int ShapeDivisions { get; set; } = 6;
    public SvgEditorInteractionMode InteractionMode
    {
        get => _interactionMode;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_interactionMode == value)
                return;
            CancelGesture();
            _interactionMode = value;
            UpdateCursor();
            Invalidate();
        }
    }

    private void RestoreStandaloneNavigation()
    {
        if (!_standalonePanning)
            return;
        _standalonePanning = false;
        PanMode = _standalonePreviousPan;
    }

    private bool TryBeginEditorGesture(PointerRoutedEventArgs args, SKPoint view)
    {
        if (ZoomMode || InteractionMode == SvgEditorInteractionMode.Zoom)
        {
            _gesture = Gesture.Zoom;
            _zoomOutGesture = ZoomOutMode || args.KeyModifiers.HasFlag(VirtualKeyModifiers.Menu);
            _startWorld = _currentWorld = Viewport.ViewToWorld(view);
            return true;
        }
        if (TryBeginAnchorGesture(view) || TryBeginCuttingGesture(args, view))
            return true;
        if (InteractionMode == SvgEditorInteractionMode.Eyedropper)
        {
            _gesture = Gesture.None;
            Controller?.SampleAppearanceAt(Viewport.ViewToWorld(view), _shift, StrokePaintTarget);
            return true;
        }
        if (InteractionMode is SvgEditorInteractionMode.Rotate or SvgEditorInteractionMode.Scale or SvgEditorInteractionMode.Reflect)
        {
            if (Controller?.SelectedElements.Count == 0)
                Controller.SelectAt(Viewport.ViewToWorld(view));
            if (CanEditSelection && Controller?.SelectionBounds is { } bounds)
            {
                _startBounds = bounds;
                BeginSelectionGesture(Gesture.Transform);
            }
            return true;
        }
        if (InteractionMode == SvgEditorInteractionMode.Gradient)
        {
            if (Controller?.SelectedElements.Count == 0)
                Controller.SelectAt(Viewport.ViewToWorld(view));
            if (CanEditSelection)
                _gesture = Gesture.Gradient;
            return true;
        }
        if (InteractionMode == SvgEditorInteractionMode.Artboard)
        {
            _gesture = Gesture.Artboard;
            _startBounds = Controller?.Renderer.Picture?.CullRect ?? default;
            _handle = -1;
            for (var index = 0; index < 8; index++)
                if (SKPoint.Distance(Viewport.WorldToView(GetHandle(_startBounds, index)), view) <= 8)
                { _handle = index; break; }
            _moveArtboard = _handle < 0 && _startBounds.Contains(_startWorld);
            return true;
        }
        if (InteractionMode == SvgEditorInteractionMode.AreaType)
        {
            _gesture = Gesture.AreaType;
            return true;
        }
        if (SvgEditorToolGeometry.IsShape(InteractionMode))
        {
            _gesture = Gesture.EditorShape;
            return true;
        }
        return false;
    }

    private bool UpdateEditorGesture()
    {
        if (UpdateAnchorGesture() || UpdateCuttingGesture())
            return true;
        if (_gesture != Gesture.Transform)
            return false;
        if (_moved)
            _previewTransform = SvgEditorToolGeometry.CreateTransform(InteractionMode, _startBounds, _startWorld, _currentWorld, _shift);
        return true;
    }

    private bool CompleteEditorGesture(Gesture gesture)
    {
        if (CompleteAnchorGesture(gesture) || CompleteCuttingGesture(gesture))
            return true;
        switch (gesture)
        {
            case Gesture.Transform:
                if (_moved)
                    Controller?.TransformSelection(_previewTransform);
                return true;
            case Gesture.Zoom:
                if (_moved && !_zoomOutGesture)
                    Viewport.Fit(RectBetween(_startWorld, Viewport.ViewToWorld(_lastView)), 8, SvgEditorViewport.MaximumZoom);
                else
                    Viewport.ZoomAt(_zoomOutGesture ? 0.5 : 2, _startView);
                return true;
            case Gesture.Gradient:
                if (_moved)
                    Controller?.ApplyGradientLine(_startWorld, _currentWorld, StrokePaintTarget);
                return true;
            case Gesture.Artboard:
                if (_moved)
                {
                    var bounds = GetArtboardPreview();
                    if (bounds.Width * Zoom >= 4 && bounds.Height * Zoom >= 4)
                    {
                        Controller?.SetArtboardBounds(bounds);
                        FitDocument();
                    }
                }
                return true;
            case Gesture.EditorShape:
                if (_moved)
                    Controller?.AddEditorShape(InteractionMode, _startWorld, _currentWorld, Math.Clamp(ShapeDivisions, 3, 64));
                return true;
            case Gesture.AreaType:
                if (_moved && Controller is { } controller)
                {
                    var bounds = RectBetween(_startWorld, _currentWorld);
                    if (bounds.Width * Zoom >= 4 && bounds.Height * Zoom >= 4)
                    {
                        var frame = controller.AddTextArea(bounds, "Your text here.");
                        RequestTextEdit(frame, true);
                    }
                }
                return true;
            default:
                return false;
        }
    }

    private SKRect GetArtboardPreview()
    {
        if (_handle >= 0)
        {
            var anchor = GetHandle(_startBounds, (_handle + 4) % 8);
            var current = _currentWorld;
            if (_handle is 1 or 5)
                return new SKRect(_startBounds.Left, Math.Min(anchor.Y, current.Y), _startBounds.Right, Math.Max(anchor.Y, current.Y));
            if (_handle is 3 or 7)
                return new SKRect(Math.Min(anchor.X, current.X), _startBounds.Top, Math.Max(anchor.X, current.X), _startBounds.Bottom);
            if (_shift && _startBounds.Height > 0)
            {
                var delta = Subtract(current, anchor);
                var ratio = _startBounds.Width / _startBounds.Height;
                if (Math.Abs(delta.X) > Math.Abs(delta.Y * ratio))
                    delta.Y = MathF.CopySign(Math.Abs(delta.X / ratio), delta.Y);
                else
                    delta.X = MathF.CopySign(Math.Abs(delta.Y * ratio), delta.X);
                current = Add(anchor, delta);
            }
            return RectBetween(anchor, current);
        }
        if (!_moveArtboard)
            return RectBetween(_startWorld, _currentWorld);
        var movement = Subtract(_currentWorld, _startWorld);
        return new SKRect(_startBounds.Left + movement.X, _startBounds.Top + movement.Y,
            _startBounds.Right + movement.X, _startBounds.Bottom + movement.Y);
    }

    private void DrawEditorGesture(SKCanvas canvas, CanvasResources resources)
    {
        DrawCuttingGesture(canvas, resources);
        DrawAnchorGesture(canvas, resources);
        resources.Stroke.Color = resources.Accent;
        resources.Stroke.StrokeWidth = 1;
        resources.Fill.Color = resources.Accent.WithAlpha(18);
        if (_gesture == Gesture.Zoom && _moved && !_zoomOutGesture)
        {
            var rect = RectBetween(_startView, _lastView);
            canvas.DrawRect(rect, resources.Fill);
            canvas.DrawRect(rect, resources.Stroke);
        }
        if (_gesture == Gesture.Transform && Controller?.SelectionBounds is { } selection)
        {
            var center = Viewport.WorldToView(new SKPoint(selection.MidX, selection.MidY));
            canvas.DrawLine(center.X - 5, center.Y, center.X + 5, center.Y, resources.Stroke);
            canvas.DrawLine(center.X, center.Y - 5, center.X, center.Y + 5, resources.Stroke);
            if (InteractionMode == SvgEditorInteractionMode.Reflect)
                canvas.DrawLine(center, Viewport.WorldToView(_currentWorld), resources.Stroke);
        }
        if (_gesture == Gesture.Gradient)
        {
            var start = Viewport.WorldToView(_startWorld);
            var end = Viewport.WorldToView(_currentWorld);
            resources.Stroke.StrokeWidth = 2;
            canvas.DrawLine(start, end, resources.Stroke);
            resources.Fill.Color = SKColors.White;
            canvas.DrawCircle(start, 4, resources.Fill);
            canvas.DrawCircle(start, 4, resources.Stroke);
            canvas.DrawCircle(end, 4, resources.Fill);
            canvas.DrawCircle(end, 4, resources.Stroke);
        }
        if (_gesture == Gesture.AreaType)
        {
            var bounds = RectBetween(Viewport.WorldToView(_startWorld), Viewport.WorldToView(_currentWorld));
            canvas.DrawRect(bounds, resources.Fill);
            canvas.DrawRect(bounds, resources.Stroke);
            for (var line = bounds.Top + 14; line < bounds.Bottom - 6; line += 8)
                canvas.DrawLine(bounds.Left + 6, line, bounds.Right - 6, line, resources.Stroke);
        }
        if (_gesture == Gesture.EditorShape)
        {
            using var shape = SvgEditorToolGeometry.CreateShape(InteractionMode, _startWorld, _currentWorld, Math.Clamp(ShapeDivisions, 3, 64));
            using var restore = new SKAutoCanvasRestore(canvas);
            canvas.Concat(Viewport.Matrix);
            resources.Stroke.StrokeWidth = (float)(1.5 / Zoom);
            if (InteractionMode is SvgEditorInteractionMode.RoundedRectangle or SvgEditorInteractionMode.Star or SvgEditorInteractionMode.RegularPolygon)
                canvas.DrawPath(shape, resources.Fill);
            canvas.DrawPath(shape, resources.Stroke);
        }
        if (InteractionMode == SvgEditorInteractionMode.Artboard && Controller?.Renderer.Picture is { } picture)
        {
            var rect = _gesture == Gesture.Artboard && _moved ? GetArtboardPreview() : picture.CullRect;
            var area = new SKRect(Viewport.WorldToView(new SKPoint(rect.Left, rect.Top)).X,
                Viewport.WorldToView(new SKPoint(rect.Left, rect.Top)).Y,
                Viewport.WorldToView(new SKPoint(rect.Right, rect.Bottom)).X,
                Viewport.WorldToView(new SKPoint(rect.Right, rect.Bottom)).Y);
            resources.Stroke.StrokeWidth = 1.5f;
            canvas.DrawRect(area, resources.Stroke);
            resources.Fill.Color = SKColors.White;
            for (var index = 0; index < 8; index++)
            {
                var handle = Viewport.WorldToView(GetHandle(rect, index));
                var bounds = SKRect.Create(handle.X - 3, handle.Y - 3, 6, 6);
                canvas.DrawRect(bounds, resources.Fill);
                canvas.DrawRect(bounds, resources.Stroke);
            }
            canvas.DrawText($"{rect.Width:0.#} × {rect.Height:0.#}", area.Left + 8, area.Top + 18,
                SKTextAlign.Left, resources.LabelFont, resources.Stroke);
        }
    }

    private void RequestTextEdit(SvgVisualElement element, bool area)
    {
        if (Controller is { } controller)
            TextEditRequested?.Invoke(this, new SvgEditorTextEditRequestedEventArgs(controller, element, area));
    }
}

public sealed class SvgEditorTextEditRequestedEventArgs : EventArgs
{
    public SvgEditorTextEditRequestedEventArgs(SvgEditorController controller, SvgVisualElement element, bool area)
    { Controller = controller; Element = element; IsArea = area; }
    public SvgEditorController Controller { get; }
    public SvgVisualElement Element { get; }
    public bool IsArea { get; }
}
