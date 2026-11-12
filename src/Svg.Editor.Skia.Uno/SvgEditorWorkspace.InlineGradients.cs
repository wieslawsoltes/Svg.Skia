using Microsoft.UI.Xaml.Controls;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private StudioGradientEditor? _studioGradientEditor;

    private void InitializeInlineGradientEditor()
    {
        if (_studioPanels["appearance"].Content is not ScrollViewer { Content: StackPanel panel })
            throw new InvalidOperationException("The Appearance panel must provide its retained content host.");
        _studioGradientEditor = new StudioGradientEditor
        {
            BeforeApply = () =>
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(SvgEditorWorkspace));
                CommitInspectorEdits();
                Canvas.CancelGesture();
            }
        };
        RegisterEditorPaintTarget(_studioGradientEditor, "Appearance's Gradient editor");
        var section = new StudioSection { Header = "Gradient editor", IsExpanded = false, Content = _studioGradientEditor };
        panel.Children.Insert(Math.Min(4, panel.Children.Count), section);
        WorkspaceTitleChanged += (_, _) => _studioGradientEditor.BindController(Controller);
        Loaded += (_, _) => _studioGradientEditor.BindController(Controller);
        Unloaded += (_, _) => _studioGradientEditor.BindController(null);
        Task Open()
        {
            OpenStudioPanel("appearance");
            _studioGradientEditor.BindController(Controller);
            RevealAppearanceSection(section);
            _activeEditorGradient = _studioGradientEditor;
            return Task.CompletedTask;
        }
        var index = _commands.FindIndex(command => command.Id == "gradient");
        var command = new EditorCommand("gradient", "Gradient editor", "Object", string.Empty, Open);
        if (index >= 0)
            _commands[index] = command;
        else
            _commands.Add(command);
        _commands.Add(new EditorCommand("gradientpanel", "Gradient editor", "Window", string.Empty, Open));
        void DraftAction(string id, string label, Action<StudioGradientEditor> execute, Func<StudioGradientEditor, bool> enabled)
            => _commands.Add(new EditorCommand(id, label, "Gradient", string.Empty, () =>
            {
                execute(RequireEditorGradientDraftOwner());
                return Task.CompletedTask;
            })
            { CanExecute = () => EditorGradientDraftOwner is { } owner && enabled(owner) });
        DraftAction("gradient-apply-draft", "Apply gradient draft", editor => editor.ApplyDraft(), editor => editor.CanApplyDraft);
        DraftAction("gradient-reverse-draft", "Reverse gradient draft", editor => editor.ReverseDraft(), editor => editor.HasCurrentDraft);
        DraftAction("gradient-undo-draft", "Undo gradient draft", editor => editor.UndoDraft(), editor => editor.CanUndoDraft);
        DraftAction("gradient-redo-draft", "Redo gradient draft", editor => editor.RedoDraft(), editor => editor.CanRedoDraft);
        DraftAction("gradient-reset-draft", "Reset gradient draft", editor => editor.ResetDraft(), editor => editor.CanResetDraft);
        // Preserve precise text-based creation as an explicit advanced command.
        _commands.Add(new EditorCommand("gradient-source-create", "Create gradient from stop values…", "Object", string.Empty, GradientDialogAsync));
    }
}
