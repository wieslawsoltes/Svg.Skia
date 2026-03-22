namespace Svg.Editor.Skia.Uno.Controls;

public sealed partial class SvgEditorDevPanel : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(ISvgEditorShellViewModel),
            typeof(SvgEditorDevPanel),
            new PropertyMetadata(null));

    public SvgEditorDevPanel()
    {
        InitializeComponent();
    }

    public ISvgEditorShellViewModel? ViewModel
    {
        get => (ISvgEditorShellViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public event EventHandler<DevCodeSnippetRequestedEventArgs>? SnippetRequested;

    public event EventHandler? CopyActiveSnippetRequested;

    public event EventHandler<EditorMainMenuCommandEventArgs>? CommandRequested;

    private void OnSnippetTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string snippetId })
        {
            return;
        }

        SnippetRequested?.Invoke(this, new DevCodeSnippetRequestedEventArgs(snippetId));
    }

    private void OnCopyActiveSnippetClick(object sender, RoutedEventArgs e)
    {
        CopyActiveSnippetRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCommandClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string rawCommand }
            || !Enum.TryParse<EditorMainMenuCommand>(rawCommand, ignoreCase: true, out var command))
        {
            return;
        }

        CommandRequested?.Invoke(this, new EditorMainMenuCommandEventArgs(command));
    }
}
