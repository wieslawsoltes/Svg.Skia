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
                if (_disposed) throw new ObjectDisposedException(nameof(SvgEditorWorkspace));
                CommitInspectorEdits(); Canvas.CancelGesture();
            }
        };
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
            return Task.CompletedTask;
        }
        var index = _commands.FindIndex(command => command.Id == "gradient");
        var command = new EditorCommand("gradient", "Gradient editor", "Object", string.Empty, Open);
        if (index >= 0) _commands[index] = command; else _commands.Add(command);
        _commands.Add(new EditorCommand("gradientpanel", "Gradient editor", "Window", string.Empty, Open));
        void DraftAction(string id, string label, Action execute)
            => _commands.Add(new EditorCommand(id, label, "Gradient", string.Empty, () => { execute(); return Task.CompletedTask; }));
        DraftAction("gradient-apply-draft", "Apply gradient draft", _studioGradientEditor.ApplyDraft);
        DraftAction("gradient-reverse-draft", "Reverse gradient draft", _studioGradientEditor.ReverseDraft);
        DraftAction("gradient-undo-draft", "Undo gradient draft", _studioGradientEditor.UndoDraft);
        DraftAction("gradient-redo-draft", "Redo gradient draft", _studioGradientEditor.RedoDraft);
        DraftAction("gradient-reset-draft", "Reset gradient draft", () => _studioGradientEditor.ResetDraft());
        // Preserve precise text-based creation as an explicit advanced command.
        _commands.Add(new EditorCommand("gradient-source-create", "Create gradient from stop values…", "Object", string.Empty, GradientDialogAsync));
    }
}
