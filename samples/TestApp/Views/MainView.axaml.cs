using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Input;
using Avalonia.Interactivity;
using ShimSkiaSharp;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Svg.Skia;
using Svg.Model.Drawables;
using Svg.Model.Services;
using TestApp.ViewModels;

namespace TestApp.Views;

public partial class MainView : UserControl
{
    private readonly ObservableCollection<string> _hitResults = new();
    private SKSvg? _currentSkSvg;

    public MainView()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, Drop);
        AddHandler(DragDrop.DragOverEvent, DragOver);
        HitResults.ItemsSource = _hitResults;
        SubscribeOnDraw();
    }

    private void SubscribeOnDraw()
    {
        if (_currentSkSvg is { })
        {
            _currentSkSvg.OnDraw -= SkSvg_OnDraw;
        }

        _currentSkSvg = Svg.SkSvg;

        if (_currentSkSvg is { })
        {
            _currentSkSvg.OnDraw += SkSvg_OnDraw;
        }
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
        var svg = Svg.SkSvg;
        if (svg is { })
        {
            svg.Settings.ShowHitBounds = ShowHitBoundsToggle.IsChecked == true;
            SubscribeOnDraw();
            Svg.InvalidateVisual();
        }
    }

    private void Svg_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var pt = e.GetPosition(Svg);

        _hitResults.Clear();

        if (Svg.SkSvg is { } skSvg)
        {
            skSvg.Settings.HitTestPoints.Clear();

            if (Svg.TryGetPicturePoint(pt, out var skPoint))
            {
                skSvg.Settings.HitTestPoints.Add(skPoint);

                // foreach (var element in Svg.HitTestElements(pt))
                // {
                //     _hitResults.Add(element.ID);
                // }
                var element = Svg.HitTestElements(pt).FirstOrDefault();
                if (element is { })
                {
                    _hitResults.Add(element.ID ?? element.GetType().Name);
                }
            }
        }

        SubscribeOnDraw();
        Svg.InvalidateVisual();
    }

    private void SelectingItemsControl_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _hitResults.Clear();

        if (Svg.SkSvg is { } skSvg)
        {
            skSvg.Settings.HitTestPoints.Clear();
            skSvg.Settings.ShowHitBounds = ShowHitBoundsToggle.IsChecked == true;
            SubscribeOnDraw();
        }

        Svg.InvalidateVisual();
    }

    private void SkSvg_OnDraw(object? sender, SKSvgDrawEventArgs e)
    {
        if (sender is not SKSvg skSvg || skSvg.Drawable is not DrawableBase drawable)
        {
            return;
        }

        if (!skSvg.Settings.ShowHitBounds)
        {
            return;
        }

        var hits = new HashSet<DrawableBase>();

        if (skSvg.Settings.HitTestPoints is { })
        {
            foreach (var pt in skSvg.Settings.HitTestPoints)
            {
                foreach (var d in HitTestService.HitTest(drawable, pt))
                {
                    hits.Add(d);
                }
            }
        }

        if (skSvg.Settings.HitTestRects is { })
        {
            foreach (var r in skSvg.Settings.HitTestRects)
            {
                foreach (var d in HitTestService.HitTest(drawable, r))
                {
                    hits.Add(d);
                }
            }
        }

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            Color = skSvg.Settings.HitBoundsColor
        };

        foreach (var hit in hits.Take(1))
        {
            var rect = skSvg.SkiaModel.ToSKRect(hit.TransformedBounds);
            e.Canvas.DrawRect(rect, paint);
        }
    }
}
