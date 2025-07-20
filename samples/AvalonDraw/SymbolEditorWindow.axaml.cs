using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AvalonDraw;

public partial class SymbolEditorWindow : Window
{
    private readonly TextBox _editor;

    public SymbolEditorWindow(string xml)
    {
        InitializeComponent();
        _editor = this.FindControl<TextBox>("Editor");
        _editor.Text = xml;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public string Result { get; private set; } = string.Empty;

    private void OkButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Result = _editor.Text;
        Close(true);
    }

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }
}
