using Svg.Editor.Skia;

namespace Svg.Editor.Skia.Uno;

/// <summary>
/// Records the model state actually presented by a retained panel. Hidden panels
/// do not consume revisions; opening one therefore catches up in a single refresh.
/// </summary>
internal sealed class StudioPanelRefreshState(bool observeSelection = false)
{
    private SvgEditorController? _controller;
    private SvgDocument? _document;
    private SvgEditorSelectionContext? _selection;
    private long _revision;
    private bool _initialized, _editing;

    public bool BelongsTo(SvgEditorController? controller)
        => ReferenceEquals(controller, _controller) && ReferenceEquals(controller?.Document, _document);

    public bool CanPrepareDraft(SvgEditorController? controller, bool hasPendingDraft)
    {
        if (hasPendingDraft || controller?.Document is null || controller.IsEditing
            || controller.SelectedElements.Count == 0 || !NeedsRefresh(true, controller))
            return false;
        foreach (var element in controller.SelectedElements)
            if (SvgEditorController.IsEditLocked(element))
                return false;
        return true;
    }

    public bool NeedsRefresh(bool visible, SvgEditorController? controller)
    {
        if (!visible)
            return false;
        if (!_initialized || !BelongsTo(controller)
            || (controller?.DocumentRevision ?? -1) != _revision)
            return true;
        return observeSelection && (_editing != (controller?.IsEditing == true)
            || _selection is not null && !_selection.IsCurrent(controller));
    }

    // Call only after population succeeds, never when skipping a hidden panel.
    public void Complete(SvgEditorController? controller)
    {
        _controller = controller;
        _document = controller?.Document;
        _revision = controller?.DocumentRevision ?? -1;
        _editing = controller?.IsEditing == true;
        _selection = observeSelection && _document is not null ? new SvgEditorSelectionContext(controller!) : null;
        _initialized = true;
    }

    public void Invalidate()
    {
        _initialized = false;
        _controller = null;
        _document = null;
        _selection = null;
    }
}
