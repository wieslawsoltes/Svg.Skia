using System.Collections.Generic;
using System.Linq;
using AvalonDraw.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace AvalonDraw;

public partial class SymbolSelectWindow : Window
{
    private readonly AutoCompleteBox _list;
    private readonly SymbolService _service;

    public SymbolSelectWindow(SymbolService service)
    {
        InitializeComponent();
        _service = service;
        _list = this.FindControl<AutoCompleteBox>("SymbolList");
        _list.ItemsSource = _service.Symbols
            .Select(s => s.Symbol.ID)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();
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

    private async void SymbolList_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            var id = _list.SelectedItem as string ?? _list.Text;
            if (!string.IsNullOrEmpty(id))
            {
                var entry = _service.Symbols.FirstOrDefault(s => s.Symbol.ID == id);
                if (entry is not null)
                {
                    var dlg = new SymbolEditorWindow(entry, _service);
                    await dlg.ShowDialog<bool>(this);
                    _list.ItemsSource = _service.Symbols
                        .Select(s => s.Symbol.ID)
                        .Where(i => !string.IsNullOrEmpty(i))
                        .ToList();
                }
            }
        }
    }
}
