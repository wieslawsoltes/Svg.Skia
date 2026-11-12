using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using Svg.Editor.Skia;
using Svg.Skia;
using Windows.System;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorCanvas
{
    private SvgShapeBuilderDraft? _shapeBuilderDraft;
    private readonly List<int> _shapeBuilderRegions = new();
    private SvgDeformationPreview? _deformationPreview;
    private readonly List<SvgDeformationSample> _deformationSamples = new();
    private readonly Stopwatch _deformationClock = new();
    private double _deformationObservedElapsed;
    private SvgSymbolBrushPreview? _symbolBrushPreview;
    private SKSvg? _sprayRenderer;
    private IReadOnlyList<SvgSymbolPlacement> _sprayPlacements = Array.Empty<SvgSymbolPlacement>();
    private SvgSymbolBrushOptions? _gestureSymbolOptions;
    private SvgDeformationOptions? _gestureDeformationOptions;
    private string? _gestureSymbolId;
    private bool _advancedReverse;
    private long _advancedPreviewAt;
    private SKPoint _advancedPreviousPoint;
    private DispatcherTimer? _symbolDwellTimer;
    private DispatcherTimer? _deformationDwellTimer;
    public SvgDeformationOptions DeformationOptions { get; set; } = new();
    public SvgSymbolBrushOptions SymbolBrushOptions { get; set; } = new();

    public static bool TryGetDeformationKind(SvgEditorInteractionMode mode, out SvgDeformationKind kind)
    {
        kind = mode switch
        {
            SvgEditorInteractionMode.Warp => SvgDeformationKind.Warp,
            SvgEditorInteractionMode.Twirl => SvgDeformationKind.Twirl,
            SvgEditorInteractionMode.Pucker => SvgDeformationKind.Pucker,
            SvgEditorInteractionMode.Bloat => SvgDeformationKind.Bloat,
            SvgEditorInteractionMode.Scallop => SvgDeformationKind.Scallop,
            SvgEditorInteractionMode.Crystallize => SvgDeformationKind.Crystallize,
            SvgEditorInteractionMode.Wrinkle => SvgDeformationKind.Wrinkle,
            _ => default
        };
        return mode is SvgEditorInteractionMode.Warp or SvgEditorInteractionMode.Twirl or SvgEditorInteractionMode.Pucker
            or SvgEditorInteractionMode.Bloat or SvgEditorInteractionMode.Scallop or SvgEditorInteractionMode.Crystallize or SvgEditorInteractionMode.Wrinkle;
    }

    public static bool TryGetSymbolBrushKind(SvgEditorInteractionMode mode, out SvgSymbolBrushKind kind)
    {
        kind = mode switch
        {
            SvgEditorInteractionMode.SymbolShifter => SvgSymbolBrushKind.Shift,
            SvgEditorInteractionMode.SymbolScruncher => SvgSymbolBrushKind.Scrunch,
            SvgEditorInteractionMode.SymbolSizer => SvgSymbolBrushKind.Size,
            SvgEditorInteractionMode.SymbolSpinner => SvgSymbolBrushKind.Spin,
            SvgEditorInteractionMode.SymbolStainer => SvgSymbolBrushKind.Stain,
            SvgEditorInteractionMode.SymbolScreener => SvgSymbolBrushKind.Screen,
            SvgEditorInteractionMode.SymbolStyler => SvgSymbolBrushKind.Style,
            _ => default
        };
        return mode is SvgEditorInteractionMode.SymbolShifter or SvgEditorInteractionMode.SymbolScruncher or SvgEditorInteractionMode.SymbolSizer
            or SvgEditorInteractionMode.SymbolSpinner or SvgEditorInteractionMode.SymbolStainer or SvgEditorInteractionMode.SymbolScreener or SvgEditorInteractionMode.SymbolStyler;
    }

    private bool TryBeginAdvancedBrush(PointerRoutedEventArgs args, SKPoint view)
    {
        if (Controller is not { } controller)
            return false;
        var deformation = TryGetDeformationKind(InteractionMode, out _);
        var symbols = TryGetSymbolBrushKind(InteractionMode, out _);
        if (!deformation && !symbols && InteractionMode is not (SvgEditorInteractionMode.ShapeBuilder or SvgEditorInteractionMode.SymbolSprayer))
            return false;
        ClearAdvancedBrushPreview();
        _startWorld = _currentWorld = Viewport.ViewToWorld(view);
        _advancedPreviousPoint = _startWorld;
        _advancedReverse = args.KeyModifiers.HasFlag(VirtualKeyModifiers.Menu);
        _advancedPreviewAt = 0;
        if (InteractionMode == SvgEditorInteractionMode.ShapeBuilder)
        {
            if (controller.SelectedElements.Count == 0)
                controller.SelectAt(_startWorld);
            _shapeBuilderDraft = controller.BeginShapeBuilder();
            var hit = _shapeBuilderDraft.HitTest(_startWorld);
            if (hit >= 0)
                _shapeBuilderRegions.Add(hit);
            _gesture = Gesture.ShapeBuilder;
            return true;
        }
        if (deformation && controller.SelectedElements.Count == 0)
            controller.SelectAt(_startWorld);
        _freehandPoints.Clear();
        _freehandPoints.Add(_startWorld);
        _gesture = deformation ? Gesture.Deformation : symbols ? Gesture.SymbolBrush : Gesture.SymbolSpray;
        if (deformation)
        {
            var options = DeformationOptions;
            _gestureDeformationOptions = new SvgDeformationOptions
            {
                Radius = options.Radius,
                Strength = options.Strength * (_advancedReverse ? -1 : 1),
                Detail = options.Detail,
                Flatness = options.Flatness,
                MaximumPoints = options.MaximumPoints,
                DwellRate = options.DwellRate
            };
            if (InteractionMode != SvgEditorInteractionMode.Warp)
            {
                _deformationSamples.Add(new SvgDeformationSample(_startWorld, 0));
                _deformationObservedElapsed = 0;
                _deformationClock.Restart();
                StartDeformationDwell();
            }
        }
        else
        {
            var options = SymbolBrushOptions;
            _gestureSymbolOptions = new SvgSymbolBrushOptions
            {
                Radius = options.Radius,
                Size = options.Size,
                Density = options.Density,
                SizeVariation = options.SizeVariation,
                RotationVariation = options.RotationVariation,
                Strength = options.Strength,
                GraphicStyleName = options.GraphicStyleName,
                Seed = options.Seed++
            };
            if (_gesture == Gesture.SymbolSpray)
            {
                _gestureSymbolId = controller.Tools.SymbolId ?? throw new InvalidOperationException("Choose a symbol before spraying.");
                _sprayRenderer = new SKSvg();
                _sprayRenderer.FromSvgDocument(controller.CreateSymbolPreviewDocument(_gestureSymbolId));
            }
            StartSymbolDwell();
        }
        RefreshAdvancedBrushPreview(true);
        return true;
    }

    private bool UpdateAdvancedBrush()
    {
        if (_gesture is not (Gesture.ShapeBuilder or Gesture.Deformation or Gesture.SymbolSpray or Gesture.SymbolBrush))
            return false;
        var previous = _advancedPreviousPoint;
        _currentWorld = Viewport.ViewToWorld(_lastView);
        _advancedPreviousPoint = _currentWorld;
        if (_gesture == Gesture.ShapeBuilder)
        {
            if (_shapeBuilderDraft is { } draft)
                foreach (var region in draft.HitTestSegment(previous, _currentWorld, (float)(0.5 / Zoom)))
                    if (!_shapeBuilderRegions.Contains(region))
                        _shapeBuilderRegions.Add(region);
            return true;
        }
        if (_gesture == Gesture.Deformation && InteractionMode != SvgEditorInteractionMode.Warp)
        {
            _deformationObservedElapsed = _deformationClock.Elapsed.TotalSeconds;
            AppendDeformationSample(_deformationObservedElapsed, false);
            RefreshAdvancedBrushPreview(false);
            return true;
        }
        var minimum = _gesture == Gesture.SymbolSpray ? Math.Max(2 / Zoom, (_gestureSymbolOptions?.Radius ?? 40) * 0.3)
            : Math.Max(1.5 / Zoom, (_gestureDeformationOptions?.Radius ?? _gestureSymbolOptions?.Radius ?? 40) * 0.05);
        if (_freehandPoints.Count == 0 || SKPoint.Distance(_freehandPoints[^1], _currentWorld) >= minimum)
        {
            var capacity = _gesture == Gesture.SymbolSpray ? SvgSymbolSprayService.MaximumInstances / Math.Max(1, _gestureSymbolOptions?.Density ?? 3) : 4096;
            if (_gesture == Gesture.Deformation && _freehandPoints.Count >= capacity)
                throw new InvalidOperationException("Finish this deformation stroke before adding more points.");
            if (_freehandPoints.Count < capacity)
                _freehandPoints.Add(_currentWorld);
        }
        RefreshAdvancedBrushPreview(false);
        return true;
    }

    private void AppendDeformationSample(double elapsed, bool force)
    {
        if (elapsed > SvgPathDeformationService.MaximumDurationSeconds)
            throw new InvalidOperationException("Finish the deformation gesture within 60 seconds, then continue with another stroke.");
        var last = _deformationSamples[^1];
        var distance = SKPoint.Distance(last.Position, _currentWorld);
        if (elapsed <= last.ElapsedSeconds && distance < 0.000001f)
            return;
        var minimum = Math.Max(1.5 / Zoom, (_gestureDeformationOptions?.Radius ?? 40) * 0.05);
        if (!force && elapsed - last.ElapsedSeconds < 0.05 && distance < minimum)
            return;
        if (_deformationSamples.Count >= SvgPathDeformationService.MaximumStrokePoints)
            throw new InvalidOperationException("Finish this deformation stroke before adding more points.");
        _deformationSamples.Add(new SvgDeformationSample(_currentWorld, elapsed));
    }

    private void StartDeformationDwell()
    {
        if (_gestureDeformationOptions?.DwellRate == 0)
            return;
        if (_deformationDwellTimer is null)
        {
            _deformationDwellTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _deformationDwellTimer.Tick += (_, _) =>
            {
                if (_gesture != Gesture.Deformation || !_deformationClock.IsRunning)
                { _deformationDwellTimer.Stop(); return; }
                try
                {
                    _deformationObservedElapsed = _deformationClock.Elapsed.TotalSeconds;
                    AppendDeformationSample(_deformationObservedElapsed, true);
                    RefreshAdvancedBrushPreview(true);
                    Invalidate();
                }
                catch (Exception exception) { HandleInteractionFailure(exception); }
            };
        }
        _deformationDwellTimer.Start();
    }

    private void StartSymbolDwell()
    {
        if (_symbolDwellTimer is null)
        {
            _symbolDwellTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _symbolDwellTimer.Tick += (_, _) =>
            {
                if (_gesture is not (Gesture.SymbolSpray or Gesture.SymbolBrush))
                { _symbolDwellTimer.Stop(); return; }
                try
                {
                    var capacity = _gesture == Gesture.SymbolSpray ? SvgSymbolSprayService.MaximumInstances / Math.Max(1, _gestureSymbolOptions?.Density ?? 3) : 4096;
                    if (_freehandPoints.Count >= capacity)
                    { _symbolDwellTimer.Stop(); return; }
                    _freehandPoints.Add(_currentWorld);
                    RefreshAdvancedBrushPreview(true);
                    Invalidate();
                }
                catch (Exception exception) { HandleInteractionFailure(exception); }
            };
        }
        _symbolDwellTimer.Start();
    }

    private void RefreshAdvancedBrushPreview(bool force)
    {
        if (Controller is not { } controller)
            return;
        var now = Stopwatch.GetTimestamp();
        if (!force && _advancedPreviewAt != 0 && (now - _advancedPreviewAt) * 1000d / Stopwatch.Frequency < 50)
            return;
        _advancedPreviewAt = now;
        if (_gesture == Gesture.Deformation && TryGetDeformationKind(InteractionMode, out var kind))
        {
            var prepared = kind == SvgDeformationKind.Warp
                ? controller.PrepareDeformation(kind, _freehandPoints, _gestureDeformationOptions)
                : controller.PrepareDeformation(kind, _deformationSamples, _gestureDeformationOptions);
            _deformationPreview?.Dispose();
            _deformationPreview = prepared;
        }
        else if (_gesture == Gesture.SymbolSpray)
            _sprayPlacements = SvgSymbolSprayService.CreatePlacements(_freehandPoints, _gestureSymbolOptions);
        else if (_gesture == Gesture.SymbolBrush && TryGetSymbolBrushKind(InteractionMode, out var symbolKind))
        {
            var prepared = controller.PrepareSymbolBrush(symbolKind, _freehandPoints, _gestureSymbolOptions, _advancedReverse);
            _symbolBrushPreview?.Dispose();
            _symbolBrushPreview = prepared;
        }
    }

    private bool CompleteAdvancedBrush(Gesture gesture)
    {
        if (gesture is not (Gesture.ShapeBuilder or Gesture.Deformation or Gesture.SymbolSpray or Gesture.SymbolBrush))
            return false;
        _symbolDwellTimer?.Stop();
        _deformationDwellTimer?.Stop();
        _deformationClock.Stop();
        try
        {
            if (Controller is not { } controller)
                return true;
            if (gesture == Gesture.ShapeBuilder && _shapeBuilderDraft is { } draft)
                controller.ApplyShapeBuilder(draft, _shapeBuilderRegions, _advancedReverse);
            else if (gesture == Gesture.Deformation && TryGetDeformationKind(InteractionMode, out var kind))
            {
                // UpdateGesture captured the actual release position/time before
                // preview computation. Commit that sample, excluding rendering delay.
                if (kind == SvgDeformationKind.Warp)
                {
                    if (_freehandPoints.Count > 0 && SKPoint.Distance(_freehandPoints[^1], _currentWorld) > 0.000001f)
                    {
                        if (_freehandPoints.Count >= SvgPathDeformationService.MaximumStrokePoints)
                            throw new InvalidOperationException("Finish this deformation stroke before adding more points.");
                        _freehandPoints.Add(_currentWorld);
                    }
                }
                else
                    AppendDeformationSample(_deformationObservedElapsed, true);
                using var prepared = kind == SvgDeformationKind.Warp
                    ? controller.PrepareDeformation(kind, _freehandPoints, _gestureDeformationOptions)
                    : controller.PrepareDeformation(kind, _deformationSamples, _gestureDeformationOptions);
                controller.ApplyDeformation(prepared);
            }
            else if (gesture == Gesture.SymbolSpray && _gestureSymbolId is { } symbol)
                controller.SpraySymbols(symbol, _freehandPoints, _gestureSymbolOptions);
            else if (gesture == Gesture.SymbolBrush && TryGetSymbolBrushKind(InteractionMode, out var symbolKind))
            {
                using var prepared = controller.PrepareSymbolBrush(symbolKind, _freehandPoints, _gestureSymbolOptions, _advancedReverse);
                controller.ApplySymbolBrush(prepared);
            }
        }
        finally { ClearAdvancedBrushPreview(); _freehandPoints.Clear(); }
        return true;
    }

    private void DrawAdvancedBrushPreview(SKCanvas canvas, CanvasResources resources)
    {
        if (_gesture is not (Gesture.ShapeBuilder or Gesture.Deformation or Gesture.SymbolSpray or Gesture.SymbolBrush))
            return;
        using var restore = new SKAutoCanvasRestore(canvas);
        canvas.Concat(Viewport.Matrix);
        resources.ToolStroke.Color = resources.Accent;
        resources.ToolStroke.StrokeWidth = (float)(1.5 / Zoom);
        resources.Fill.Color = resources.Accent.WithAlpha(55);
        if (_shapeBuilderDraft is { } draft)
        {
            foreach (var index in _shapeBuilderRegions)
            {
                canvas.DrawPath(draft.Regions[index].Geometry, resources.Fill);
                canvas.DrawPath(draft.Regions[index].Geometry, resources.ToolStroke);
            }
            var cross = (float)(5 / Zoom);
            canvas.DrawLine(_currentWorld.X - cross, _currentWorld.Y, _currentWorld.X + cross, _currentWorld.Y, resources.ToolStroke);
            if (!_advancedReverse)
                canvas.DrawLine(_currentWorld.X, _currentWorld.Y - cross, _currentWorld.X, _currentWorld.Y + cross, resources.ToolStroke);
            return;
        }
        if (_deformationPreview is { } deformation)
            foreach (var path in deformation.Paths)
                canvas.DrawPath(path, resources.ToolStroke);
        if (_symbolBrushPreview is { } symbols)
            foreach (var bounds in symbols.Bounds)
                canvas.DrawRect(bounds, resources.ToolStroke);
        if (_sprayRenderer?.Picture is { } picture)
            foreach (var placement in _sprayPlacements)
            {
                using var instanceRestore = new SKAutoCanvasRestore(canvas);
                canvas.Translate(placement.Position.X, placement.Position.Y);
                canvas.RotateDegrees(placement.Rotation);
                canvas.Scale(placement.Size / 64);
                canvas.Translate(-32, -32);
                canvas.DrawPicture(picture, resources.GhostPaint);
            }
        var radius = _gestureDeformationOptions?.Radius ?? _gestureSymbolOptions?.Radius ?? 40;
        canvas.DrawCircle(_currentWorld, radius, resources.ToolStroke);
    }

    private void ClearAdvancedBrushPreview()
    {
        _symbolDwellTimer?.Stop();
        _deformationDwellTimer?.Stop();
        _deformationClock.Reset();
        _deformationSamples.Clear();
        _deformationObservedElapsed = 0;
        _shapeBuilderDraft?.Dispose();
        _shapeBuilderDraft = null;
        _shapeBuilderRegions.Clear();
        _deformationPreview?.Dispose();
        _deformationPreview = null;
        _symbolBrushPreview?.Dispose();
        _symbolBrushPreview = null;
        _sprayRenderer?.Dispose();
        _sprayRenderer = null;
        _sprayPlacements = Array.Empty<SvgSymbolPlacement>();
        _gestureSymbolOptions = null;
        _gestureDeformationOptions = null;
        _gestureSymbolId = null;
    }
}
