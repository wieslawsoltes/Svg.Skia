using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Input;
using Avalonia.Interactivity;
using ShimSkiaSharp;
using System.Collections.ObjectModel;
using Avalonia;
using TestApp.ViewModels;

namespace TestApp.Views;

public partial class MainView : UserControl
{
    private readonly ObservableCollection<string> _hitResults = new();

    public MainView()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, Drop);
        AddHandler(DragDrop.DragOverEvent, DragOver);
        HitResults.ItemsSource = _hitResults;
    }

    private void DragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DragEffects & (DragDropEffects.Copy | DragDropEffects.Link);

        if (!e.Data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void Drop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            var paths = e.Data.GetFileNames();
            if (paths is { })
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    try
                    {
                        vm.Drop(paths);
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }
            }
        }
    }

    private void FileItem_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control control && control.DataContext is FileItemViewModel fileItemViewModel)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("explorer", fileItemViewModel.Path);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", fileItemViewModel.Path);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", fileItemViewModel.Path);
            }
        }
    }

    private void ShowHitBoundsToggle_OnToggled(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch ts)
        {
            var svg = Svg.SkSvg;
            if (svg is { })
            {
                svg.Settings.ShowHitBounds = ts.IsChecked == true;
                Svg.InvalidateVisual();
            }
        }
    }

    private void Svg_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var pt = e.GetPosition(Svg);

        _hitResults.Clear();

        if (Svg.SkSvg is { } skSvg && Svg.TryGetPicturePoint(pt, out var skPoint))
        {
            skSvg.Settings.HitTestPoints.Clear();
            skSvg.Settings.HitTestPoints.Add(skPoint);

            foreach (var element in Svg.HitTestElements(pt))
            {
                var id = element.ID ?? element.GetType().Name;
                _hitResults.Add(id);
            }
        }

        Svg.InvalidateVisual();
    }
}
