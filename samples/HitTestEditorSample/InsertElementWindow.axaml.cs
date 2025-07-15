using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HitTestEditorSample;

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
}
