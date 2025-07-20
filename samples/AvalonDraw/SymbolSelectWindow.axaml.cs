using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using Avalonia.Input;
using AvalonDraw.Services;
using Svg;
using Svg.Model.Services;

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
        _list.ItemsSource = service.Symbols
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

    public event EventHandler? SymbolEdited;

    private async void SymbolList_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_list.SelectedItem is not string id)
            return;
        var symbol = _service.Find(id);
        if (symbol is null)
            return;
        var win = new SymbolEditorWindow(symbol.GetXML());
        var ok = await win.ShowDialog<bool>(this);
        if (!ok)
            return;
        var wrapper = $"<svg xmlns='http://www.w3.org/2000/svg'><defs>{win.Result}</defs></svg>";
        var doc = SvgService.FromSvg(wrapper);
        var newSym = doc?.Children.OfType<SvgDefinitionList>()
            .SelectMany(d => d.Children.OfType<SvgSymbol>())
            .FirstOrDefault();
        if (newSym is null)
            return;
        if (string.IsNullOrEmpty(newSym.ID))
            newSym.ID = id;
        _service.ReplaceSymbol(symbol, newSym);
        SymbolEdited?.Invoke(this, EventArgs.Empty);
    }
}
