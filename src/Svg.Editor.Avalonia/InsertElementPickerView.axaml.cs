using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Svg.Editor.Avalonia.Models;

namespace Svg.Editor.Avalonia;

public partial class InsertElementPickerView : SvgEditorDialogViewBase<InsertElementResult>
{
    private readonly AutoCompleteBox _elementList;

    public InsertElementPickerView()
    {
        InitializeComponent();
        _elementList = this.FindControl<AutoCompleteBox>("ElementList")!;
    }

    public IEnumerable<string>? ElementNames
    {
        get => _elementList.ItemsSource as IEnumerable<string>;
        set => _elementList.ItemsSource = value;
    }

    public string? SelectedElement => _elementList.SelectedItem as string ?? _elementList.Text;

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OkButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(SelectedElement))
            Accept(new InsertElementResult(SelectedElement!));
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Cancel();
    }

    private void ElementList_OnGotFocus(object? sender, FocusChangedEventArgs e)
    {
        _elementList.IsDropDownOpen = true;
    }
}
