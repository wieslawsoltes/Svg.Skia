using Microsoft.UI.Xaml.Controls;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private StudioColorEditor? _studioColorEditor;

    partial void BuildProductCommands()
    {
        void Add(string id, string label, Action execute)
            => _commands.Add(new EditorCommand(id, label, "Object", string.Empty, () => { execute(); return Task.CompletedTask; }));
        _commands.Add(new EditorCommand("filterstudio", "Filter Studio…", "Object", string.Empty, FilterStudioAsync));
        Add("removefilters", "Remove filters", () => Controller?.RemoveSelectionFilters());
        Add("alphamask", "Create alpha mask", () => Controller?.CreateOpacityMask(false));
        Add("luminancemask", "Create luminance mask", () => Controller?.CreateOpacityMask(true));
        Add("releasemask", "Release mask and restore artwork", () => Controller?.ReleaseOpacityMasks(true));
        Add("disablemask", "Disable mask", () => Controller?.ReleaseOpacityMasks(false));
        BuildStudioCommands();
        BuildNamedWorkspaceCommands();
        InitializeInlineColorMixer();
        InitializeInlineGradientEditor();
        InitializeInlineArrangement();
        InitializeThemeGallery();
    }

    private void InitializeInlineColorMixer()
    {
        if (_studioPanels["appearance"].Content is not ScrollViewer { Content: StackPanel panel })
            throw new InvalidOperationException("The Appearance panel must provide its retained content host.");
        _studioColorEditor = new StudioColorEditor
        {
            BeforeApply = () =>
            {
                if (_disposed) throw new ObjectDisposedException(nameof(SvgEditorWorkspace));
                CommitInspectorEdits(); Canvas.CancelGesture();
            }
        };
        var section = new StudioSection { Header = "Color mixer", IsExpanded = false, Content = _studioColorEditor };
        panel.Children.Insert(Math.Min(3, panel.Children.Count), section);
        WorkspaceTitleChanged += (_, _) => _studioColorEditor.BindController(Controller);
        Loaded += (_, _) => _studioColorEditor.BindController(Controller);
        Unloaded += (_, _) => _studioColorEditor.BindController(null);
        _commands.Add(new EditorCommand("colormixer", "Color mixer", "Window", string.Empty, () =>
        {
            OpenStudioPanel("appearance");
            _studioColorEditor.BindController(Controller);
            RevealAppearanceSection(section);
            return Task.CompletedTask;
        }));
    }
}
