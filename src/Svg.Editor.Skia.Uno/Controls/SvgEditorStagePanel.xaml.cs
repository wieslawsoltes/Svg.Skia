namespace Svg.Editor.Skia.Uno.Controls;

public sealed partial class SvgEditorStagePanel : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(ISvgEditorShellViewModel),
            typeof(SvgEditorStagePanel),
            new PropertyMetadata(null));

    public static readonly DependencyProperty StageContentProperty =
        DependencyProperty.Register(
            nameof(StageContent),
            typeof(UIElement),
            typeof(SvgEditorStagePanel),
            new PropertyMetadata(null));

    public SvgEditorStagePanel()
    {
        InitializeComponent();
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

    public event EventHandler<CommentThreadRequestedEventArgs>? CommentThreadRequested;

    public event EventHandler<CommentTextRequestedEventArgs>? CommentComposerSubmitted;

    public event EventHandler? CommentComposerCanceled;

    private void OnViewportToggleChanged(object sender, RoutedEventArgs e)
    {
        ViewportToggleChanged?.Invoke(sender, e);
    }

    private void OnZoomOutClick(object sender, RoutedEventArgs e)
    {
        ZoomOutRequested?.Invoke(sender, e);
    }

    private void OnZoomInClick(object sender, RoutedEventArgs e)
    {
        ZoomInRequested?.Invoke(sender, e);
    }

    private void OnCreateFrameClick(object sender, RoutedEventArgs e)
    {
        CreateFrameRequested?.Invoke(sender, e);
    }

    private void OnDuplicateFrameClick(object sender, RoutedEventArgs e)
    {
        DuplicateFrameRequested?.Invoke(sender, e);
    }

    private void OnToolRequested(object sender, RoutedEventArgs e)
    {
        ToolButtonRequested?.Invoke(sender, e);
    }

    private void OnActionsClick(object sender, RoutedEventArgs e)
    {
        ActionsRequested?.Invoke(sender, e);
    }

    private void OnDevModeClick(object sender, RoutedEventArgs e)
    {
        DevModeRequested?.Invoke(sender, e);
    }

    private void OnAlignSelectionClick(object sender, RoutedEventArgs e)
    {
        AlignSelectionRequested?.Invoke(sender, e);
    }

    private void OnDistributeSelectionClick(object sender, RoutedEventArgs e)
    {
        DistributeSelectionRequested?.Invoke(sender, e);
    }

    private void OnCommentThreadRequested(object sender, CommentThreadRequestedEventArgs e)
    {
        CommentThreadRequested?.Invoke(sender, e);
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
