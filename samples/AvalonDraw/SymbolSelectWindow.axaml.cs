using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace AvalonDraw;

public partial class SymbolSelectWindow : Window
{
    private readonly AutoCompleteBox _list;

    public SymbolSelectWindow(IEnumerable<string> ids)
    {
        InitializeComponent();
        _list = this.FindControl<AutoCompleteBox>("SymbolList");
        _list.ItemsSource = ids;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public string? Selected { get; private set; }

    private void OkButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Selected = _list.SelectedItem as string ?? _list.Text;
        Close(Selected);
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void SymbolList_OnGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (_list is AutoCompleteBox box)
            box.IsDropDownOpen = true;
    }
}
