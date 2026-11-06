using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Svg.Editor.Skia;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.System;

namespace Svg.Editor.Skia.Uno;

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
    public StudioPanelItem(string id, string title, FrameworkElement content)
    { Id = id; Title = title; Content = content; }
    public string Id { get; }
    public string Title { get; }
    public FrameworkElement Content { get; }
}

/// <summary>Retains live panel instances across tab switches; the host owns placement and persistence.</summary>
public sealed partial class StudioDockGroup : Control
{
    public const string DragFormat = "application/x-unodraw-panel";
    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(nameof(Caption), typeof(string), typeof(StudioDockGroup), new PropertyMetadata("Panels"));
    private readonly List<StudioPanelItem> _items = new();
    private readonly IReadOnlyList<StudioPanelItem> _readOnlyItems;
    private readonly Dictionary<string, StudioTabButton> _tabs = new(StringComparer.Ordinal);
    private StackPanel? _tabHost;
    private ScrollViewer? _tabScroll;
    private Grid? _contentHost;
    private Button? _menu, _collapse, _previous, _next, _picker;
    private Thumb? _grip;
    private bool _collapsed;
    private string? _activeId;
    private int _revealVersion;
    public string Caption { get => (string)GetValue(CaptionProperty); private set => SetValue(CaptionProperty, value); }
    public SvgPanelDock Dock { get; set; }
    public string WorkspaceToken { get; set; } = string.Empty;
    public string? ActiveId => _activeId;
    public IReadOnlyList<StudioPanelItem> Items => _readOnlyItems;
    public Func<bool>? CanChangePanel { get; set; }
    public event EventHandler<string>? ActiveChanged;
    public event EventHandler<StudioDockRequestEventArgs>? DockRequested;
    public event EventHandler<Point>? HeaderDragged;
    public event EventHandler? HeaderDragCompleted;

    public StudioDockGroup()
    {
        _readOnlyItems = _items.AsReadOnly();
        DefaultStyleKey = typeof(StudioDockGroup); IsTabStop = false; AllowDrop = true;
        DragOver += OnDragOver; Drop += OnDrop;
        SizeChanged += (_, _) => { UpdateTabNavigation(); RevealActiveTab(); };
        Loaded += (_, _) => { UpdateTabNavigation(); RevealActiveTab(); };
        Unloaded += (_, _) => { _revealVersion++; ClosePanelSwitcher(); };
    }

    protected override void OnApplyTemplate()
    {
        ClosePanelSwitcher();
        if (_menu is not null) _menu.Click -= MenuClick;
        if (_collapse is not null) _collapse.Click -= CollapseClick;
        if (_previous is not null) _previous.Click -= PreviousClick;
        if (_next is not null) _next.Click -= NextClick;
        if (_picker is not null) _picker.Click -= PickerClick;
        if (_tabScroll is not null) _tabScroll.ViewChanged -= TabViewChanged;
        if (_tabHost is not null) _tabHost.SizeChanged -= TabSizeChanged;
        if (_grip is not null) { _grip.DragDelta -= GripDelta; _grip.DragCompleted -= GripCompleted; }
        _contentHost?.Children.Clear();
        base.OnApplyTemplate();
        _tabHost = GetTemplateChild("PART_Tabs") as StackPanel;
        _tabScroll = GetTemplateChild("PART_TabScroll") as ScrollViewer;
        _contentHost = GetTemplateChild("PART_Content") as Grid;
        _menu = GetTemplateChild("PART_Menu") as Button;
        _collapse = GetTemplateChild("PART_Collapse") as Button;
        _previous = GetTemplateChild("PART_PreviousTabs") as Button;
        _next = GetTemplateChild("PART_NextTabs") as Button;
        _picker = GetTemplateChild("PART_PanelPicker") as Button;
        _grip = GetTemplateChild("PART_Grip") as Thumb;
        if (_menu is not null) _menu.Click += MenuClick;
        if (_collapse is not null) _collapse.Click += CollapseClick;
        if (_previous is not null) _previous.Click += PreviousClick;
        if (_next is not null) _next.Click += NextClick;
        if (_picker is not null) _picker.Click += PickerClick;
        if (_tabScroll is not null) _tabScroll.ViewChanged += TabViewChanged;
        if (_tabHost is not null) _tabHost.SizeChanged += TabSizeChanged;
        if (_grip is not null) { _grip.DragDelta += GripDelta; _grip.DragCompleted += GripCompleted; }
        Rebuild();
    }

    public void SetItems(IEnumerable<StudioPanelItem> items, string? activeId)
    {
        ArgumentNullException.ThrowIfNull(items);
        var snapshot = items.ToArray();
        if (snapshot.Any(item => item is null || string.IsNullOrEmpty(item.Id) || item.Content is null) ||
            snapshot.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
            throw new ArgumentException("Panel items must have unique nonempty IDs and live content.", nameof(items));
        ClosePanelSwitcher();
        _contentHost?.Children.Clear(); _items.Clear(); _items.AddRange(snapshot);
        _activeId = _items.Any(item => item.Id == activeId) ? activeId : _items.FirstOrDefault()?.Id;
        _collapsed = false; Rebuild();
    }

    public void Activate(string id)
    {
        if (!_items.Any(item => item.Id == id)) return;
        if (CanChangePanel?.Invoke() == false) { Sync(); return; }
        _activeId = id; _collapsed = false;
        Sync(); ActiveChanged?.Invoke(this, id); RevealActiveTab();
    }

    private void Rebuild()
    {
        _tabs.Clear(); _tabHost?.Children.Clear(); _contentHost?.Children.Clear();
        if (_tabHost is null || _contentHost is null) return;
        foreach (var item in _items)
        {
            var tab = new StudioTabButton { Content = item.Title, Tag = item.Id, CanDrag = true };
            AutomationProperties.SetName(tab, item.Title + " panel");
            AutomationProperties.SetAutomationId(tab, "studio-tab-" + item.Id);
            ToolTipService.SetToolTip(tab, item.Title + " · arrow keys switch panels; right-click for docking");
            tab.Click += (_, _) => Activate(item.Id);
            tab.KeyDown += (_, args) =>
            {
                if (_items.Count == 0) return;
                var current = _items.FindIndex(candidate => candidate.Id == item.Id);
                var direction = FlowDirection == FlowDirection.RightToLeft ? -1 : 1;
                var next = args.Key switch
                {
                    VirtualKey.Left => (current - direction + _items.Count) % _items.Count,
                    VirtualKey.Right => (current + direction + _items.Count) % _items.Count,
                    VirtualKey.Home => 0,
                    VirtualKey.End => _items.Count - 1,
                    _ => -1
                };
                if (next < 0) return;
                Activate(_items[next].Id);
                if (_activeId is { } active && _tabs.TryGetValue(active, out var activeTab)) activeTab.Focus(FocusState.Keyboard);
                args.Handled = true;
            };
            tab.DragStarting += (_, args) =>
            {
                if (CanChangePanel?.Invoke() == false) { args.Cancel = true; return; }
                args.Data.SetData(DragFormat, WorkspaceToken + "|" + item.Id);
                args.Data.RequestedOperation = DataPackageOperation.Move;
            };
            tab.ContextFlyout = CreateMenu(item.Id);
            _tabs[item.Id] = tab; _tabHost.Children.Add(tab); _contentHost.Children.Add(item.Content);
        }
        Sync(); UpdateTabNavigation(); RevealActiveTab();
    }

    private void RevealActiveTab()
    {
        var version = ++_revealVersion;
        var expected = _activeId;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (version != _revealVersion || !IsLoaded || expected != _activeId || expected is null || !_tabs.TryGetValue(expected, out var tab)) return;
            if (_tabScroll is { ViewportWidth: > 0 } scroll && _tabHost is not null)
            {
                var left = tab.TransformToVisual(_tabHost).TransformPoint(default).X;
                var right = left + tab.ActualWidth;
                var offset = scroll.HorizontalOffset;
                if (left < offset) offset = left;
                else if (right > offset + scroll.ViewportWidth) offset = right - scroll.ViewportWidth;
                scroll.ChangeView(Math.Clamp(offset, 0, scroll.ScrollableWidth), null, null, true);
            }
            else tab.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
            UpdateTabNavigation();
        });
    }

    private void UpdateTabNavigation()
    {
        if (_tabHost is null || _tabScroll is null) return;
        var showPicker = _items.Count > 1;
        if (_picker is not null)
        {
            _picker.Visibility = showPicker ? Visibility.Visible : Visibility.Collapsed;
            AutomationProperties.SetName(_picker, "Choose panel · " + _items.Count + " available");
            AutomationProperties.SetAutomationId(_picker, "studio-panel-picker-" + Dock);
        }
        var pickerWidth = showPicker ? 28d : 0d;
        var overflow = _tabHost.DesiredSize.Width > Math.Max(0, ActualWidth - pickerWidth - 2) + 0.5;
        UpdateScrollArrow(_previous, overflow, _tabScroll.HorizontalOffset > 0.5);
        UpdateScrollArrow(_next, overflow, _tabScroll.HorizontalOffset < _tabScroll.ScrollableWidth - 0.5);
    }

    private void UpdateScrollArrow(Button? button, bool visible, bool enabled)
    {
        if (button is null) return;
        // Do not strand keyboard focus on an arrow that disables itself after a click.
        // Move to stationary chrome before changing availability, without activating
        // a tab or revealing the active tab and undoing the user's deliberate scroll.
        if ((!visible || !enabled) && button.FocusState != FocusState.Unfocused &&
            _picker is { IsLoaded: true, IsEnabled: true, Visibility: Visibility.Visible })
            _picker.Focus(FocusState.Programmatic);
        button.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        button.IsEnabled = enabled;
    }
    private void TabViewChanged(object? sender, ScrollViewerViewChangedEventArgs args) => UpdateTabNavigation();
    private void TabSizeChanged(object sender, SizeChangedEventArgs args) => UpdateTabNavigation();
    private void PreviousClick(object sender, RoutedEventArgs args) => ScrollTabs(-1);
    private void NextClick(object sender, RoutedEventArgs args) => ScrollTabs(1);
    private void ScrollTabs(int direction)
    {
        if (_tabScroll is not { } scroll) return;
        var offset = scroll.HorizontalOffset + direction * Math.Max(64, scroll.ViewportWidth * 0.75);
        scroll.ChangeView(Math.Clamp(offset, 0, scroll.ScrollableWidth), null, null, true);
    }
    private void PickerClick(object sender, RoutedEventArgs args) => OpenPanelSwitcher((FrameworkElement)sender);

    private void Sync()
    {
        Caption = _items.FirstOrDefault(item => item.Id == _activeId)?.Title ?? "Panels";
        foreach (var item in _items)
        {
            item.Content.Visibility = item.Id == _activeId && !_collapsed ? Visibility.Visible : Visibility.Collapsed;
            if (_tabs.TryGetValue(item.Id, out var button)) button.IsChecked = item.Id == _activeId;
        }
        if (_contentHost is not null) _contentHost.Visibility = _collapsed ? Visibility.Collapsed : Visibility.Visible;
        if (_collapse is not null)
        {
            _collapse.Content = _collapsed ? "＋" : "−";
            AutomationProperties.SetName(_collapse, _collapsed ? "Expand panel" : "Collapse panel");
        }
    }

    private MenuFlyout CreateMenu(string id)
    {
        var menu = new MenuFlyout();
        void Add(string text, SvgPanelDock dock, int index = int.MaxValue)
        {
            var item = new MenuFlyoutItem { Text = text };
            item.Click += (_, _) => DockRequested?.Invoke(this, new StudioDockRequestEventArgs(id, dock, index));
            menu.Items.Add(item);
        }
        Add("Dock left", SvgPanelDock.Left); Add("Dock right · upper", SvgPanelDock.RightTop);
        Add("Dock right · lower", SvgPanelDock.RightBottom); Add("Dock below canvas", SvgPanelDock.Bottom);
        Add("Float in workspace", SvgPanelDock.Floating);
        menu.Items.Add(new MenuFlyoutSeparator());
        var current = _items.FindIndex(item => item.Id == id);
        Add("Move tab left", Dock, Math.Max(0, current - 1)); Add("Move tab right", Dock, current + 1);
        menu.Items.Add(new MenuFlyoutSeparator()); Add("Close panel", SvgPanelDock.Hidden);
        return menu;
    }

    private void MenuClick(object sender, RoutedEventArgs args)
    { if (_activeId is { } id) CreateMenu(id).ShowAt((FrameworkElement)sender); }
    private void CollapseClick(object sender, RoutedEventArgs args)
    { if (CanChangePanel?.Invoke() != false) { _collapsed = !_collapsed; Sync(); } }
    private void GripDelta(object sender, DragDeltaEventArgs args)
        => HeaderDragged?.Invoke(this, new Point(args.HorizontalChange, args.VerticalChange));
    private void GripCompleted(object sender, DragCompletedEventArgs args)
        => HeaderDragCompleted?.Invoke(this, EventArgs.Empty);
    private void OnDragOver(object sender, DragEventArgs args)
    {
        if (!args.DataView.Contains(DragFormat)) return;
        args.AcceptedOperation = DataPackageOperation.Move;
        args.DragUIOverride.Caption = "Move panel here"; args.Handled = true;
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
