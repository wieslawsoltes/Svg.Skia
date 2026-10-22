using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Svg.Editor.Core;
using Svg.Editor.Skia;
using Svg.Editor.Svg;
using Windows.System;

namespace Svg.Editor.Skia.Uno;

/// <summary>A reusable Uno workspace. The SVG controller remains independent of Uno.</summary>
public sealed partial class SvgEditorWorkspace : UserControl, IDisposable
{
    private readonly List<EditorCommand> _commands = new();
    private readonly Dictionary<SvgEditorToolKind, ToggleButton> _toolButtons = new();
    private readonly DispatcherTimer _recoveryTimer = new() { Interval = TimeSpan.FromSeconds(20) };
    private readonly DispatcherTimer _animationTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly System.Diagnostics.Stopwatch _animationClock = new();
    private EditorDocument? _current;
    private SvgElement? _inspectedElement;
    private long _outlineRevision = -1;
    private bool _refreshPending;
    private bool _refreshing;
    private bool _disposed;
    private bool _closing;
    private bool _dialogVisible;
    private bool _spacePanning;
    private bool _zenMode;

    public SvgEditorWorkspace()
    {
        InitializeComponent();
        BuildCommands();
        BuildMenus();
        BuildTools();
        Canvas.InteractionStarting += (_, _) =>
        {
            if (!_dialogVisible)
                CommitInspectorEdits();
        };
        Canvas.ViewportChanged += (_, _) => ZoomLabel.Content = $"{Canvas.ZoomPercent:0.#}%";
        Canvas.PointerPositionChanged += (_, _) => PositionText.Text = $"X {Canvas.PointerPosition.X:0.0}  Y {Canvas.PointerPosition.Y:0.0}";
        Canvas.InteractionFailed += async (_, args) => await ShowErrorAsync(args.Exception.Message);
        ActualThemeChanged += (_, _) => Canvas.IsDarkTheme = ActualTheme == ElementTheme.Dark;
        SizeChanged += (_, _) => UpdateResponsiveLayout();
        _recoveryTimer.Tick += async (_, _) => await SaveRecoveryAsync();
        _animationTimer.Tick += (_, _) =>
        {
            if (Controller is not { } controller)
                return;
            controller.Renderer.SetAnimationTime(_animationClock.Elapsed);
            Canvas.Invalidate();
        };
        Loaded += (_, _) =>
        {
            Canvas.IsDarkTheme = ActualTheme == ElementTheme.Dark;
            _recoveryTimer.Start();
            Focus(FocusState.Programmatic);
        };
        Unloaded += (_, _) =>
        {
            _recoveryTimer.Stop();
            _animationTimer.Stop();
            Canvas.CancelGesture();
        };
    }

    public ObservableCollection<EditorDocument> Documents { get; } = new();
    public EditorDocument? CurrentDocument => _current;
    public SvgEditorController? Controller => _current?.Controller;
    public SvgEditorCanvas Surface => Canvas;
    public Window? HostWindow { get; set; }
    public event EventHandler<string>? WorkspaceTitleChanged;

    public void NewDocument(float width = 1200, float height = 800)
    {
        CommitInspectorEdits();
        var document = new EditorDocument { Name = $"Untitled {Documents.Count + 1}.svg" };
        document.Controller.NewDocument(width, height);
        AddDocument(document);
        StatusText.Text = "New artboard. Choose a tool to start drawing.";
    }

    private void AddDocument(EditorDocument document)
    {
        CommitInspectorEdits();
        Documents.Add(document);
        document.Controller.Changed += Controller_Changed;
        var tab = new TabViewItem { Header = document.Title, Tag = document, IsClosable = true };
        DocumentTabs.TabItems.Add(tab);
        DocumentTabs.SelectedItem = tab;
        ActivateDocument(document);
    }

    private void ActivateDocument(EditorDocument document)
    {
        if (ReferenceEquals(document, _current))
            return;
        CommitInspectorEdits();
        Canvas.CancelGesture();
        if (_current is { } previous)
        {
            var center = Canvas.Viewport.ViewToWorld(new SkiaSharp.SKPoint((float)Canvas.Viewport.Width / 2, (float)Canvas.Viewport.Height / 2));
            previous.Camera = (Canvas.Zoom, center.X, center.Y);
        }
        _animationTimer.Stop();
        _animationClock.Reset();
        _current = document;
        _inspectedElement = null;
        _outlineRevision = -1;
        Canvas.Controller = document.Controller;
        Canvas.Tool = document.Controller.Session.CurrentTool;
        Canvas.ShowGrid = document.Controller.Session.Settings.ShowGrid;
        SnapToggle.IsChecked = document.Controller.Session.Settings.SnapToGrid;
        RefreshWorkspace();
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!ReferenceEquals(document, _current))
                return;
            if (document.Camera is { } camera)
            {
                Canvas.Viewport.SetZoomAt(camera.Zoom, default);
                var center = Canvas.Viewport.WorldToView(new SkiaSharp.SKPoint(camera.CenterX, camera.CenterY));
                Canvas.Viewport.Pan(Canvas.Viewport.Width / 2 - center.X, Canvas.Viewport.Height / 2 - center.Y);
            }
            else
                Canvas.FitDocument();
        });
    }

    private void Controller_Changed(object? sender, EventArgs e)
    {
        if (_refreshPending || _disposed)
            return;
        _refreshPending = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _refreshPending = false;
            if (!_disposed)
                RefreshWorkspace();
        });
    }

    private void RefreshWorkspace()
    {
        if (_refreshing || Controller is not { } controller)
            return;
        _refreshing = true;
        try
        {
            foreach (var tab in DocumentTabs.TabItems.OfType<TabViewItem>())
                if (tab.Tag is EditorDocument document)
                    tab.Header = document.Title;
            WorkspaceTitleChanged?.Invoke(this, $"{_current!.Title} — UnoDraw");
            if (_outlineRevision != controller.DocumentRevision)
            {
                _outlineRevision = controller.DocumentRevision;
                RefreshOutline();
                RefreshResources();
            }
            if (controller.SelectedElements.Count > 0)
                _inspectedElement = controller.SelectedElements[0];
            else if (_inspectedElement is SvgVisualElement || _inspectedElement?.OwnerDocument != controller.Document)
                _inspectedElement = controller.Document;
            RefreshInspector();
            foreach (var pair in _toolButtons)
                pair.Value.IsChecked = pair.Key == Canvas.Tool && !Canvas.PanMode;
            var count = controller.SelectedElements.Count;
            StatusText.Text = count > 0
                ? $"{count} object{(count == 1 ? string.Empty : "s")} selected · Shift-click to extend · Esc to clear"
                : $"{ToolLabel(Canvas.Tool)} · Space to pan · Wheel to zoom · Ctrl+K for commands";
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void BuildTools()
    {
        var tools = new (SvgEditorToolKind Tool, string Glyph, string Shortcut)[]
        {
            (SvgEditorToolKind.Select, "\uE8B0", "V"),
            (SvgEditorToolKind.PathSelect, "\uE809", "A"),
            (SvgEditorToolKind.Rect, "\uE003", "R"),
            (SvgEditorToolKind.Ellipse, "\uEA3A", "E"),
            (SvgEditorToolKind.Line, "\uE738", "L"),
            (SvgEditorToolKind.PathCubic, "\uED63", "P"),
            (SvgEditorToolKind.Freehand, "\uE76D", "B"),
            (SvgEditorToolKind.Polygon, "\uE7C1", "G"),
            (SvgEditorToolKind.Text, "\uE8D2", "T"),
            (SvgEditorToolKind.Image, "\uE91B", "")
        };
        foreach (var item in tools)
        {
            var button = new ToggleButton
            {
                Style = (Style)Resources["RailButton"],
                Content = item.Tool == SvgEditorToolKind.Rect
                    ? Resources["RectangleToolIcon"]
                    : new FontIcon { Glyph = item.Glyph, FontSize = 18 },
                Tag = item.Tool
            };
            var label = ToolLabel(item.Tool) + (item.Shortcut.Length > 0 ? $" ({item.Shortcut})" : string.Empty);
            ToolTipService.SetToolTip(button, label);
            AutomationProperties.SetName(button, label);
            button.Click += async (_, _) =>
            {
                if (item.Tool == SvgEditorToolKind.Image)
                    await RunCommandAsync("image");
                else
                    SetTool(item.Tool);
            };
            ToolRail.Children.Add(button);
            _toolButtons.Add(item.Tool, button);
        }
        ToolRail.Children.Add(new Border { Height = 1, Margin = new Thickness(6, 6, 6, 6), Background = (Brush)Resources["PanelBorder"] });
        var pan = new ToggleButton
        {
            Style = (Style)Resources["RailButton"],
            Content = new FontIcon { Glyph = "\uE7C9", FontSize = 18 }
        };
        ToolTipService.SetToolTip(pan, "Hand (H / hold Space)");
        AutomationProperties.SetName(pan, "Hand tool");
        pan.Click += (_, _) =>
        {
            Canvas.CancelGesture();
            Canvas.PanMode = pan.IsChecked == true;
            RefreshWorkspace();
        };
        ToolRail.Children.Add(pan);
    }

    private static string ToolLabel(SvgEditorToolKind tool) => tool switch
    {
        SvgEditorToolKind.Rect => "Rectangle",
        SvgEditorToolKind.PathSelect => "Edit nodes",
        SvgEditorToolKind.PathCubic => "Bézier pen",
        SvgEditorToolKind.PathQuadratic => "Quadratic curve",
        SvgEditorToolKind.PathLine => "Straight pen",
        SvgEditorToolKind.PathArc => "Arc",
        SvgEditorToolKind.Freehand => "Pencil",
        _ => tool.ToString()
    };

    private void SetTool(SvgEditorToolKind tool)
    {
        Canvas.CancelGesture();
        Canvas.PanMode = false;
        Canvas.Tool = tool;
        if (Controller is { } controller)
            controller.Session.CurrentTool = tool;
        RefreshWorkspace();
    }

    private void UpdateResponsiveLayout()
    {
        if (EditorBody.ColumnDefinitions.Count != 4)
            return;
        var showOutline = !_zenMode && ActualWidth >= 1100;
        var showInspector = !_zenMode && ActualWidth >= 760;
        OutlinePanel.Visibility = showOutline ? Visibility.Visible : Visibility.Collapsed;
        InspectorPanel.Visibility = showInspector ? Visibility.Visible : Visibility.Collapsed;
        EditorBody.ColumnDefinitions[1].Width = new GridLength(showOutline ? 228 : 0);
        EditorBody.ColumnDefinitions[3].Width = new GridLength(showInspector ? 292 : 0);
    }

    private void SnapToggle_Click(object sender, RoutedEventArgs e)
    {
        if (Controller is not { } controller)
            return;
        controller.Session.Settings.SnapToGrid = SnapToggle.IsChecked == true;
        controller.Selection.SnapToGrid = SnapToggle.IsChecked == true;
    }

    private void DocumentTabs_AddTabButtonClick(TabView sender, object args) => ApplySafely(() => NewDocument());

    private void DocumentTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DocumentTabs.SelectedItem is TabViewItem { Tag: EditorDocument document })
        {
            try
            {
                ActivateDocument(document);
            }
            catch (Exception exception)
            {
                DocumentTabs.SelectedItem = DocumentTabs.TabItems.OfType<TabViewItem>()
                    .FirstOrDefault(tab => ReferenceEquals(tab.Tag, _current));
                _ = ShowErrorAsync(exception.Message);
            }
        }
    }

    private async void DocumentTabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (_closing || _dialogVisible || args.Tab.Tag is not EditorDocument document)
            return;
        _closing = true;
        try
        {
            CommitInspectorEdits();
            WorkspaceRoot.IsHitTestVisible = false;
            Canvas.CancelGesture();
            if (!await ConfirmSaveAsync(document))
                return;
            document.Controller.Changed -= Controller_Changed;
            Documents.Remove(document);
            DocumentTabs.TabItems.Remove(args.Tab);
            DeleteRecovery(document);
            if (ReferenceEquals(_current, document))
                _current = null;
            document.Dispose();
            if (Documents.Count == 0)
                NewDocument();
            else if (DocumentTabs.SelectedItem is TabViewItem { Tag: EditorDocument selected })
                ActivateDocument(selected);
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(exception.Message);
        }
        finally
        {
            WorkspaceRoot.IsHitTestVisible = true;
            _closing = false;
        }
    }

    public new void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _recoveryTimer.Stop();
        _animationTimer.Stop();
        Canvas.CancelGesture();
        Canvas.Controller = null;
        foreach (var document in Documents)
        {
            document.Controller.Changed -= Controller_Changed;
            document.Dispose();
        }
        Documents.Clear();
        Canvas.Dispose();
        base.Dispose();
    }
}
