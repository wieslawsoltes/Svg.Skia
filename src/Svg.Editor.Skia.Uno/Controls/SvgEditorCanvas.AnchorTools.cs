using SkiaSharp;
using Svg.Editor.Skia;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorCanvas
{
    private SvgAnchorEditPreview? _anchorPreview;

    private bool TryBeginAnchorGesture(SKPoint view)
    {
        if (InteractionMode is not (SvgEditorInteractionMode.AddAnchor or SvgEditorInteractionMode.DeleteAnchor or SvgEditorInteractionMode.AnchorPoint))
            return false;
        _gesture = Gesture.Anchor;
        _startWorld = _currentWorld = Viewport.ViewToWorld(view);
        return true;
    }

    private bool UpdateAnchorGesture()
    {
        if (_gesture != Gesture.Anchor)
            return false;
        _currentWorld = Viewport.ViewToWorld(_lastView);
        if (InteractionMode == SvgEditorInteractionMode.AnchorPoint)
        {
            ClearAnchorPreview();
            _anchorPreview = Controller?.PrepareAnchorConversion(_startWorld,
                _moved ? Subtract(_currentWorld, _startWorld) : null, (float)(8 / Zoom));
        }
        return true;
    }

    private bool CompleteAnchorGesture(Gesture gesture)
    {
        if (gesture != Gesture.Anchor)
            return false;
        try
        {
            if (InteractionMode == SvgEditorInteractionMode.AddAnchor)
                Controller?.AddAnchorAt(_startWorld, (float)(8 / Zoom));
            else if (InteractionMode == SvgEditorInteractionMode.DeleteAnchor)
                Controller?.DeleteAnchorAt(_startWorld, (float)(8 / Zoom));
            else if (_anchorPreview is { } preview)
                Controller?.ApplyAnchorConversion(preview);
        }
        finally { ClearAnchorPreview(); }
        return true;
    }

    private void DrawAnchorGesture(SKCanvas canvas, CanvasResources resources)
    {
        if (_gesture != Gesture.Anchor || _anchorPreview is not { } preview)
            return;
        using var restore = new SKAutoCanvasRestore(canvas);
        canvas.Concat(Viewport.Matrix);
        resources.ToolStroke.Color = resources.Accent;
        resources.ToolStroke.StrokeWidth = (float)(1.5 / Zoom);
        canvas.DrawPath(preview.Geometry, resources.ToolStroke);
        if (preview.Handle is not { } handle)
            return;
        var mirrored = new SKPoint(preview.Anchor.X * 2 - handle.X, preview.Anchor.Y * 2 - handle.Y);
        canvas.DrawLine(mirrored, handle, resources.ToolStroke);
        resources.Fill.Color = SKColors.White;
        foreach (var point in new[] { mirrored, handle })
        {
            canvas.DrawCircle(point, (float)(3 / Zoom), resources.Fill);
            canvas.DrawCircle(point, (float)(3 / Zoom), resources.ToolStroke);
        }
    }

    private void ClearAnchorPreview() { _anchorPreview?.Dispose(); _anchorPreview = null; }
}
