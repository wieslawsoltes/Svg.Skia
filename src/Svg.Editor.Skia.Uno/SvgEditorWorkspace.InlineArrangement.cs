using Microsoft.UI.Xaml.Controls;
using Svg.Editor.Skia;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private StudioArrangeEditor? _studioArrangeEditor;

    private void InitializeInlineArrangement()
    {
        if (_studioPanels["align"].Content is not ScrollViewer { Content: StackPanel panel })
            throw new InvalidOperationException("The Align panel must provide its retained content host.");
        // Keep the existing stacking-order section, replacing only the older
        // selection-only controls with the reference-aware editor.
        foreach (var child in panel.Children.Where(child => child is TextBlock || child is StudioSection
            { Header: "Align objects" or "Distribute objects" }).ToArray()) panel.Children.Remove(child);
        _studioArrangeEditor = new StudioArrangeEditor
        {
            BeforeApply = () =>
            {
                if (_disposed) throw new ObjectDisposedException(nameof(SvgEditorWorkspace));
                CommitInspectorEdits(); Canvas.CancelGesture();
            }
        };
        panel.Children.Insert(0, _studioArrangeEditor);
        WorkspaceTitleChanged += (_, _) => _studioArrangeEditor.BindController(Controller);
        Loaded += (_, _) => _studioArrangeEditor.BindController(Controller);
        Unloaded += (_, _) => _studioArrangeEditor.BindController(null);
        _commands.Add(new EditorCommand("equal-horizontal-gaps", "Equalize horizontal gaps", "Arrange", string.Empty, () =>
        { Controller?.SpaceSelection(SvgArrangementAxis.Horizontal); return Task.CompletedTask; }));
        _commands.Add(new EditorCommand("equal-vertical-gaps", "Equalize vertical gaps", "Arrange", string.Empty, () =>
        { Controller?.SpaceSelection(SvgArrangementAxis.Vertical); return Task.CompletedTask; }));
        _commands.Add(new EditorCommand("center-on-artboard", "Center selection on artboard", "Arrange", string.Empty, () =>
        { Controller?.CenterSelectionOnArtboard(); return Task.CompletedTask; }));
    }
}
