using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AvalonDraw;

public partial class SymbolNameWindow : Window
{
    private readonly TextBox _nameBox;

    public SymbolNameWindow()
    {
        InitializeComponent();
        _nameBox = this.FindControl<TextBox>("NameBox");
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public string? Result { get; private set; }

    private void OkButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Result = _nameBox.Text;
        Close(Result);
    }

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }
}
