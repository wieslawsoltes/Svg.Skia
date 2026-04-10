using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Svg.Editor.Avalonia.Models;

namespace Svg.Editor.Avalonia;

public partial class SymbolPickerView : SvgEditorDialogViewBase<SymbolSelectionResult>
{
    private readonly AutoCompleteBox _symbolList;

    public SymbolPickerView()
    {
        InitializeComponent();
        _symbolList = this.FindControl<AutoCompleteBox>("SymbolList")!;
    }

    public IEnumerable<string>? SymbolIds
    {
        get => _symbolList.ItemsSource as IEnumerable<string>;
        set => _symbolList.ItemsSource = value;
    }

    public string? SelectedSymbol => _symbolList.SelectedItem as string ?? _symbolList.Text;

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OkButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(SelectedSymbol))
            Accept(new SymbolSelectionResult(SelectedSymbol!));
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Cancel();
    }

    private void SymbolList_OnGotFocus(object? sender, FocusChangedEventArgs e)
    {
        _symbolList.IsDropDownOpen = true;
    }
}
