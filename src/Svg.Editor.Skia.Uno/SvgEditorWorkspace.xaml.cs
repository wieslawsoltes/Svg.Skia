using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Svg.Editor.Core;
using Svg.Editor.Skia;
using Svg.Editor.Svg;

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
    private bool _positionUpdatePending;
    private double _reportedZoom = double.NaN;

    public SvgEditorWorkspace()
    {
        InitializeComponent();
        InitializeStudioWorkspace();
        BuildCommands();
        BuildEditorCommands();
        ApplyEditorShortcutMetadata();
        BuildMenus();
        BuildTools();
        InitializeHierarchy();
        Canvas.InteractionStarting += (_, _) => { if (!_dialogVisible) { CommitInspectorEdits(); Focus(FocusState.Pointer); } };
        Canvas.ViewportChanged += (_, _) => UpdateZoomLabel();
        Canvas.PointerPositionChanged += (_, _) => QueuePointerPositionLabel();
        Canvas.InteractionFailed += async (_, args) => await ShowErrorAsync(args.Exception.Message);
        ActualThemeChanged += (_, _) => { Canvas.IsDarkTheme = ActualTheme == ElementTheme.Dark; SaveEditorPreferences(); };
        SizeChanged += (_, _) => UpdateResponsiveLayout();
        _recoveryTimer.Tick += async (_, _) => await SaveRecoveryAsync();
        _animationTimer.Tick += (_, _) =>
        {
            _animationTimer.Stop();
            if (_disposed || !_animationClock.IsRunning || Controller is not { } controller)
                return;
            controller.Renderer.SetAnimationTime(_animationPlaybackOrigin + _animationClock.Elapsed);
            Canvas.Invalidate();
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                if (!_disposed && _animationClock.IsRunning && ReferenceEquals(Controller, controller))
                    _animationTimer.Start();
            });
        };
        Loaded += (_, _) =>
        {
            Canvas.IsDarkTheme = ActualTheme == ElementTheme.Dark;
            _recoveryTimer.Start();
            UpdateResponsiveLayout();
            Focus(FocusState.Programmatic);
        };
        Unloaded += (_, _) =>
        {
            SaveStudioLayout();
            _recoveryTimer.Stop();
            _animationTimer.Stop();
            _animationClock.Stop();
            Canvas.CancelGesture();
        };
    }

    public ObservableCollection<EditorDocument> Documents { get; } = new();
    public EditorDocument? CurrentDocument => _current;
    public SvgEditorController? Controller => _current?.Controller;
    public SvgEditorCanvas Surface => Canvas;
    public Window? HostWindow { get; set; }
    public event EventHandler<string>? WorkspaceTitleChanged;

    private void UpdateZoomLabel()
    {
        var zoom = Canvas.Zoom;
        if (_disposed || zoom == _reportedZoom)
            return;
        _reportedZoom = zoom;
        var text = $"{zoom * 100:0.#}%";
        if (!Equals(ZoomLabel.Content, text))
            ZoomLabel.Content = text;
    }

    private void QueuePointerPositionLabel()
    {
        if (_disposed || _positionUpdatePending)
            return;
        _positionUpdatePending = true;
        if (!DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            _positionUpdatePending = false;
            if (_disposed)
                return;
            // A burst of pointer events needs one formatted label with the most
            // recent coordinates, without queuing redundant text/layout work.
            var position = Canvas.PointerPosition;
            var text = $"X {position.X:0.0}  Y {position.Y:0.0}";
            if (PositionText.Text != text)
                PositionText.Text = text;
        }))
            _positionUpdatePending = false;
    }

    public void NewDocument(float width = 1200, float height = 800)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SvgEditorWorkspace));
        if (_closing)
            return;
        CommitInspectorEdits();
        var document = new EditorDocument { Name = $"Untitled {Documents.Count + 1}.svg" };
        try
        { document.Controller.NewDocument(width, height); AddDocument(document); }
        catch { document.Dispose(); throw; }
        StatusText.Text = "New artboard. Choose a tool to start drawing.";
    }
    private void AddDocument(EditorDocument document)
    {
        // An asynchronous load may finish after its host has closed. Ownership
        // transfers only while the workspace can retain the document and its canvas.
        if (_disposed || _closing)
        { document.Dispose(); return; }
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
        _animationPlaybackOrigin = TimeSpan.Zero;
        _current = document;
        _inspectedElement = null;
        _outlineRevision = -1;
        Canvas.Controller = document.Controller;
        Canvas.Tool = document.Controller.Session.CurrentTool;
        Canvas.InteractionMode = SvgEditorInteractionMode.None;
        Canvas.ActiveBrush = null;
        Canvas.ShowGrid = document.Controller.Session.Settings.ShowGrid;
        SnapToggle.IsChecked = document.Controller.Session.Settings.SnapToGrid;
        RefreshWorkspace();
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_disposed || _closing || !ReferenceEquals(document, _current))
                return;
            if (document.Camera is { } camera)
            {
                Canvas.Viewport.SetZoomAt(camera.Zoom, default);
                var center = Canvas.Viewport.WorldToView(new SkiaSharp.SKPoint(camera.CenterX, camera.CenterY));
                Canvas.Viewport.Pan(Canvas.Viewport.Width / 2 - center.X, Canvas.Viewport.Height / 2 - center.Y);
            }
            else
                Canvas.FitDocument();
            UpdateStudioRulers();
        });
    }
    private void Controller_Changed(object? sender, EventArgs args)
    {
        if (_refreshPending || _disposed)
            return;
        _refreshPending = true;
        DispatcherQueue.TryEnqueue(() => { _refreshPending = false; if (!_disposed) RefreshWorkspace(); });
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
            SynchronizeStudioLayerSelection(controller);
            RefreshInspector();
            RefreshStudioPanels();
            RefreshEditorPanels();
            foreach (var pair in _toolButtons)
            {
                var active = pair.Key == Canvas.Tool
                    || pair.Key == SvgEditorToolKind.PathCubic && Canvas.Tool is SvgEditorToolKind.PathQuadratic or SvgEditorToolKind.PathLine or SvgEditorToolKind.PathArc
                    || pair.Key == SvgEditorToolKind.Ellipse && Canvas.Tool == SvgEditorToolKind.Circle
                    || pair.Key == SvgEditorToolKind.Polygon && Canvas.Tool == SvgEditorToolKind.Polyline;
                pair.Value.IsChecked = active && !Canvas.PanMode;
            }
            if (_studioHand is not null)
                _studioHand.IsChecked = Canvas.PanMode;
            RefreshEditorToolState();
            RefreshEditorMenus();
            var count = controller.SelectedElements.Count;
            StatusText.Text = (count > 0 ? $"{count} object{(count == 1 ? string.Empty : "s")} · " : string.Empty)
                + ToolContextLabel() + " · " + EditorToolHint();
        }
        finally { _refreshing = false; }
    }

    private void SynchronizeStudioLayerSelection(SvgEditorController controller)
    {
        if (_outlineDragging || _rebuildingOutline)
            return;
        var selected = new HashSet<SvgVisualElement>(controller.SelectedElements);
        var wanted = EnumerateOutlineNodes(DocumentOutline.RootNodes).Where(node =>
            node.Content is ElementItem item && (item.Element is SvgVisualElement visual
                ? selected.Contains(visual) : ReferenceEquals(item.Element, _inspectedElement))).ToArray();
        var current = DocumentOutline.SelectedNodes.ToArray();
        if (current.Length == wanted.Length && current.All(wanted.Contains))
            return;
        _rebuildingOutline = true;
        try
        {
            // Selection-only changes do not increment DocumentRevision. Update only
            // selected nodes, preserving expansion, scroll and retained row controls.
            foreach (var node in current)
                if (!wanted.Contains(node))
                    DocumentOutline.SelectedNodes.Remove(node);
            foreach (var node in wanted)
                if (!DocumentOutline.SelectedNodes.Contains(node))
                    DocumentOutline.SelectedNodes.Add(node);
        }
        finally { _rebuildingOutline = false; }
    }

    private void BuildTools() => BuildStudioTools();
    private static string ToolLabel(SvgEditorToolKind tool) => tool switch
    {
        SvgEditorToolKind.Select => "Selection",
        SvgEditorToolKind.Rect => "Rectangle",
        SvgEditorToolKind.PathSelect => "Direct selection",
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
        Canvas.InteractionMode = SvgEditorInteractionMode.None;
        if (Controller is { } controller)
            controller.Session.CurrentTool = tool;
        RefreshWorkspace();
    }
    private void UpdateResponsiveLayout() => ApplyStudioMetrics();
    private void SnapToggle_Click(object sender, RoutedEventArgs args)
    {
        if (Controller is not { } controller)
            return;
        controller.Session.Settings.SnapToGrid = SnapToggle.IsChecked == true;
        controller.Selection.SnapToGrid = SnapToggle.IsChecked == true;
    }
    private void DocumentTabs_AddTabButtonClick(TabView sender, object args) => ApplySafely(() => NewDocument());
    private void DocumentTabs_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (DocumentTabs.SelectedItem is not TabViewItem { Tag: EditorDocument document })
            return;
        try
        { ActivateDocument(document); }
        catch (Exception exception)
        {
            DocumentTabs.SelectedItem = DocumentTabs.TabItems.OfType<TabViewItem>().FirstOrDefault(tab => ReferenceEquals(tab.Tag, _current));
            _ = ShowErrorAsync(exception.Message);
        }
    }
    private async void DocumentTabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        => await CloseEditorDocumentAsync(args.Tab);

    private async Task CloseEditorDocumentAsync(TabViewItem tab)
    {
        if (_disposed || _closing || _dialogVisible || tab.Tag is not EditorDocument document
            || !Documents.Contains(document))
            return;
        try
        {
            _closing = true;
            try
            {
                CommitInspectorEdits();
                WorkspaceRoot.IsHitTestVisible = false;
                Canvas.CancelGesture();
                if (!await ConfirmSaveAsync(document) || _disposed || !Documents.Contains(document))
                    return;
                // Failure to remove a recovery copy must leave the live tab intact.
                DeleteRecovery(document);
                var retained = ReferenceEquals(_current, document) ? null : _current;
                document.Controller.Changed -= Controller_Changed;
                try
                {
                    Documents.Remove(document);
                    if (_disposed)
                        return;
                    DocumentTabs.TabItems.Remove(tab);
                    if (_disposed)
                        return;
                    // TabView can change selection while removing a tab. Closing an
                    // inactive tab keeps the existing document; an active close uses
                    // the native selection, or the first surviving tab as a fallback.
                    var next = retained ?? ((DocumentTabs.SelectedItem as TabViewItem)?.Tag as EditorDocument);
                    if (next is null || !Documents.Contains(next))
                        next = Documents.FirstOrDefault();
                    if (next is not null)
                    {
                        DocumentTabs.SelectedItem = DocumentTabs.TabItems.OfType<TabViewItem>()
                            .First(item => ReferenceEquals(item.Tag, next));
                        if (!_disposed)
                            ActivateDocument(next);
                    }
                }
                finally
                {
                    // Observable collection/selection callbacks may dispose the
                    // host synchronously. The removed document is still ours to
                    // release, but disposed controls must not be accessed again.
                    if (ReferenceEquals(_current, document))
                    {
                        _current = null;
                        _inspectedElement = null;
                    }
                    if (!_disposed && ReferenceEquals(Canvas.Controller, document.Controller))
                        Canvas.Controller = null;
                    document.Dispose();
                }
            }
            finally
            {
                _closing = false;
                if (!_disposed)
                    WorkspaceRoot.IsHitTestVisible = true;
            }
            // NewDocument/AddDocument reject work while the close guard is held.
            // Release it before replacing the last tab, after detaching its canvas.
            if (!_disposed && Documents.Count == 0)
                NewDocument();
        }
        catch (Exception exception) { await ShowErrorAsync(exception.Message); }
    }
    public new void Dispose()
    {
        if (_disposed)
            return;
        DisposeEditorKeyboard();
        SaveStudioLayout();
        _disposed = true;
        _recoveryTimer.Stop();
        _animationTimer.Stop();
        Canvas.CancelGesture();
        Canvas.Controller = null;
        DisposeStudioWorkspace();
        foreach (var document in Documents)
        { document.Controller.Changed -= Controller_Changed; document.Dispose(); }
        Documents.Clear();
        Canvas.Dispose();
        base.Dispose();
    }
}
