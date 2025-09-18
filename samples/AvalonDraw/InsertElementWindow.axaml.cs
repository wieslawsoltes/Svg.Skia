using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace AvalonDraw;

public partial class InsertElementWindow : Window
{
    public InsertElementWindow(IEnumerable<string> names)
    {
        InitializeComponent();
        ElementList.ItemsSource = names;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public string? Selected { get; private set; }

    private void OkButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Selected = ElementList.SelectedItem as string;
        Close(Selected);
    }

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }

    private void ElementList_OnGotFocus(object? sender, Avalonia.Input.GotFocusEventArgs e)
    {
        if (ElementList is AutoCompleteBox box)
            box.IsDropDownOpen = true;
    }
}
