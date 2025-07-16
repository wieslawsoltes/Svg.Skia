using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HitTestEditorSample;

public partial class TextEditorWindow : Window
{
    public TextEditorWindow(string text)
    {
        InitializeComponent();
        Editor.Text = text;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public string Result { get; private set; } = string.Empty;

    private void OkButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Result = Editor.Text;
        Close(Result);
    }

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }
}
