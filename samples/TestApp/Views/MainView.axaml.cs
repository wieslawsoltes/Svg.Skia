using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ShimSkiaSharp;
using System.Collections.ObjectModel;
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
        HitResults.Items = _hitResults;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
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
        var svg = Svg.SkSvg;
        if (svg?.Picture is null)
        {
            return;
        }

        var pt = e.GetPosition(Svg);

        var picture = svg.Picture;
        var viewPort = new Rect(Svg.Bounds.Size);
        var sourceSize = new Size(picture.CullRect.Width, picture.CullRect.Height);
        var scale = Svg.Stretch.CalculateScaling(Svg.Bounds.Size, sourceSize, Svg.StretchDirection);
        var scaledSize = sourceSize * scale;
        var destRect = viewPort.CenterRect(new Rect(scaledSize)).Intersect(viewPort);
        var sourceRect = new Rect(sourceSize).CenterRect(new Rect(destRect.Size / scale));
        var bounds = picture.CullRect;
        var scaleMatrix = Matrix.CreateScale(destRect.Width / sourceRect.Width, destRect.Height / sourceRect.Height);
        var translateMatrix = Matrix.CreateTranslation(-sourceRect.X + destRect.X - bounds.Top, -sourceRect.Y + destRect.Y - bounds.Left);
        var matrix = scaleMatrix * translateMatrix;
        var inverse = matrix.Invert();
        var picturePoint = inverse.Transform(pt);

        var skPoint = new SKPoint((float)picturePoint.X, (float)picturePoint.Y);

        _hitResults.Clear();
        svg.Settings.HitTestPoints.Clear();
        svg.Settings.HitTestPoints.Add(skPoint);
        foreach (var element in svg.HitTestElements(skPoint))
        {
            var id = element.ID ?? element.GetType().Name;
            _hitResults.Add(id);
        }

        Svg.InvalidateVisual();
    }
}
