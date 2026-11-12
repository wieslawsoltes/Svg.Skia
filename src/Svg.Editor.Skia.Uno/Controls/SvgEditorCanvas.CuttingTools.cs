using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using Svg.Editor.Skia;
using Windows.System;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorCanvas
{
    private SvgEditorSelectionBehavior _lassoSelectionBehavior;
    private float _eraserDiameter = 20;
    public SvgMagicWandOptions MagicWandOptions { get; set; } = new();
    public bool LassoFullyContained { get; set; }
    /// <summary>The eraser's diameter in picture coordinates; zoom scales its visible footprint.</summary>
    public float EraserDiameter
    {
        get => _eraserDiameter;
        set
        {
            if (!float.IsFinite(value) || value <= 0 || value > 10000)
                throw new ArgumentOutOfRangeException(nameof(value));
            _eraserDiameter = value;
            InvalidateInteraction();
        }
    }

    private bool TryBeginCuttingGesture(PointerRoutedEventArgs args, SKPoint view)
    {
        var behavior = args.KeyModifiers.HasFlag(VirtualKeyModifiers.Menu) ? SvgEditorSelectionBehavior.Subtract
            : args.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift) ? SvgEditorSelectionBehavior.Add : SvgEditorSelectionBehavior.Replace;
        if (InteractionMode == SvgEditorInteractionMode.MagicWand)
        {
            Controller?.SelectByAppearanceAt(Viewport.ViewToWorld(view), MagicWandOptions, behavior);
            return true;
        }
        if (InteractionMode is not (SvgEditorInteractionMode.Scissors or SvgEditorInteractionMode.Eraser or SvgEditorInteractionMode.Knife or SvgEditorInteractionMode.Lasso))
            return false;
        _startWorld = _currentWorld = Viewport.ViewToWorld(view);
        _lassoSelectionBehavior = behavior;
        _gesture = InteractionMode == SvgEditorInteractionMode.Scissors ? Gesture.Scissors
            : InteractionMode == SvgEditorInteractionMode.Lasso ? Gesture.Lasso : Gesture.CutSweep;
        if (_gesture != Gesture.Scissors)
        {
            _freehandPoints.Clear();
            _freehandPoints.Add(_startWorld);
            _freehandPreview ??= new SKPathBuilder();
            _freehandPreview.Reset();
            _freehandPreview.MoveTo(_startWorld);
        }
        return true;
    }

    private bool UpdateCuttingGesture()
    {
        if (_gesture is not (Gesture.CutSweep or Gesture.Scissors or Gesture.Lasso))
            return false;
        _currentWorld = Viewport.ViewToWorld(_lastView);
        if (_gesture == Gesture.Scissors)
            return true;
        if (InteractionMode == SvgEditorInteractionMode.Knife && _shift)
        {
            var vector = Subtract(_currentWorld, _startWorld);
            var angle = Math.Round(Math.Atan2(vector.Y, vector.X) / (Math.PI / 4)) * Math.PI / 4;
            var length = SKPoint.Distance(_currentWorld, _startWorld);
            _currentWorld = new SKPoint(_startWorld.X + (float)(Math.Cos(angle) * length), _startWorld.Y + (float)(Math.Sin(angle) * length));
            _freehandPoints.Clear();
            _freehandPoints.Add(_startWorld);
            _freehandPoints.Add(_currentWorld);
            _freehandPreview?.Reset();
            _freehandPreview?.MoveTo(_startWorld);
            _freehandPreview?.LineTo(_currentWorld);
            return true;
        }
        if (_freehandPoints.Count == 0 || SKPoint.Distance(_freehandPoints[^1], _currentWorld) * Zoom >= 1.5)
        {
            if (_freehandPoints.Count >= SvgPathCuttingService.MaximumStrokePoints)
                throw new InvalidOperationException("Complete this tool stroke and continue with another stroke.");
            _freehandPoints.Add(_currentWorld);
            _freehandPreview?.LineTo(_currentWorld);
        }
        return true;
    }

    private bool CompleteCuttingGesture(Gesture gesture)
    {
        if (gesture == Gesture.Scissors)
        {
            Controller?.CutPathAt(_currentWorld, (float)(8 / Zoom));
            return true;
        }
        if (gesture is not (Gesture.CutSweep or Gesture.Lasso))
            return false;
        try
        {
            if (gesture == Gesture.Lasso)
            {
                if (_moved && _freehandPoints.Count >= 3)
                    Controller?.SelectInLasso(_freehandPoints, _lassoSelectionBehavior, LassoFullyContained);
                else if (_lassoSelectionBehavior == SvgEditorSelectionBehavior.Replace)
                    Controller?.Select(null);
            }
            else if (InteractionMode == SvgEditorInteractionMode.Eraser)
                Controller?.EraseAlong(_freehandPoints, EraserDiameter);
            else if (_moved && _freehandPoints.Count > 1)
                Controller?.KnifeAlong(_freehandPoints);
        }
        finally { _freehandPoints.Clear(); _freehandPreview?.Reset(); }
        return true;
    }

    private void DrawCuttingGesture(SKCanvas canvas, CanvasResources resources)
    {
        if (_gesture is not (Gesture.CutSweep or Gesture.Scissors or Gesture.Lasso))
            return;
        using var restore = new SKAutoCanvasRestore(canvas);
        canvas.Concat(Viewport.Matrix);
        var stroke = resources.ToolStroke;
        stroke.Color = resources.Accent;
        stroke.StrokeWidth = (float)(1.5 / Zoom);
        if (_gesture == Gesture.Scissors)
        {
            var radius = (float)(5 / Zoom);
            canvas.DrawCircle(_currentWorld, radius, stroke);
            canvas.DrawLine(_currentWorld.X - radius * 1.6f, _currentWorld.Y, _currentWorld.X + radius * 1.6f, _currentWorld.Y, stroke);
            return;
        }
        if (_freehandPreview is null)
            return;
        using var preview = _freehandPreview.Snapshot();
        if (_gesture == Gesture.CutSweep && InteractionMode == SvgEditorInteractionMode.Eraser)
        {
            stroke.Color = resources.Accent.WithAlpha(80);
            stroke.StrokeWidth = EraserDiameter;
            if (_freehandPoints.Count == 1)
            {
                resources.Fill.Color = stroke.Color;
                canvas.DrawCircle(_currentWorld, EraserDiameter / 2, resources.Fill);
            }
            else
                canvas.DrawPath(preview, stroke);
            stroke.Color = resources.Accent;
            stroke.StrokeWidth = (float)(1 / Zoom);
            canvas.DrawCircle(_currentWorld, EraserDiameter / 2, stroke);
        }
        else
        {
            canvas.DrawPath(preview, stroke);
            if (_gesture == Gesture.Lasso)
            {
                stroke.Color = resources.Accent.WithAlpha(135);
                canvas.DrawLine(_currentWorld, _startWorld, stroke);
            }
        }
    }
}
