using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Svg;
using Svg.Skia;
using TestApp.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.System;
using SKPicture = SkiaSharp.SKPicture;
using UnoDispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;
using UnoSvgControl = Uno.Svg.Skia.Svg;

namespace UnoTestApp;

public sealed partial class MainPage : Page
{
    private readonly UnoDispatcherQueueTimer _animationUiTimer;
    private readonly UnoSvgViewAdapter _svgViewAdapter;
    private readonly MainWindowViewModel _viewModel;

    public MainPage()
    {
        InitializeComponent();

        _viewModel = ((App)Application.Current).ViewModel;
        DataContext = _viewModel;
        _svgViewAdapter = new UnoSvgViewAdapter(Svg);

        HorizontalScrollBarVisibilityBox.ItemsSource = Enum.GetValues<ScrollBarVisibility>();
        VerticalScrollBarVisibilityBox.ItemsSource = Enum.GetValues<ScrollBarVisibility>();
        SvgStretchBox.ItemsSource = Enum.GetValues<Microsoft.UI.Xaml.Media.Stretch>();
        AnimationBackendBox.ItemsSource = Enum.GetValues<SvgAnimationHostBackend>();
        HorizontalScrollBarVisibilityBox.SelectedItem = ScrollBarVisibility.Disabled;
        VerticalScrollBarVisibilityBox.SelectedItem = ScrollBarVisibility.Disabled;
        SvgStretchBox.SelectedItem = Microsoft.UI.Xaml.Media.Stretch.None;
        AnimationBackendBox.SelectedItem = SvgAnimationHostBackend.Default;
        AnimationPlaybackRateSlider.Value = Svg.AnimationPlaybackRate;
        AnimationPlaybackRateText.Text = $"{Svg.AnimationPlaybackRate:0.00}x";
        EnableCacheToggle.IsOn = Svg.EnableCache;
        WireframeToggle.IsOn = Svg.Wireframe;

        _viewModel.SvgView.Attach(_svgViewAdapter);

        _animationUiTimer = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().CreateTimer();
        _animationUiTimer.Interval = TimeSpan.FromMilliseconds(100);
        _animationUiTimer.Tick += OnAnimationUiTick;
        _animationUiTimer.Start();

        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _animationUiTimer.Stop();
        _viewModel.SvgView.Detach();
    }

    private async void Root_OnDrop(object sender, DragEventArgs e)
    {
        var paths = new List<string>();

        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            paths.AddRange(items.Select(item => item.Path).Where(static path => !string.IsNullOrWhiteSpace(path)));
        }

        if (paths.Count > 0)
        {
            _viewModel.Drop(paths);
        }
    }

    private void Root_OnDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private void FileItem_OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FileItemViewModel fileItem })
        {
            fileItem.OpenInExplorerCommand.Execute(null);
        }
    }

    private void FileList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _viewModel.SvgView.NotifySelectionChanged();
    }

    private void FileList_OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Delete && FileList.SelectedItem is FileItemViewModel item)
        {
            item.RemoveCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void HorizontalScrollBarVisibilityBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HorizontalScrollBarVisibilityBox.SelectedItem is ScrollBarVisibility value)
        {
            SvgScrollViewer.HorizontalScrollBarVisibility = value;
        }
    }

    private void VerticalScrollBarVisibilityBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VerticalScrollBarVisibilityBox.SelectedItem is ScrollBarVisibility value)
        {
            SvgScrollViewer.VerticalScrollBarVisibility = value;
        }
    }

    private void SvgStretchBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SvgStretchBox.SelectedItem is Microsoft.UI.Xaml.Media.Stretch value)
        {
            Svg.Stretch = value;
        }
    }

    private void EnableCacheToggle_OnToggled(object sender, RoutedEventArgs e)
    {
        Svg.EnableCache = EnableCacheToggle.IsOn;
    }

    private void WireframeToggle_OnToggled(object sender, RoutedEventArgs e)
    {
        Svg.Wireframe = WireframeToggle.IsOn;
    }

    private void AnimationBackendBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AnimationBackendBox.SelectedItem is SvgAnimationHostBackend value)
        {
            Svg.AnimationBackend = value;
        }
    }

    private void AnimationPlaybackRateSlider_OnValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        Svg.AnimationPlaybackRate = e.NewValue;
        AnimationPlaybackRateText.Text = $"{e.NewValue:0.00}x";
    }

    private void PlayAnimationButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SvgView.PlayAnimation();
    }

    private void PauseAnimationButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SvgView.PauseAnimation();
    }

    private void RestartAnimationButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SvgView.RestartAnimation();
    }

    private void Svg_OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(Svg).Position;
        _viewModel.SvgView.HandlePointerPressed(point.X, point.Y);
    }

    private async void ExportButton_OnClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.ExportAsync(Svg.Picture);
    }

    private void OnAnimationUiTick(UnoDispatcherQueueTimer sender, object args)
    {
        _viewModel.SvgView.Tick();
        AnimationPlaybackRateText.Text = $"{Svg.AnimationPlaybackRate:0.00}x";
    }

    private sealed class UnoSvgViewAdapter : ITestAppSvgViewAdapter
    {
        private readonly UnoSvgControl _svg;

        public UnoSvgViewAdapter(UnoSvgControl svg)
        {
            _svg = svg;
        }

        public SKPicture? Picture => _svg.Picture;

        public SKSvg? SkSvg => _svg.SkSvg;

        public double AnimationPlaybackRate
        {
            get => _svg.AnimationPlaybackRate;
            set => _svg.AnimationPlaybackRate = value;
        }

        public SvgAnimationHostBackend ActualAnimationBackend => _svg.ActualAnimationBackend;

        public string? AnimationBackendFallbackReason => _svg.AnimationBackendFallbackReason;

        public bool TryGetPicturePoint(double x, double y, out ShimSkiaSharp.SKPoint picturePoint)
            => _svg.TryGetPicturePoint(new Point(x, y), out picturePoint);

        public IEnumerable<SvgElement> HitTestElements(double x, double y)
            => _svg.HitTestElements(new Point(x, y));

        public void InvalidateView() => _svg.Invalidate();
    }
}
