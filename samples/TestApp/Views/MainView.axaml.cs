using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Svg;
using Svg.Skia;
using TestApp.ViewModels;
using ShimPoint = ShimSkiaSharp.SKPoint;
using SkiaPicture = SkiaSharp.SKPicture;

namespace TestApp.Views;

public partial class MainView : UserControl
{
    private readonly DispatcherTimer _animationUiTimer;
    private readonly AvaloniaSvgViewAdapter _svgViewAdapter;
    private MainWindowViewModel? _boundViewModel;

    public MainView()
    {
        InitializeComponent();

        _svgViewAdapter = new AvaloniaSvgViewAdapter(Svg);
        _animationUiTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, OnAnimationUiTick);
        _animationUiTimer.Start();

        AddHandler(DragDrop.DropEvent, Drop);
        AddHandler(DragDrop.DragOverEvent, DragOver);

        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => _boundViewModel?.SvgView.Detach();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _boundViewModel?.SvgView.Detach();
        _boundViewModel = DataContext as MainWindowViewModel;
        _boundViewModel?.SvgView.Attach(_svgViewAdapter);
    }

    private void DragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DragEffects & (DragDropEffects.Copy | DragDropEffects.Link);

        if (!HasFileDrop(e))
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void Drop(object? sender, DragEventArgs e)
    {
        if (!TryGetDroppedPaths(e, out var paths))
        {
            return;
        }

        if (DataContext is MainWindowViewModel vm)
        {
            vm.Drop(paths);
        }
    }

    private void FileItem_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: FileItemViewModel fileItem })
        {
            fileItem.OpenInExplorerCommand.Execute(null);
        }
    }

    private void Svg_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            var point = e.GetPosition(Svg);
            vm.SvgView.HandlePointerPressed(point.X, point.Y);
        }
    }

    private void SelectingItemsControl_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.SvgView.NotifySelectionChanged();
        }
    }

    private void PlayAnimationButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.SvgView.PlayAnimation();
        }
    }

    private void PauseAnimationButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.SvgView.PauseAnimation();
        }
    }

    private void RestartAnimationButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.SvgView.RestartAnimation();
        }
    }

    private async void ExportButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            await vm.ExportAsync(Svg.Picture);
        }
    }

    private void OnAnimationUiTick(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.SvgView.Tick();
        }
    }

    private sealed class AvaloniaSvgViewAdapter : ITestAppSvgViewAdapter
    {
        private readonly Avalonia.Svg.Skia.Svg _svg;

        public AvaloniaSvgViewAdapter(Avalonia.Svg.Skia.Svg svg)
        {
            _svg = svg;
        }

        public SkiaPicture? Picture => _svg.Picture;

        public SKSvg? SkSvg => _svg.SkSvg;

        public double AnimationPlaybackRate
        {
            get => _svg.AnimationPlaybackRate;
            set => _svg.AnimationPlaybackRate = value;
        }

        public SvgAnimationHostBackend ActualAnimationBackend => _svg.ActualAnimationBackend;

        public string? AnimationBackendFallbackReason => _svg.AnimationBackendFallbackReason;

        public bool TryGetPicturePoint(double x, double y, out ShimPoint picturePoint)
            => _svg.TryGetPicturePoint(new Point(x, y), out picturePoint);

        public IEnumerable<SvgElement> HitTestElements(double x, double y)
            => _svg.HitTestElements(new Point(x, y));

        public void InvalidateView() => _svg.InvalidateVisual();
    }

    private static bool HasFileDrop(DragEventArgs e)
        => e.DataTransfer.Items.Any(item => item.Formats.Contains(DataFormat.File));

    private static bool TryGetDroppedPaths(DragEventArgs e, out IReadOnlyList<string> paths)
    {
        var items = e.DataTransfer.Items
            .Where(item => item.Formats.Contains(DataFormat.File))
            .Select(item => item.TryGetRaw(DataFormat.File))
            .OfType<IStorageItem>()
            .Select(item => item.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToList();

        paths = items;
        return items.Count > 0;
    }
}
