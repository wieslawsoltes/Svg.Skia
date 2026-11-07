using System.Diagnostics;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using Svg.Editor.Skia;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorCanvas
{
    private SvgPathRefinementPreview? _pathRefinementPreview;
    private SKPath? _blobPreview;
    private long _pathRefinementAt;
    private SKPoint _measurementStart, _measurementEnd;
    private bool _brushPointerInside;
    public float SmoothDiameter { get; set; } = 40;
    public float SmoothStrength { get; set; } = 0.5f;
    public float PathEraserDiameter { get; set; } = 20;
    public float JoinDiameter { get; set; } = 12;
    public float BlobBrushDiameter { get; set; } = 20;
    public bool BlobBrushMerge { get; set; } = true;
    public double PencilFidelity { get; set; } = 0.65;
    public bool PencilSmooth { get; set; } = true;
    public SvgEditorMeasurement? LastMeasurement { get; private set; }
    public event EventHandler? MeasurementChanged;

    public static bool TryGetRefinementKind(SvgEditorInteractionMode mode, out SvgPathRefinementKind kind)
    {
        kind = mode switch
        {
            SvgEditorInteractionMode.Smooth => SvgPathRefinementKind.Smooth,
            SvgEditorInteractionMode.PathEraser => SvgPathRefinementKind.Erase,
            SvgEditorInteractionMode.Join => SvgPathRefinementKind.Join,
            _ => (SvgPathRefinementKind)(-1)
        };
        return (int)kind >= 0;
    }

    private float RefinementDiameter => InteractionMode switch
    {
        SvgEditorInteractionMode.Smooth => SmoothDiameter,
        SvgEditorInteractionMode.PathEraser => PathEraserDiameter,
        SvgEditorInteractionMode.Join => JoinDiameter,
        _ => BlobBrushDiameter
    };

    private bool TryGetHoverBrushRadius(out float radius)
    {
        radius = InteractionMode == SvgEditorInteractionMode.Eraser ? EraserDiameter / 2
            : TryGetRefinementKind(InteractionMode, out _) || InteractionMode == SvgEditorInteractionMode.BlobBrush ? RefinementDiameter / 2
            : TryGetDeformationKind(InteractionMode, out _) ? DeformationOptions.Radius
            : TryGetSymbolBrushKind(InteractionMode, out _) || InteractionMode == SvgEditorInteractionMode.SymbolSprayer ? SymbolBrushOptions.Radius : 0;
        return radius > 0 && float.IsFinite(radius);
    }

    private bool TryBeginPathUtilityGesture(PointerRoutedEventArgs args, SKPoint view)
    {
        if (InteractionMode == SvgEditorInteractionMode.Measure)
        {
            _gesture = Gesture.Measure;
            _measurementStart = _measurementEnd = _startWorld = _currentWorld = Viewport.ViewToWorld(view);
            UpdateMeasurement();
            return true;
        }
        if (!TryGetRefinementKind(InteractionMode, out _) && InteractionMode != SvgEditorInteractionMode.BlobBrush)
            return false;
        ClearPathUtilityPreview();
        _startWorld = _currentWorld = Viewport.ViewToWorld(view);
        _freehandPoints.Clear();
        _freehandPoints.Add(_startWorld);
        _gesture = InteractionMode == SvgEditorInteractionMode.BlobBrush ? Gesture.BlobStroke : Gesture.PathRefinement;
        RefreshPathUtilityPreview(true);
        return true;
    }

    private bool UpdatePathUtilityGesture()
    {
        if (_gesture is not (Gesture.PathRefinement or Gesture.BlobStroke or Gesture.Measure))
            return false;
        _currentWorld = Viewport.ViewToWorld(_lastView);
        if (_gesture == Gesture.Measure)
        {
            if (_shift)
            {
                var delta = Subtract(_currentWorld, _startWorld);
                var angle = Math.Round(Math.Atan2(delta.Y, delta.X) / (Math.PI / 4)) * Math.PI / 4;
                var length = SKPoint.Distance(_startWorld, _currentWorld);
                _currentWorld = new SKPoint(_startWorld.X + (float)Math.Cos(angle) * length, _startWorld.Y + (float)Math.Sin(angle) * length);
            }
            _measurementEnd = _currentWorld;
            UpdateMeasurement();
            return true;
        }
        if (_freehandPoints.Count == 0 || SKPoint.Distance(_freehandPoints[^1], _currentWorld) * Zoom >= 1.5)
        {
            if (_freehandPoints.Count >= 4096)
                throw new InvalidOperationException("Complete this brush stroke and continue with another stroke.");
            _freehandPoints.Add(_currentWorld);
        }
        RefreshPathUtilityPreview(false);
        return true;
    }

    private void RefreshPathUtilityPreview(bool force)
    {
        if (Controller is not { } controller)
            return;
        var now = Stopwatch.GetTimestamp();
        if (!force && _pathRefinementAt != 0 && (now - _pathRefinementAt) * 1000d / Stopwatch.Frequency < 50)
            return;
        _pathRefinementAt = now;
        if (_gesture == Gesture.PathRefinement && TryGetRefinementKind(InteractionMode, out var kind))
        {
            var preview = controller.PreparePathRefinement(kind, _freehandPoints, RefinementDiameter / 2, SmoothStrength);
            _pathRefinementPreview?.Dispose();
            _pathRefinementPreview = preview;
        }
        else if (_gesture == Gesture.BlobStroke)
        {
            var preview = SvgPathRefinementService.CreateBlobOutline(_freehandPoints, BlobBrushDiameter);
            _blobPreview?.Dispose();
            _blobPreview = preview;
        }
    }

    private bool CompletePathUtilityGesture(Gesture gesture)
    {
        if (gesture == Gesture.Measure)
        { UpdateMeasurement(); return true; }
        if (gesture is not (Gesture.PathRefinement or Gesture.BlobStroke))
            return false;
        try
        {
            if (Controller is not { } controller)
                return true;
            // The final pointer sample can be shorter than the display sampling interval.
            var final = Viewport.ViewToWorld(_lastView);
            if (_freehandPoints.Count > 0 && SKPoint.Distance(_freehandPoints[^1], final) > 0.00001f && _freehandPoints.Count < 4096)
                _freehandPoints.Add(final);
            if (gesture == Gesture.PathRefinement && TryGetRefinementKind(InteractionMode, out var kind))
            {
                using var prepared = controller.PreparePathRefinement(kind, _freehandPoints, RefinementDiameter / 2, SmoothStrength);
                controller.ApplyPathRefinement(prepared);
            }
            else if (gesture == Gesture.BlobStroke)
                controller.AddBlobStroke(_freehandPoints, BlobBrushDiameter, BlobBrushMerge);
        }
        finally { ClearPathUtilityPreview(); _freehandPoints.Clear(); }
        return true;
    }

    private void UpdateMeasurement()
    {
        LastMeasurement = Controller?.MeasureInDocumentCoordinates(_measurementStart, _measurementEnd);
        MeasurementChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClearPathUtilityPreview()
    {
        _pathRefinementPreview?.Dispose();
        _pathRefinementPreview = null;
        _blobPreview?.Dispose();
        _blobPreview = null;
        _pathRefinementAt = 0;
        if (LastMeasurement is not null)
        { LastMeasurement = null; MeasurementChanged?.Invoke(this, EventArgs.Empty); }
    }

    private void DrawPathUtilityPreview(SKCanvas canvas, CanvasResources resources)
    {
        if (_gesture == Gesture.None && _brushPointerInside && !PanMode && !ZoomMode && TryGetHoverBrushRadius(out var radius))
        {
            resources.ToolStroke.Color = resources.Accent;
            resources.ToolStroke.StrokeWidth = 1;
            canvas.DrawCircle(Viewport.WorldToView(PointerPosition), radius * (float)Zoom, resources.ToolStroke);
        }
        if (InteractionMode == SvgEditorInteractionMode.Measure && LastMeasurement is { } measurement)
        {
            var start = Viewport.WorldToView(_measurementStart);
            var end = Viewport.WorldToView(_measurementEnd);
            resources.ToolStroke.Color = resources.Accent;
            resources.ToolStroke.StrokeWidth = 1.5f;
            canvas.DrawLine(start, end, resources.ToolStroke);
            foreach (var point in new[] { start, end })
            {
                canvas.DrawLine(point.X - 4, point.Y, point.X + 4, point.Y, resources.ToolStroke);
                canvas.DrawLine(point.X, point.Y - 4, point.X, point.Y + 4, resources.ToolStroke);
            }
            var label = $"{measurement.Distance:0.##}  ·  {measurement.AngleDegrees:0.##}°";
            var center = new SKPoint((start.X + end.X) / 2, (start.Y + end.Y) / 2);
            var width = resources.LabelFont.MeasureText(label, resources.Fill);
            resources.Fill.Color = resources.Background.WithAlpha(235);
            canvas.DrawRoundRect(new SKRect(center.X - width / 2 - 6, center.Y - 25, center.X + width / 2 + 6, center.Y - 5), 3, 3, resources.Fill);
            resources.Fill.Color = resources.Label;
            canvas.DrawText(label, center.X - width / 2, center.Y - 11, SKTextAlign.Left, resources.LabelFont, resources.Fill);
            return;
        }
        if (_gesture is not (Gesture.PathRefinement or Gesture.BlobStroke))
            return;
        using var restore = new SKAutoCanvasRestore(canvas);
        canvas.Concat(Viewport.Matrix);
        resources.ToolStroke.Color = resources.Accent;
        resources.ToolStroke.StrokeWidth = (float)(1.5 / Zoom);
        if (_pathRefinementPreview is { } preview)
        foreach (var path in preview.Paths)
            canvas.DrawPath(path, resources.ToolStroke);
        if (_blobPreview is { } blob)
        { resources.Fill.Color = resources.Accent.WithAlpha(100); canvas.DrawPath(blob, resources.Fill); }
        canvas.DrawCircle(_currentWorld, RefinementDiameter / 2, resources.ToolStroke);
    }
}
