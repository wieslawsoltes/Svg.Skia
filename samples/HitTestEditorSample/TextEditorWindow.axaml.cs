using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HitTestEditorSample;

public partial class TextEditorWindow : Window
{
    private readonly TextBox _editor;

    public TextEditorWindow(string text)
    {
        InitializeComponent();
        _editor = this.FindControl<TextBox>("Editor");
        _editor.Text = text;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public string Result { get; private set; } = string.Empty;

    private void OkButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Result = _editor.Text;
        Close(Result);
    }

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }
}
