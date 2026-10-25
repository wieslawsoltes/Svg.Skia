using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Svg.Editor.Skia;
using Windows.Storage;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private const string StudioLayoutKey = "UnoDraw.Studio.Layout.v1";
    private readonly string _studioToken = Guid.NewGuid().ToString("N");
    private SvgWorkspaceLayout _studioLayout = new();
    private readonly Dictionary<string, StudioPanelItem> _studioPanels = new(StringComparer.Ordinal);
    private readonly Dictionary<SvgPanelDock, StudioDockGroup> _studioGroups = new();
    private readonly Dictionary<string, (Grid Host, StudioDockGroup Group)> _studioFloating = new(StringComparer.Ordinal);
    private bool _studioInitialized;
    private bool _updatingStudioLayout;
    private bool _syncingStudioPreset;
    private string? _temporaryStudioPanel;
    private int _floatingZ;

    /// <summary>Export host UI layout independently from the SVG document and undo stack.</summary>
    public string CaptureWorkspaceLayout() => _studioLayout.Serialize();
    public void RestoreWorkspaceLayout(string xml)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(SvgEditorWorkspace));
        var restored = SvgWorkspaceLayout.Parse(xml);
        CommitInspectorEdits(); _studioLayout = restored; _temporaryStudioPanel = null;
        RebuildStudioDocks(); ApplyStudioMetrics(); SaveStudioLayout();
    }

    private void InitializeStudioWorkspace()
    {
        try
        {
            if (ApplicationData.Current.LocalSettings.Values[StudioLayoutKey] is string xml) _studioLayout = SvgWorkspaceLayout.Parse(xml);
        }
        catch (Exception) { _studioLayout = new SvgWorkspaceLayout(); }
        _studioGroups.Add(SvgPanelDock.Left, LeftDock);
        _studioGroups.Add(SvgPanelDock.RightTop, RightTopDock);
        _studioGroups.Add(SvgPanelDock.RightBottom, RightBottomDock);
        _studioGroups.Add(SvgPanelDock.Bottom, BottomDock);
        foreach (var pair in _studioGroups)
        {
            pair.Value.Dock = pair.Key; pair.Value.WorkspaceToken = _studioToken; WireStudioGroup(pair.Value);
            var dragX = 0d; var dragY = 0d;
            pair.Value.HeaderDragged += (_, point) => { dragX += point.X; dragY += point.Y; };
            pair.Value.HeaderDragCompleted += (_, _) =>
            {
                if (Math.Abs(dragX) + Math.Abs(dragY) > 48 && pair.Value.ActiveId is { } id)
                    MoveStudioPanel(new StudioDockRequestEventArgs(id, SvgPanelDock.Floating));
                dragX = dragY = 0;
            };
        }
        RegisterStudioPanel("properties", "Properties", InspectorPanel);
        RegisterStudioPanel("layers", "Layers", OutlinePanel);
        RegisterStudioPanel("attributes", "SVG attributes", AttributesPanel);
        RegisterStudioPanel("resources", "Resources", ResourcesPanel);
        BuildStudioPanels(); BuildStudioContextBar(); BuildStudioPanelRail();
        foreach (var field in _studioTextFields)
        {
            field.CaptureTargetValidation = CaptureStudioTargetValidation;
            field.EditCanceled += (_, _) => RefreshStudioPanels();
        }
        foreach (var field in _strokeWidths.Concat(_opacities)
            .Concat(new[] { _studioX, _studioY, _studioW, _studioH, _studioAngle, _studioMiter }.OfType<StudioNumericField>()).Distinct())
        {
            field.CaptureTargetValidation = CaptureStudioTargetValidation;
            field.EditRejected += (_, message) => StatusText.Text = message;
        }
        WorkspacePresetPicker.Items.Clear();
        _studioInitialized = true;
        WorkspacePresetPicker.PlaceholderText = "Custom workspace";
        SynchronizeStudioPreset(); RebuildStudioDocks();
        LeftDockSplitter.DragDelta += (_, args) => ResizeStudioDock(SvgPanelDock.Left, _studioLayout.LeftWidth + args.HorizontalChange);
        RightDockSplitter.DragDelta += (_, args) => ResizeStudioDock(SvgPanelDock.RightTop, _studioLayout.RightWidth - args.HorizontalChange);
        BottomDockSplitter.DragDelta += (_, args) => ResizeStudioDock(SvgPanelDock.Bottom, _studioLayout.BottomHeight - args.VerticalChange);
        LeftDockSplitter.KeyboardResize += (_, delta) => { ResizeStudioDock(SvgPanelDock.Left, _studioLayout.LeftWidth + delta); SaveStudioLayout(); };
        RightDockSplitter.KeyboardResize += (_, delta) => { ResizeStudioDock(SvgPanelDock.RightTop, _studioLayout.RightWidth - delta); SaveStudioLayout(); };
        BottomDockSplitter.KeyboardResize += (_, delta) => { ResizeStudioDock(SvgPanelDock.Bottom, _studioLayout.BottomHeight - delta); SaveStudioLayout(); };
        foreach (var splitter in new[] { LeftDockSplitter, RightDockSplitter, BottomDockSplitter, RightGroupSplitter })
            splitter.DragCompleted += (_, _) => SaveStudioLayout();
        RightGroupSplitter.DragDelta += (_, args) =>
        {
            _studioLayout.SetRightSplit(_studioLayout.RightTopRatio + args.VerticalChange / Math.Max(1, RightDockContainer.ActualHeight)); ApplyStudioMetrics();
        };
        RightGroupSplitter.KeyboardResize += (_, delta) =>
        { _studioLayout.SetRightSplit(_studioLayout.RightTopRatio + delta / Math.Max(1, RightDockContainer.ActualHeight)); ApplyStudioMetrics(); SaveStudioLayout(); };
        Canvas.ViewportChanged += (_, _) => UpdateStudioRulers();
        Canvas.PointerPositionChanged += (_, _) => UpdateStudioRulers();
        ActualThemeChanged += (_, _) => UpdateStudioRulers();
        ApplyStudioMetrics();
    }

    private Func<bool> CaptureStudioTargetValidation()
    {
        var context = Controller is { } controller ? new SvgEditorSelectionContext(controller) : null;
        return () => !_disposed && context is not null && context.IsCurrent(Controller);
    }
    private void RegisterStudioPanel(string id, string title, FrameworkElement content)
    {
        PanelStaging.Children.Remove(content);
        content.HorizontalAlignment = HorizontalAlignment.Stretch; content.VerticalAlignment = VerticalAlignment.Stretch;
        _studioPanels.Add(id, new StudioPanelItem(id, title, content));
    }
    private void WireStudioGroup(StudioDockGroup group)
    {
        group.CanChangePanel = () =>
        {
            if (_disposed || _closing || _dialogVisible) return false;
            try { CommitInspectorEdits(); return true; }
            catch (Exception exception) { StatusText.Text = exception.Message; return false; }
        };
        group.DockRequested += (_, args) => MoveStudioPanel(args);
        group.ActiveChanged += (_, id) => { _studioLayout.Activate(id); SaveStudioLayout(); };
    }
    private void MoveStudioPanel(StudioDockRequestEventArgs args)
    {
        ApplySafely(() =>
        {
            if (_disposed || !_studioPanels.ContainsKey(args.PanelId)) return;
            CommitInspectorEdits();
            if (_temporaryStudioPanel == args.PanelId && args.Destination == SvgPanelDock.Hidden) _temporaryStudioPanel = null;
            else { _temporaryStudioPanel = null; _studioLayout.Move(args.PanelId, args.Destination, args.Index); }
            RebuildStudioDocks(); ApplyStudioMetrics(); SaveStudioLayout();
        });
    }
    private void RebuildStudioDocks()
    {
        if (!_studioInitialized) return;
        foreach (var group in _studioGroups.Values) group.SetItems(Array.Empty<StudioPanelItem>(), null);
        foreach (var floating in _studioFloating.Values) floating.Group.SetItems(Array.Empty<StudioPanelItem>(), null);
        FloatingPanelLayer.Children.Clear(); _studioFloating.Clear();
        foreach (var pair in _studioGroups)
        {
            var items = _studioLayout.Panels.Where(panel => panel.Dock == pair.Key && panel.Id != _temporaryStudioPanel).Select(panel => _studioPanels[panel.Id]);
            pair.Value.SetItems(items, _studioLayout.Active(pair.Key));
        }
        foreach (var panel in _studioLayout.Panels.Where(panel => panel.Dock == SvgPanelDock.Floating || panel.Id == _temporaryStudioPanel))
        {
            var group = new StudioDockGroup { Dock = SvgPanelDock.Floating, WorkspaceToken = _studioToken };
            WireStudioGroup(group); group.SetItems(new[] { _studioPanels[panel.Id] }, panel.Id);
            var host = new Grid(); host.Children.Add(group);
            var resize = new StudioSplitter { Width = 14, Height = 14, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
            host.Children.Add(resize);
            var id = panel.Id;
            group.HeaderDragged += (_, delta) => AdjustStudioFloat(id, delta.X, delta.Y, 0, 0);
            group.HeaderDragCompleted += (_, _) => SaveStudioLayout();
            resize.DragDelta += (_, args) => AdjustStudioFloat(id, 0, 0, args.HorizontalChange, args.VerticalChange);
            resize.DragCompleted += (_, _) => SaveStudioLayout();
            resize.KeyboardResize += (_, delta) => { AdjustStudioFloat(id, 0, 0, delta, delta); SaveStudioLayout(); };
            host.PointerPressed += (_, _) => Microsoft.UI.Xaml.Controls.Canvas.SetZIndex(host, ++_floatingZ);
            _studioFloating.Add(id, (host, group)); FloatingPanelLayer.Children.Add(host); PositionStudioFloat(id);
        }
    }
    private SvgWorkspaceFloatingBounds StudioFloatBounds(string id)
        => SvgWorkspaceFloatingBounds.Project(_studioLayout.Find(id),
            ActualWidth > 0 ? Math.Max(1, ActualWidth - 12) : 16384,
            EditorBody.ActualHeight > 0 ? Math.Max(1, EditorBody.ActualHeight - 8) : 16384);
    private void AdjustStudioFloat(string id, double dx, double dy, double dw, double dh)
    {
        var panel = _studioLayout.Find(id);
        var displayed = StudioFloatBounds(id);
        var width = dw == 0 ? panel.Width : displayed.Width + dw;
        var height = dh == 0 ? panel.Height : displayed.Height + dh;
        var availableWidth = Math.Max(1, ActualWidth - 12);
        var availableHeight = Math.Max(1, EditorBody.ActualHeight - 8);
        var x = Math.Clamp(displayed.X + dx, 0, Math.Max(0, availableWidth - Math.Min(width, availableWidth)));
        var y = Math.Clamp(displayed.Y + dy, 0, Math.Max(0, availableHeight - Math.Min(height, availableHeight)));
        _studioLayout.SetFloatingBounds(id, x, y, width, height, 16384, 16384);
        PositionStudioFloat(id);
    }
    private void PositionStudioFloat(string id)
    {
        if (!_studioFloating.TryGetValue(id, out var floating)) return;
        var bounds = StudioFloatBounds(id);
        floating.Host.Width = bounds.Width; floating.Host.Height = bounds.Height;
        Microsoft.UI.Xaml.Controls.Canvas.SetLeft(floating.Host, bounds.X);
        Microsoft.UI.Xaml.Controls.Canvas.SetTop(floating.Host, bounds.Y);
    }
    private void ResizeStudioDock(SvgPanelDock dock, double extent) { _studioLayout.Resize(dock, extent); ApplyStudioMetrics(); }
    private void ApplyStudioMetrics()
    {
        if (!_studioInitialized || _updatingStudioLayout) return;
        _updatingStudioLayout = true;
        try
        {
            var metrics = _studioLayout.Measure(Math.Max(0, ActualWidth), Math.Max(0, ActualHeight), _zenMode);
            LeftDockColumn.Width = new GridLength(metrics.LeftWidth); LeftSplitterColumn.Width = new GridLength(metrics.LeftWidth > 0 ? 6 : 0);
            RightDockColumn.Width = new GridLength(metrics.RightWidth); RightSplitterColumn.Width = new GridLength(metrics.RightWidth > 0 ? 6 : 0);
            BottomDockRow.Height = new GridLength(metrics.BottomHeight); BottomSplitterRow.Height = new GridLength(metrics.BottomHeight > 0 ? 6 : 0);
            LeftDock.Visibility = metrics.LeftWidth > 0 ? Visibility.Visible : Visibility.Collapsed;
            RightDockContainer.Visibility = metrics.RightWidth > 0 ? Visibility.Visible : Visibility.Collapsed;
            BottomDock.Visibility = metrics.BottomHeight > 0 ? Visibility.Visible : Visibility.Collapsed;
            LeftDockSplitter.Visibility = metrics.LeftWidth > 0 ? Visibility.Visible : Visibility.Collapsed;
            RightDockSplitter.Visibility = metrics.RightWidth > 0 ? Visibility.Visible : Visibility.Collapsed;
            BottomDockSplitter.Visibility = metrics.BottomHeight > 0 ? Visibility.Visible : Visibility.Collapsed;
            var top = RightTopDock.Items.Count > 0; var bottom = RightBottomDock.Items.Count > 0;
            RightTopRow.Height = top ? new GridLength(bottom ? _studioLayout.RightTopRatio : 1, GridUnitType.Star) : new GridLength(0);
            RightBottomRow.Height = bottom ? new GridLength(top ? 1 - _studioLayout.RightTopRatio : 1, GridUnitType.Star) : new GridLength(0);
            RightDockContainer.RowDefinitions[1].Height = new GridLength(top && bottom ? 6 : 0);
            RightGroupSplitter.Visibility = top && bottom ? Visibility.Visible : Visibility.Collapsed;
            var rulers = _studioLayout.ShowRulers && !_zenMode;
            RulerRow.Height = new GridLength(rulers ? 22 : 0); RulerColumn.Width = new GridLength(rulers ? 22 : 0);
            HorizontalRuler.Visibility = VerticalRuler.Visibility = rulers ? Visibility.Visible : Visibility.Collapsed;
            WorkspaceMenu.Visibility = MenuDivider.Visibility = metrics.Compact ? Visibility.Collapsed : Visibility.Visible;
            CompactMenuButton.Visibility = metrics.Compact ? Visibility.Visible : Visibility.Collapsed;
            BrandTitle.Visibility = ActualWidth < 560 ? Visibility.Collapsed : Visibility.Visible;
            WorkspacePresetPicker.Visibility = ActualWidth < 760 ? Visibility.Collapsed : Visibility.Visible;
            CommandsLabel.Visibility = CommandsShortcut.Visibility = ActualWidth < 1200 ? Visibility.Collapsed : Visibility.Visible;
            ThemeButton.Visibility = ActualWidth < 480 ? Visibility.Collapsed : Visibility.Visible;
            ExportButton.Visibility = ActualWidth < 640 ? Visibility.Collapsed : Visibility.Visible;
            WorkspaceLabel.Visibility = ActualWidth < 1000 ? Visibility.Collapsed : Visibility.Visible;
            PositionText.Visibility = ActualWidth < 700 ? Visibility.Collapsed : Visibility.Visible;
            ContextSelectionLabel.Visibility = ActualWidth < 560 ? Visibility.Collapsed : Visibility.Visible;
            ToolColumn.Width = new GridLength(_studioLayout.TouchDensity ? 96 : 68);
            foreach (var button in ToolRail.Children.OfType<Control>())
            { button.Width = _studioLayout.TouchDensity ? 42 : 30; button.Height = _studioLayout.TouchDensity ? 44 : 32; }
            foreach (var id in _studioFloating.Keys) PositionStudioFloat(id);
            FloatingPanelLayer.Visibility = _zenMode || _studioLayout.PanelsHidden ? Visibility.Collapsed : Visibility.Visible;
            UpdateStudioRulers();
        }
        finally { _updatingStudioLayout = false; }
    }
    private void OpenStudioPanel(string id)
    {
        if (!_studioPanels.ContainsKey(id)) return;
        CommitInspectorEdits(); _zenMode = false; _studioLayout.PanelsHidden = false; _studioLayout.Activate(id);
        var panel = _studioLayout.Find(id);
        var metrics = _studioLayout.Measure(Math.Max(0, ActualWidth), Math.Max(0, ActualHeight));
        var collapsedDock = panel.Dock == SvgPanelDock.Left && metrics.LeftWidth == 0
            || (panel.Dock is SvgPanelDock.RightTop or SvgPanelDock.RightBottom) && metrics.RightWidth == 0
            || panel.Dock == SvgPanelDock.Bottom && metrics.BottomHeight == 0;
        var temporary = collapsedDock ? id : null;
        if (_temporaryStudioPanel != temporary)
        { _temporaryStudioPanel = temporary; RebuildStudioDocks(); }
        else if (_studioFloating.TryGetValue(id, out var floating))
        { floating.Group.Activate(id); Microsoft.UI.Xaml.Controls.Canvas.SetZIndex(floating.Host, ++_floatingZ); }
        else if (_studioGroups.TryGetValue(panel.Dock, out var group) && group.Items.Any(item => item.Id == id)) group.Activate(id);
        else RebuildStudioDocks();
        ApplyStudioMetrics(); RefreshStudioPanels(); SaveStudioLayout();
    }
    private void WorkspacePreset_SelectionChanged(object sender, SelectionChangedEventArgs args) => SelectNamedWorkspaceChoice();
    private void SynchronizeStudioPreset() => SynchronizeNamedWorkspacePicker();
    private void ResetStudioLayout(SvgWorkspacePreset preset)
    {
        CommitInspectorEdits(); _activeNamedWorkspaceId = null; _studioLayout.Reset(preset); _temporaryStudioPanel = null; _zenMode = false;
        RebuildStudioDocks(); ApplyStudioMetrics(); SaveStudioLayout();
    }
    private void SaveStudioLayout()
    {
        if (!_studioInitialized || _disposed) return;
        SynchronizeStudioPreset();
        try { ApplicationData.Current.LocalSettings.Values[StudioLayoutKey] = _studioLayout.Serialize(); }
        catch (Exception exception) { StatusText.Text = "Workspace changes could not be saved: " + exception.Message; }
    }
    private void UpdateStudioRulers()
    {
        if (!_studioInitialized) return;
        var origin = Canvas.Viewport.WorldToView(default); var pointer = Canvas.Viewport.WorldToView(Canvas.PointerPosition);
        HorizontalRuler.Update(origin.X, Canvas.Zoom, pointer.X, ActualTheme == ElementTheme.Dark);
        VerticalRuler.Update(origin.Y, Canvas.Zoom, pointer.Y, ActualTheme == ElementTheme.Dark);
    }
    private void DisposeStudioWorkspace()
    {
        foreach (var group in _studioGroups.Values) group.SetItems(Array.Empty<StudioPanelItem>(), null);
        foreach (var floating in _studioFloating.Values) floating.Group.SetItems(Array.Empty<StudioPanelItem>(), null);
        FloatingPanelLayer.Children.Clear(); _studioFloating.Clear(); _studioPanels.Clear(); HorizontalRuler.Dispose(); VerticalRuler.Dispose();
    }
}
