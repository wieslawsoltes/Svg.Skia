using System.ComponentModel;
using Microsoft.UI.Xaml.Input;

namespace Svg.Editor.Skia.Uno.Controls;

public sealed partial class SvgEditorShell : UserControl
{
    private const double DefaultUtilityRailWidth = 54.0;
    private const double DefaultSidebarWidth = 314.0;
    private const double DefaultInspectorWidth = 328.0;
    private const double LeftPanelInnerSpacing = 10.0;
    private const double SplitterTrackWidth = 10.0;
    private const double MinimumLeftPanelWidth = 300.0;
    private const double MinimumInspectorWidth = 284.0;

    private double _expandedLeftPanelWidth = DefaultUtilityRailWidth + LeftPanelInnerSpacing + DefaultSidebarWidth;
    private double _inspectorWidth = DefaultInspectorWidth;
    private INotifyPropertyChanged? _trackedViewModel;
    private bool _isDraggingLeftSplitter;
    private bool _isDraggingRightSplitter;
    private double _splitterDragOriginX;
    private double _splitterDragOriginWidth;

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(ISvgEditorShellViewModel),
            typeof(SvgEditorShell),
            new PropertyMetadata(null, OnViewModelChanged));

    public static readonly DependencyProperty StageContentProperty =
        DependencyProperty.Register(
            nameof(StageContent),
            typeof(UIElement),
            typeof(SvgEditorShell),
            new PropertyMetadata(null));

    public SvgEditorShell()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public ISvgEditorShellViewModel? ViewModel
    {
        get => (ISvgEditorShellViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public UIElement? StageContent
    {
        get => (UIElement?)GetValue(StageContentProperty);
        set => SetValue(StageContentProperty, value);
    }

    public event RoutedEventHandler? ResetRequested;

    public event RoutedEventHandler? FitRequested;

    public event RoutedEventHandler? ShareRequested;

    public event RoutedEventHandler? MainMenuRequested;

    public event RoutedEventHandler? LeftPanelToggleRequested;

    public event EventHandler<EditorMainMenuCommandEventArgs>? ZoomMenuCommandRequested;

    public event EventHandler<ZoomPercentRequestedEventArgs>? ZoomPercentRequested;

    public event RoutedEventHandler? LayersTabRequested;

    public event RoutedEventHandler? AssetsTabRequested;

    public event RoutedEventHandler? ManageLibrariesRequested;

    public event RoutedEventHandler? PageAddRequested;

    public event EventHandler<PageRequestedEventArgs>? PageSelectionRequested;

    public event EventHandler<ComponentRequestedEventArgs>? ComponentAssetRequested;

    public event ItemClickEventHandler? OutlineItemInvoked;

    public event EventHandler<OutlineSelectionRequestedEventArgs>? OutlineSelectionRequested;

    public event EventHandler<OutlineContextRequestedEventArgs>? OutlineContextRequested;

    public event EventHandler<OutlineNodeRequestEventArgs>? OutlineNodeExpansionRequested;

    public event TextChangedEventHandler? OutlineFilterChanged;

    public event RoutedEventHandler? ObjectVisibilityChanged;

    public event RoutedEventHandler? ObjectLockChanged;

    public event RoutedEventHandler? ViewportToggleChanged;

    public event RoutedEventHandler? ZoomOutRequested;

    public event RoutedEventHandler? ZoomInRequested;

    public event RoutedEventHandler? CreateFrameRequested;

    public event RoutedEventHandler? DuplicateFrameRequested;

    public event RoutedEventHandler? ToolButtonRequested;

    public event RoutedEventHandler? ActionsRequested;

    public event RoutedEventHandler? DevModeRequested;

    public event RoutedEventHandler? AlignSelectionRequested;

    public event RoutedEventHandler? DistributeSelectionRequested;

    public event RoutedEventHandler? DesignInspectorRequested;

    public event RoutedEventHandler? PrototypeInspectorRequested;

    public event RoutedEventHandler? DevInspectorRequested;

    public event RoutedEventHandler? CreateComponentRequested;

    public event RoutedEventHandler? InsertComponentRequested;

    public event RoutedEventHandler? SwapComponentRequested;

    public event RoutedEventHandler? DetachComponentRequested;

    public event RoutedEventHandler? QuickFieldCommitted;

    public event KeyEventHandler? QuickFieldKeyDown;

    public event EventHandler<EditorStyleRequestedEventArgs>? StyleRequested;

    public event EventHandler<PaintStyleRequestedEventArgs>? PaintStyleRequested;

    public event EventHandler<PaintStyleCreateRequestedEventArgs>? PaintStyleCreateRequested;

    public event TextChangedEventHandler? PropertyFilterChanged;

    public event RoutedEventHandler? PropertyValueCommitted;

    public event KeyEventHandler? PropertyValueKeyDown;

    public event EventHandler<CommentThreadRequestedEventArgs>? CommentThreadRequested;

    public event EventHandler<CommentTextRequestedEventArgs>? CommentReplyRequested;

    public event EventHandler<CommentThreadRequestedEventArgs>? CommentResolveRequested;

    public event EventHandler<CommentThreadRequestedEventArgs>? CommentDeleteRequested;

    public event EventHandler<CommentTextRequestedEventArgs>? CommentComposerSubmitted;

    public event EventHandler? CommentComposerCanceled;

    public event EventHandler<DevCodeSnippetRequestedEventArgs>? DevSnippetRequested;

    public event EventHandler? CopyActiveDevSnippetRequested;

    public event EventHandler<EditorMainMenuCommandEventArgs>? InspectorCommandRequested;

    public event EventHandler<EditorMainMenuCommandEventArgs>? DevCommandRequested;

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SvgEditorShell)d;
        control.Unsubscribe(e.OldValue as INotifyPropertyChanged);
        control.Subscribe(e.NewValue as INotifyPropertyChanged);
        control.InitializeStoredWidths(e.NewValue as ISvgEditorShellViewModel);
        control.ApplyPanelLayout();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyPanelLayout();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Unsubscribe(_trackedViewModel);
    }

    private void Subscribe(INotifyPropertyChanged? viewModel)
    {
        if (viewModel is null)
        {
            return;
        }

        _trackedViewModel = viewModel;
        _trackedViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void Unsubscribe(INotifyPropertyChanged? viewModel)
    {
        if (viewModel is null)
        {
            return;
        }

        viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        if (ReferenceEquals(_trackedViewModel, viewModel))
        {
            _trackedViewModel = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) ||
            e.PropertyName == nameof(ISvgEditorShellViewModel.IsLeftPanelCollapsed) ||
            e.PropertyName == nameof(ISvgEditorShellViewModel.UtilityRailColumnWidth) ||
            e.PropertyName == nameof(ISvgEditorShellViewModel.SidebarColumnWidth) ||
            e.PropertyName == nameof(ISvgEditorShellViewModel.LeftPanelVisibility))
        {
            ApplyPanelLayout();
        }
    }

    private void InitializeStoredWidths(ISvgEditorShellViewModel? viewModel)
    {
        if (viewModel is null)
        {
            return;
        }

        var leftWidth = viewModel.UtilityRailColumnWidth.Value + viewModel.SidebarColumnWidth.Value;
        if (leftWidth > 0.0)
        {
            _expandedLeftPanelWidth = Math.Max(MinimumLeftPanelWidth, leftWidth + LeftPanelInnerSpacing);
        }
    }

    private void ApplyPanelLayout()
    {
        if (LeftPanelColumn is null || LeftPanelSplitterColumn is null || InspectorColumn is null || RightPanelSplitterColumn is null)
        {
            return;
        }

        var isLeftPanelCollapsed = ViewModel?.IsLeftPanelCollapsed ?? false;

        LeftPanelColumn.Width = isLeftPanelCollapsed
            ? new GridLength(0.0)
            : new GridLength(Math.Max(MinimumLeftPanelWidth, _expandedLeftPanelWidth));

        LeftPanelSplitterColumn.Width = isLeftPanelCollapsed
            ? new GridLength(0.0)
            : new GridLength(SplitterTrackWidth);

        RightPanelSplitterColumn.Width = new GridLength(SplitterTrackWidth);
        InspectorColumn.Width = new GridLength(Math.Max(MinimumInspectorWidth, _inspectorWidth));
    }

    private void OnLeftPanelHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ViewModel?.IsLeftPanelCollapsed == true)
        {
            return;
        }

        if (e.NewSize.Width >= MinimumLeftPanelWidth)
        {
            _expandedLeftPanelWidth = e.NewSize.Width;
        }
    }

    private void OnInspectorPanelHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width >= MinimumInspectorWidth)
        {
            _inspectorWidth = e.NewSize.Width;
        }
    }

    private void OnLeftSplitterPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel?.IsLeftPanelCollapsed == true || sender is not UIElement element)
        {
            return;
        }

        _isDraggingLeftSplitter = true;
        _splitterDragOriginX = e.GetCurrentPoint(WorkspaceGrid).Position.X;
        _splitterDragOriginWidth = LeftPanelColumn.Width.Value;
        element.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnLeftSplitterPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingLeftSplitter)
        {
            return;
        }

        var delta = e.GetCurrentPoint(WorkspaceGrid).Position.X - _splitterDragOriginX;
        _expandedLeftPanelWidth = Math.Max(MinimumLeftPanelWidth, _splitterDragOriginWidth + delta);
        LeftPanelColumn.Width = new GridLength(_expandedLeftPanelWidth);
        e.Handled = true;
    }

    private void OnLeftSplitterPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        EndLeftSplitterDrag(sender as UIElement, e);
    }

    private void OnLeftSplitterPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        EndLeftSplitterDrag(sender as UIElement, e);
    }

    private void OnLeftSplitterPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        EndLeftSplitterDrag(sender as UIElement, e);
    }

    private void EndLeftSplitterDrag(UIElement? element, PointerRoutedEventArgs e)
    {
        if (!_isDraggingLeftSplitter)
        {
            return;
        }

        _isDraggingLeftSplitter = false;
        element?.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void OnRightSplitterPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not UIElement element)
        {
            return;
        }

        _isDraggingRightSplitter = true;
        _splitterDragOriginX = e.GetCurrentPoint(WorkspaceGrid).Position.X;
        _splitterDragOriginWidth = InspectorColumn.Width.Value;
        element.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnRightSplitterPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingRightSplitter)
        {
            return;
        }

        var delta = e.GetCurrentPoint(WorkspaceGrid).Position.X - _splitterDragOriginX;
        _inspectorWidth = Math.Max(MinimumInspectorWidth, _splitterDragOriginWidth - delta);
        InspectorColumn.Width = new GridLength(_inspectorWidth);
        e.Handled = true;
    }

    private void OnRightSplitterPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        EndRightSplitterDrag(sender as UIElement, e);
    }

    private void OnRightSplitterPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        EndRightSplitterDrag(sender as UIElement, e);
    }

    private void OnRightSplitterPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        EndRightSplitterDrag(sender as UIElement, e);
    }

    private void EndRightSplitterDrag(UIElement? element, PointerRoutedEventArgs e)
    {
        if (!_isDraggingRightSplitter)
        {
            return;
        }

        _isDraggingRightSplitter = false;
        element?.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void OnResetRequested(object sender, RoutedEventArgs e)
    {
        ResetRequested?.Invoke(sender, e);
    }

    private void OnMainMenuRequested(object sender, RoutedEventArgs e)
    {
        MainMenuRequested?.Invoke(sender, e);
    }

    private void OnLeftPanelToggleRequested(object sender, RoutedEventArgs e)
    {
        LeftPanelToggleRequested?.Invoke(sender, e);
    }

    private void OnFitRequested(object sender, RoutedEventArgs e)
    {
        FitRequested?.Invoke(sender, e);
    }

    private void OnShareRequested(object sender, RoutedEventArgs e)
    {
        ShareRequested?.Invoke(sender, e);
    }

    private void OnZoomMenuCommandRequested(object sender, EditorMainMenuCommandEventArgs e)
    {
        ZoomMenuCommandRequested?.Invoke(sender, e);
    }

    private void OnZoomPercentRequested(object sender, ZoomPercentRequestedEventArgs e)
    {
        ZoomPercentRequested?.Invoke(sender, e);
    }

    private void OnLayersTabRequested(object sender, RoutedEventArgs e)
    {
        LayersTabRequested?.Invoke(sender, e);
    }

    private void OnAssetsTabRequested(object sender, RoutedEventArgs e)
    {
        AssetsTabRequested?.Invoke(sender, e);
    }

    private void OnManageLibrariesRequested(object sender, RoutedEventArgs e)
    {
        ManageLibrariesRequested?.Invoke(sender, e);
    }

    private void OnPageAddRequested(object sender, RoutedEventArgs e)
    {
        PageAddRequested?.Invoke(sender, e);
    }

    private void OnPageSelectionRequested(object sender, PageRequestedEventArgs e)
    {
        PageSelectionRequested?.Invoke(sender, e);
    }

    private void OnComponentAssetRequested(object sender, ComponentRequestedEventArgs e)
    {
        ComponentAssetRequested?.Invoke(sender, e);
    }

    private void OnOutlineItemInvoked(object sender, ItemClickEventArgs e)
    {
        OutlineItemInvoked?.Invoke(sender, e);
    }

    private void OnOutlineSelectionRequested(object sender, OutlineSelectionRequestedEventArgs e)
    {
        OutlineSelectionRequested?.Invoke(sender, e);
    }

    private void OnOutlineContextRequested(object sender, OutlineContextRequestedEventArgs e)
    {
        OutlineContextRequested?.Invoke(sender, e);
    }

    private void OnOutlineNodeExpansionRequested(object sender, OutlineNodeRequestEventArgs e)
    {
        OutlineNodeExpansionRequested?.Invoke(sender, e);
    }

    private void OnOutlineFilterChanged(object sender, TextChangedEventArgs e)
    {
        OutlineFilterChanged?.Invoke(sender, e);
    }

    private void OnObjectVisibilityChanged(object sender, RoutedEventArgs e)
    {
        ObjectVisibilityChanged?.Invoke(sender, e);
    }

    private void OnObjectLockChanged(object sender, RoutedEventArgs e)
    {
        ObjectLockChanged?.Invoke(sender, e);
    }

    private void OnViewportToggleChanged(object sender, RoutedEventArgs e)
    {
        ViewportToggleChanged?.Invoke(sender, e);
    }

    private void OnZoomOutRequested(object sender, RoutedEventArgs e)
    {
        ZoomOutRequested?.Invoke(sender, e);
    }

    private void OnZoomInRequested(object sender, RoutedEventArgs e)
    {
        ZoomInRequested?.Invoke(sender, e);
    }

    private void OnCreateFrameRequested(object sender, RoutedEventArgs e)
    {
        CreateFrameRequested?.Invoke(sender, e);
    }

    private void OnDuplicateFrameRequested(object sender, RoutedEventArgs e)
    {
        DuplicateFrameRequested?.Invoke(sender, e);
    }

    private void OnToolButtonRequested(object sender, RoutedEventArgs e)
    {
        ToolButtonRequested?.Invoke(sender, e);
    }

    private void OnActionsRequested(object sender, RoutedEventArgs e)
    {
        ActionsRequested?.Invoke(sender, e);
    }

    private void OnDevModeRequested(object sender, RoutedEventArgs e)
    {
        DevModeRequested?.Invoke(sender, e);
    }

    private void OnAlignSelectionRequested(object sender, RoutedEventArgs e)
    {
        AlignSelectionRequested?.Invoke(sender, e);
    }

    private void OnDistributeSelectionRequested(object sender, RoutedEventArgs e)
    {
        DistributeSelectionRequested?.Invoke(sender, e);
    }

    private void OnDesignInspectorRequested(object sender, RoutedEventArgs e)
    {
        DesignInspectorRequested?.Invoke(sender, e);
    }

    private void OnPrototypeInspectorRequested(object sender, RoutedEventArgs e)
    {
        PrototypeInspectorRequested?.Invoke(sender, e);
    }

    private void OnDevInspectorRequested(object sender, RoutedEventArgs e)
    {
        DevInspectorRequested?.Invoke(sender, e);
    }

    private void OnCreateComponentRequested(object sender, RoutedEventArgs e)
    {
        CreateComponentRequested?.Invoke(sender, e);
    }

    private void OnInsertComponentRequested(object sender, RoutedEventArgs e)
    {
        InsertComponentRequested?.Invoke(sender, e);
    }

    private void OnSwapComponentRequested(object sender, RoutedEventArgs e)
    {
        SwapComponentRequested?.Invoke(sender, e);
    }

    private void OnDetachComponentRequested(object sender, RoutedEventArgs e)
    {
        DetachComponentRequested?.Invoke(sender, e);
    }

    private void OnQuickFieldCommitted(object sender, RoutedEventArgs e)
    {
        QuickFieldCommitted?.Invoke(sender, e);
    }

    private void OnQuickFieldKeyDown(object sender, KeyRoutedEventArgs e)
    {
        QuickFieldKeyDown?.Invoke(sender, e);
    }

    private void OnPaintStyleRequested(object sender, PaintStyleRequestedEventArgs e)
    {
        PaintStyleRequested?.Invoke(sender, e);
    }

    private void OnPaintStyleCreateRequested(object sender, PaintStyleCreateRequestedEventArgs e)
    {
        PaintStyleCreateRequested?.Invoke(sender, e);
    }

    private void OnStyleRequested(object sender, EditorStyleRequestedEventArgs e)
    {
        StyleRequested?.Invoke(sender, e);
    }

    private void OnPropertyFilterChanged(object sender, TextChangedEventArgs e)
    {
        PropertyFilterChanged?.Invoke(sender, e);
    }

    private void OnPropertyValueCommitted(object sender, RoutedEventArgs e)
    {
        PropertyValueCommitted?.Invoke(sender, e);
    }

    private void OnPropertyValueKeyDown(object sender, KeyRoutedEventArgs e)
    {
        PropertyValueKeyDown?.Invoke(sender, e);
    }

    private void OnCommentThreadRequested(object sender, CommentThreadRequestedEventArgs e)
    {
        CommentThreadRequested?.Invoke(sender, e);
    }

    private void OnCommentReplyRequested(object sender, CommentTextRequestedEventArgs e)
    {
        CommentReplyRequested?.Invoke(sender, e);
    }

    private void OnCommentResolveRequested(object sender, CommentThreadRequestedEventArgs e)
    {
        CommentResolveRequested?.Invoke(sender, e);
    }

    private void OnCommentDeleteRequested(object sender, CommentThreadRequestedEventArgs e)
    {
        CommentDeleteRequested?.Invoke(sender, e);
    }

    private void OnDevSnippetRequested(object sender, DevCodeSnippetRequestedEventArgs e)
    {
        DevSnippetRequested?.Invoke(sender, e);
    }

    private void OnCopyActiveDevSnippetRequested(object sender, EventArgs e)
    {
        CopyActiveDevSnippetRequested?.Invoke(sender, e);
    }

    private void OnInspectorCommandRequested(object sender, EditorMainMenuCommandEventArgs e)
    {
        InspectorCommandRequested?.Invoke(sender, e);
    }

    private void OnDevCommandRequested(object sender, EditorMainMenuCommandEventArgs e)
    {
        DevCommandRequested?.Invoke(sender, e);
    }

    private void OnCommentComposerSubmitted(object sender, CommentTextRequestedEventArgs e)
    {
        CommentComposerSubmitted?.Invoke(sender, e);
    }

    private void OnCommentComposerCanceled(object sender, EventArgs e)
    {
        CommentComposerCanceled?.Invoke(sender, e);
    }
}
