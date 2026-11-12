using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private sealed record EditorPaintTargetOwner(string Label, Func<bool, bool> CanChange, Action<bool> Synchronize);
    private readonly List<EditorPaintTargetOwner> _editorPaintTargets = new();
    private StudioGradientEditor? _activeEditorGradient;
    private bool _synchronizingEditorPaintTargets;

    private void RegisterEditorPaintTarget(StudioColorEditor editor, string label)
    {
        editor.SynchronizePaintTarget(Canvas.StrokePaintTarget);
        editor.PaintTargetChangeRequested = SetActiveEditorPaint;
        _editorPaintTargets.Add(new EditorPaintTargetOwner(label, editor.CanChangePaintTarget, editor.SynchronizePaintTarget));
    }

    private void RegisterEditorPaintTarget(StudioGradientEditor editor, string label)
    {
        editor.SynchronizePaintTarget(Canvas.StrokePaintTarget);
        editor.PaintTargetChangeRequested = SetActiveEditorPaint;
        editor.GotFocus += (_, _) => _activeEditorGradient = editor;
        _editorPaintTargets.Add(new EditorPaintTargetOwner(label, editor.CanChangePaintTarget, editor.SynchronizePaintTarget));
    }

    private bool TrySynchronizeEditorPaintTarget(bool stroke)
    {
        if (_disposed)
            return false;
        if (_synchronizingEditorPaintTargets)
            return Canvas.StrokePaintTarget == stroke;
        _synchronizingEditorPaintTargets = true;
        try
        {
            bool ValidateDrafts()
            {
                foreach (var owner in _editorPaintTargets)
                {
                    if (owner.CanChange(stroke))
                        continue;
                    StatusText.Text = $"Apply or Reset the draft in {owner.Label} before changing Fill/Stroke.";
                    return false;
                }
                return true;
            }
            // Validate every retained draft before changing any target. Inspector
            // focus loss can finish a native input, so validate once more afterwards.
            if (!ValidateDrafts())
                return false;
            CommitInspectorEdits();
            if (!ValidateDrafts())
                return false;
            Canvas.CancelGesture();
            foreach (var owner in _editorPaintTargets)
                owner.Synchronize(stroke);
            Canvas.StrokePaintTarget = stroke;
            return true;
        }
        catch (Exception exception)
        {
            StatusText.Text = exception.Message;
            return false;
        }
        finally { _synchronizingEditorPaintTargets = false; }
    }

    private void EditorPaintPanelActivated(string id)
    {
        if (id == "gradient")
            _activeEditorGradient = _editorGradientPanel;
        else if (id == "appearance" && IsEditorPaintControlVisible(_studioGradientEditor))
            _activeEditorGradient = _studioGradientEditor;
    }

    private StudioGradientEditor? EditorGradientDraftOwner
    {
        get
        {
            if (_activeEditorGradient is not null)
                return _activeEditorGradient;
            if (IsEditorPaintControlVisible(_editorGradientPanel))
                return _editorGradientPanel;
            return _studioGradientEditor ?? _editorGradientPanel;
        }
    }

    private StudioGradientEditor RequireEditorGradientDraftOwner()
        => EditorGradientDraftOwner ?? throw new InvalidOperationException("Open a Gradient editor first.");

    private static bool IsEditorPaintControlVisible(FrameworkElement? editor)
    {
        if (editor?.IsLoaded != true)
            return false;
        for (DependencyObject? current = editor; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is UIElement { Visibility: Visibility.Collapsed } or StudioSection { IsExpanded: false })
                return false;
        return true;
    }
}
