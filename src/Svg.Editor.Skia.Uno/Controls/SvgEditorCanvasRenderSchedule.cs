using System;

namespace Svg.Editor.Skia.Uno;

[Flags]
internal enum SvgEditorCanvasInvalidation
{
    None = 0,
    Document = 1,
    Interaction = 2
}

internal readonly record struct SvgEditorCanvasPaintPlan(bool CombineInteraction, bool InvalidateInteraction);

/// <summary>
/// Coordinates a combined full paint with a retained interaction layer. Surface
/// visibility changes only when the corresponding document pixels are painted.
/// </summary>
internal sealed class SvgEditorCanvasRenderSchedule
{
    private enum Mode { Combined, Separated }

    private Mode _currentMode = Mode.Separated;
    private Mode? _nextMode = Mode.Combined;
    private bool _interactionPending;
    private bool _interactionHasPainted;

    public void InvalidateDocument() => _nextMode = Mode.Combined;

    public SvgEditorCanvasInvalidation InvalidateInteraction()
    {
        // A camera update commonly requests feedback again in the same pointer
        // event. The pending full paint already includes its latest state.
        if (_nextMode == Mode.Combined)
            return SvgEditorCanvasInvalidation.None;

        var invalidation = SvgEditorCanvasInvalidation.None;
        if (_nextMode is null && _currentMode == Mode.Combined)
        {
            // The parent must remove its baked feedback before the child can be
            // shown. Subsequent interaction updates keep that document recording.
            _nextMode = Mode.Separated;
            invalidation = SvgEditorCanvasInvalidation.Document;
        }

        if (!_interactionPending)
        {
            _interactionPending = true;
            invalidation |= SvgEditorCanvasInvalidation.Interaction;
        }

        return invalidation;
    }

    public SvgEditorCanvasPaintPlan BeginDocumentPaint()
    {
        var mode = _nextMode ?? _currentMode;
        _nextMode = null;

        // A hidden SKCanvasElement does not initialize its compositor bridge.
        // Let the child paint once during startup/reload, before retaining it
        // invisibly, so a later interaction needs no asynchronous island setup.
        _currentMode = _interactionHasPainted ? mode : Mode.Separated;
        if (_currentMode == Mode.Combined)
        {
            _interactionPending = false;
            return new SvgEditorCanvasPaintPlan(true, false);
        }

        // Also covers a nonvirtual SKCanvasElement.Invalidate call that bypasses
        // InvalidateDocument: a separated parent always refreshes its feedback.
        var invalidateInteraction = !_interactionPending;
        _interactionPending = true;
        return new SvgEditorCanvasPaintPlan(false, invalidateInteraction);
    }

    public bool BeginInteractionPaint()
    {
        if (_currentMode == Mode.Combined)
            return false;

        // Consume before drawing, preserving an invalidation raised by the draw.
        _interactionPending = false;
        return true;
    }

    public void CompleteInteractionPaint() => _interactionHasPainted = true;

    public void Reset()
    {
        _currentMode = Mode.Separated;
        _nextMode = Mode.Combined;
        _interactionPending = false;
        _interactionHasPainted = false;
    }
}
