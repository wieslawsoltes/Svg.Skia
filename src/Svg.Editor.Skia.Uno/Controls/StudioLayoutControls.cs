using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Svg.Editor.Skia;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.System;

namespace Svg.Editor.Skia.Uno;

public sealed class StudioWrapPanel : Panel
{
    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(nameof(Spacing), typeof(double), typeof(StudioWrapPanel), new PropertyMetadata(4d, (sender, _) => ((StudioWrapPanel)sender).InvalidateMeasure()));
    public double Spacing { get => (double)GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? Math.Max(0, availableSize.Width) : double.MaxValue;
        var spacing = double.IsFinite(Spacing) ? Math.Max(0, Spacing) : 0;
        var x = 0d; var y = 0d; var row = 0d; var measuredWidth = 0d;
        foreach (var child in Children.Where(child => child.Visibility == Visibility.Visible))
        {
            child.Measure(new Size(width, double.PositiveInfinity));
            var size = child.DesiredSize;
            var childWidth = Math.Min(size.Width, width);
            if (x > 0 && x + childWidth > width) { measuredWidth = Math.Max(measuredWidth, x - spacing); x = 0; y += row + spacing; row = 0; }
            x += childWidth + spacing; row = Math.Max(row, size.Height);
        }
        return new Size(Math.Max(measuredWidth, Math.Max(0, x - spacing)), y + row);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        var spacing = double.IsFinite(Spacing) ? Math.Max(0, Spacing) : 0;
        var x = 0d; var y = 0d; var row = 0d;
        foreach (var child in Children.Where(child => child.Visibility == Visibility.Visible))
        {
            var size = child.DesiredSize;
            var width = Math.Min(size.Width, Math.Max(0, finalSize.Width));
            if (x > 0 && x + width > finalSize.Width) { x = 0; y += row + spacing; row = 0; }
            child.Arrange(new Rect(x, y, width, size.Height));
            x += width + spacing; row = Math.Max(row, size.Height);
        }
        return finalSize;
    }
}

public sealed class StudioResizeEventArgs : EventArgs
{
    public StudioResizeEventArgs(double horizontal, double vertical) { HorizontalChange = horizontal; VerticalChange = vertical; }
    public double HorizontalChange { get; }
    public double VerticalChange { get; }
}

/// <summary>Retemplatable, pointer-capturing and keyboard-accessible resize grip.</summary>
public sealed class StudioSplitter : Control
{
    private uint? _pointer;
    private Point _last;
    public event EventHandler<double>? KeyboardResize;
    public event EventHandler<StudioResizeEventArgs>? DragDelta;
    public event EventHandler? DragCompleted;
    public StudioSplitter()
    {
        DefaultStyleKey = typeof(StudioSplitter); IsTabStop = true;
        AutomationProperties.SetName(this, "Resize panel. Use arrow keys to adjust.");
    }
    protected override void OnPointerPressed(PointerRoutedEventArgs args)
    {
        base.OnPointerPressed(args);
        var point = args.GetCurrentPoint(XamlRoot?.Content ?? this);
        if (_pointer is not null || !point.Properties.IsLeftButtonPressed || !CapturePointer(args.Pointer)) return;
        _pointer = args.Pointer.PointerId; _last = point.Position;
        Focus(FocusState.Pointer); VisualStateManager.GoToState(this, "Pressed", false); args.Handled = true;
    }
    protected override void OnPointerMoved(PointerRoutedEventArgs args)
    {
        base.OnPointerMoved(args);
        if (_pointer != args.Pointer.PointerId) return;
        // Window coordinates remain stable when the splitter moves with its resized dock.
        var point = args.GetCurrentPoint(XamlRoot?.Content ?? this).Position;
        var delta = new StudioResizeEventArgs(point.X - _last.X, point.Y - _last.Y);
        _last = point; DragDelta?.Invoke(this, delta); args.Handled = true;
    }
    protected override void OnPointerReleased(PointerRoutedEventArgs args)
    {
        base.OnPointerReleased(args);
        if (_pointer != args.Pointer.PointerId) return;
        CompleteDrag(); ReleasePointerCapture(args.Pointer); args.Handled = true;
    }
    protected override void OnPointerCaptureLost(PointerRoutedEventArgs args) { base.OnPointerCaptureLost(args); CompleteDrag(); }
    protected override void OnPointerCanceled(PointerRoutedEventArgs args) { base.OnPointerCanceled(args); CompleteDrag(); }
    protected override void OnPointerEntered(PointerRoutedEventArgs args)
    { base.OnPointerEntered(args); if (_pointer is null) VisualStateManager.GoToState(this, "PointerOver", false); }
    protected override void OnPointerExited(PointerRoutedEventArgs args)
    { base.OnPointerExited(args); if (_pointer is null) VisualStateManager.GoToState(this, "Normal", false); }
    private void CompleteDrag()
    {
        if (_pointer is null) return;
        _pointer = null; VisualStateManager.GoToState(this, "Normal", false); DragCompleted?.Invoke(this, EventArgs.Empty);
    }
    protected override void OnKeyDown(KeyRoutedEventArgs args)
    {
        if (args.Key is VirtualKey.Left or VirtualKey.Up or VirtualKey.Right or VirtualKey.Down)
        {
            KeyboardResize?.Invoke(this, args.Key is VirtualKey.Left or VirtualKey.Up ? -16d : 16d);
            args.Handled = true;
        }
        base.OnKeyDown(args);
    }
}

public sealed class StudioDockRequestEventArgs : EventArgs
{
    public StudioDockRequestEventArgs(string panelId, SvgPanelDock destination, int index = int.MaxValue)
    { PanelId = panelId; Destination = destination; Index = index; }
    public string PanelId { get; }
    public SvgPanelDock Destination { get; }
    public int Index { get; }
}

public sealed class StudioPanelItem
{
    public StudioPanelItem(string id, string title, FrameworkElement content) { Id = id; Title = title; Content = content; }
    public string Id { get; }
    public string Title { get; }
    public FrameworkElement Content { get; }
}

/// <summary>Retains live panel instances across tab switches; the host owns placement and persistence.</summary>
public sealed class StudioDockGroup : Control
{
    public const string DragFormat = "application/x-unodraw-panel";
    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(nameof(Caption), typeof(string), typeof(StudioDockGroup), new PropertyMetadata("Panels"));
    private readonly List<StudioPanelItem> _items = new();
    private readonly Dictionary<string, ToggleButton> _tabs = new(StringComparer.Ordinal);
    private StackPanel? _tabHost;
    private Grid? _contentHost;
    private Button? _menu;
    private Button? _collapse;
    private Thumb? _grip;
    private bool _collapsed;
    private string? _activeId;
    public string Caption { get => (string)GetValue(CaptionProperty); private set => SetValue(CaptionProperty, value); }
    public SvgPanelDock Dock { get; set; }
    public string WorkspaceToken { get; set; } = string.Empty;
    public string? ActiveId => _activeId;
    public IReadOnlyList<StudioPanelItem> Items => _items.AsReadOnly();
    /// <summary>Called before user-driven tab/collapse transitions. Return false to retain invalid input.</summary>
    public Func<bool>? CanChangePanel { get; set; }
    public event EventHandler<string>? ActiveChanged;
    public event EventHandler<StudioDockRequestEventArgs>? DockRequested;
    public event EventHandler<Point>? HeaderDragged;
    public event EventHandler? HeaderDragCompleted;
    public StudioDockGroup()
    {
        DefaultStyleKey = typeof(StudioDockGroup); IsTabStop = false; AllowDrop = true;
        DragOver += OnDragOver; Drop += OnDrop;
    }
    protected override void OnApplyTemplate()
    {
        if (_menu is not null) _menu.Click -= MenuClick;
        if (_collapse is not null) _collapse.Click -= CollapseClick;
        if (_grip is not null) { _grip.DragDelta -= GripDelta; _grip.DragCompleted -= GripCompleted; }
        _contentHost?.Children.Clear();
        base.OnApplyTemplate();
        _tabHost = GetTemplateChild("PART_Tabs") as StackPanel;
        _contentHost = GetTemplateChild("PART_Content") as Grid;
        _menu = GetTemplateChild("PART_Menu") as Button;
        _collapse = GetTemplateChild("PART_Collapse") as Button;
        _grip = GetTemplateChild("PART_Grip") as Thumb;
        if (_menu is not null) _menu.Click += MenuClick;
        if (_collapse is not null) _collapse.Click += CollapseClick;
        if (_grip is not null) { _grip.DragDelta += GripDelta; _grip.DragCompleted += GripCompleted; }
        Rebuild();
    }
    public void SetItems(IEnumerable<StudioPanelItem> items, string? activeId)
    {
        _contentHost?.Children.Clear(); _items.Clear(); _items.AddRange(items);
        _activeId = _items.Any(item => item.Id == activeId) ? activeId : _items.FirstOrDefault()?.Id;
        _collapsed = false;
        Rebuild();
    }
    public void Activate(string id)
    {
        if (!_items.Any(item => item.Id == id)) return;
        if (CanChangePanel?.Invoke() == false) { Sync(); return; }
        _activeId = id; _collapsed = false; Sync(); ActiveChanged?.Invoke(this, id);
    }
    private void Rebuild()
    {
        _tabs.Clear(); _tabHost?.Children.Clear(); _contentHost?.Children.Clear();
        if (_tabHost is null || _contentHost is null) return;
        foreach (var item in _items)
        {
            var tab = new ToggleButton { Content = item.Title, Tag = item.Id, MinHeight = 28, Padding = new Thickness(8, 3, 8, 3), FontSize = 11, CornerRadius = new CornerRadius(0), CanDrag = true };
            AutomationProperties.SetName(tab, item.Title + " panel");
            tab.Click += (_, _) => Activate(item.Id);
            tab.DragStarting += (_, args) =>
            {
                if (CanChangePanel?.Invoke() == false) { args.Cancel = true; return; }
                args.Data.SetData(DragFormat, WorkspaceToken + "|" + item.Id); args.Data.RequestedOperation = DataPackageOperation.Move;
            };
            tab.ContextFlyout = CreateMenu(item.Id);
            _tabs[item.Id] = tab; _tabHost.Children.Add(tab); _contentHost.Children.Add(item.Content);
        }
        Sync();
    }
    private void Sync()
    {
        Caption = _items.FirstOrDefault(item => item.Id == _activeId)?.Title ?? "Panels";
        foreach (var item in _items)
        {
            item.Content.Visibility = item.Id == _activeId && !_collapsed ? Visibility.Visible : Visibility.Collapsed;
            if (_tabs.TryGetValue(item.Id, out var button)) button.IsChecked = item.Id == _activeId;
        }
        if (_contentHost is not null) _contentHost.Visibility = _collapsed ? Visibility.Collapsed : Visibility.Visible;
        if (_collapse is not null) { _collapse.Content = _collapsed ? "＋" : "−"; AutomationProperties.SetName(_collapse, _collapsed ? "Expand panel" : "Collapse panel"); }
    }
    private MenuFlyout CreateMenu(string id)
    {
        var menu = new MenuFlyout();
        void Add(string text, SvgPanelDock dock, int index = int.MaxValue)
        {
            var item = new MenuFlyoutItem { Text = text };
            item.Click += (_, _) => DockRequested?.Invoke(this, new StudioDockRequestEventArgs(id, dock, index)); menu.Items.Add(item);
        }
        Add("Dock left", SvgPanelDock.Left); Add("Dock right · upper", SvgPanelDock.RightTop);
        Add("Dock right · lower", SvgPanelDock.RightBottom); Add("Dock below canvas", SvgPanelDock.Bottom); Add("Float in workspace", SvgPanelDock.Floating);
        menu.Items.Add(new MenuFlyoutSeparator());
        var current = _items.FindIndex(item => item.Id == id);
        Add("Move tab left", Dock, Math.Max(0, current - 1)); Add("Move tab right", Dock, current + 1);
        menu.Items.Add(new MenuFlyoutSeparator()); Add("Close panel", SvgPanelDock.Hidden); return menu;
    }
    private void MenuClick(object sender, RoutedEventArgs args) { if (_activeId is { } id) CreateMenu(id).ShowAt((FrameworkElement)sender); }
    private void CollapseClick(object sender, RoutedEventArgs args)
    {
        if (CanChangePanel?.Invoke() == false) return;
        _collapsed = !_collapsed; Sync();
    }
    private void GripDelta(object sender, DragDeltaEventArgs args) => HeaderDragged?.Invoke(this, new Point(args.HorizontalChange, args.VerticalChange));
    private void GripCompleted(object sender, DragCompletedEventArgs args) => HeaderDragCompleted?.Invoke(this, EventArgs.Empty);
    private void OnDragOver(object sender, DragEventArgs args)
    {
        if (!args.DataView.Contains(DragFormat)) return;
        args.AcceptedOperation = DataPackageOperation.Move; args.DragUIOverride.Caption = "Move panel here"; args.Handled = true;
    }
    private async void OnDrop(object sender, DragEventArgs args)
    {
        if (!args.DataView.Contains(DragFormat)) return;
        var deferral = args.GetDeferral();
        try
        {
            if (await args.DataView.GetDataAsync(DragFormat) is not string data || !data.StartsWith(WorkspaceToken + "|", StringComparison.Ordinal)) return;
            var id = data.Substring(WorkspaceToken.Length + 1);
            var position = args.GetPosition(_tabHost ?? (UIElement)this);
            var index = _items.Count;
            if (_tabHost is not null && position.Y <= _tabHost.ActualHeight + 8)
            {
                index = 0;
                foreach (var tab in _tabHost.Children.OfType<FrameworkElement>())
                {
                    var origin = tab.TransformToVisual(_tabHost).TransformPoint(default);
                    if (position.X < origin.X + tab.ActualWidth / 2) break;
                    index++;
                }
            }
            var original = _items.FindIndex(item => item.Id == id);
            if (original >= 0 && original < index) index--;
            DockRequested?.Invoke(this, new StudioDockRequestEventArgs(id, Dock, index)); args.Handled = true;
        }
        catch (Exception) { /* Malformed external drag data is ignored. */ }
        finally { deferral.Complete(); }
    }
}
