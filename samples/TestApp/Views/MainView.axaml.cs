using Avalonia.Controls;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using ShimSkiaSharp;
using SkiaSharp;
using Svg.Skia;
using Svg.Model.Drawables;
using Svg.Model.Services;
using TestApp.ViewModels;

namespace TestApp.Views;

public partial class MainView : UserControl
{
    private readonly ObservableCollection<string> _hitResults = new();
    private SKSvg? _currentSkSvg;
    private bool _showHitBounds;
    private SkiaSharp.SKColor _hitBoundsColor = SKColors.Cyan;
    private readonly IList<ShimSkiaSharp.SKPoint> _hitTestPoints = new List<ShimSkiaSharp.SKPoint>();
    private readonly IList<ShimSkiaSharp.SKRect> _hitTestRects = new List<ShimSkiaSharp.SKRect>();

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
        _showHitBounds = ShowHitBoundsToggle.IsChecked == true;
        SubscribeOnDraw();
        Svg.InvalidateVisual();
    }

    private void Svg_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var pt = e.GetPosition(Svg);

        _hitResults.Clear();

        if (Svg.SkSvg is { })
        {
            _hitTestPoints.Clear();

            if (Svg.TryGetPicturePoint(pt, out var skPoint))
            {
                _hitTestPoints.Add(skPoint);

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

        if (Svg.SkSvg is { })
        {
            _hitTestPoints.Clear();
            _showHitBounds = ShowHitBoundsToggle.IsChecked == true;
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

        if (!_showHitBounds)
        {
            return;
        }

        var hits = new HashSet<DrawableBase>();

        foreach (var pt in _hitTestPoints)
        {
            foreach (var d in HitTestService.HitTest(drawable, pt))
            {
                hits.Add(d);
            }
        }

        foreach (var r in _hitTestRects)
        {
            foreach (var d in HitTestService.HitTest(drawable, r))
            {
                hits.Add(d);
            }
        }

        using var paint = new SkiaSharp.SKPaint();
        paint.IsAntialias = true;
        paint.Style = SkiaSharp.SKPaintStyle.Stroke;
        paint.Color = _hitBoundsColor;

        foreach (var hit in hits.Take(1))
        {
            var rect = skSvg.SkiaModel.ToSKRect(hit.TransformedBounds);
            e.Canvas.DrawRect(rect, paint);
        }
    }
}
