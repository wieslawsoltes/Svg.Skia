using Svg.Editor.Skia.Uno.Models;

namespace Svg.Editor.Skia.Uno.Controls;

public sealed partial class SvgEditorInspectorPanel : UserControl
{
    private bool _isStrokeExpanded;
    private bool _isEffectsExpanded;
    private bool _isSelectionColorsExpanded;
    private bool _isLayoutGuideExpanded;
    private bool _isExportExpanded;
    private bool _isAdvancedExpanded;

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(ISvgEditorShellViewModel),
            typeof(SvgEditorInspectorPanel),
            new PropertyMetadata(null));

    public SvgEditorInspectorPanel()
    {
        InitializeComponent();
        UpdateDisclosureState();
    }

    public ISvgEditorShellViewModel? ViewModel
    {
        get => (ISvgEditorShellViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public event RoutedEventHandler? DesignInspectorRequested;

    public event RoutedEventHandler? PrototypeInspectorRequested;

    public event RoutedEventHandler? DevInspectorRequested;

    public event RoutedEventHandler? CreateComponentRequested;

    public event RoutedEventHandler? InsertComponentRequested;

    public event RoutedEventHandler? SwapComponentRequested;

    public event RoutedEventHandler? DetachComponentRequested;

    public event RoutedEventHandler? QuickFieldCommitted;

    public event KeyEventHandler? QuickFieldKeyDown;

    public event TextChangedEventHandler? PropertyFilterChanged;

    public event RoutedEventHandler? PropertyValueCommitted;

    public event KeyEventHandler? PropertyValueKeyDown;

    public event RoutedEventHandler? AlignSelectionRequested;

    public event RoutedEventHandler? DistributeSelectionRequested;

    public event EventHandler<EditorMainMenuCommandEventArgs>? CommandRequested;

    public event EventHandler<CommentThreadRequestedEventArgs>? CommentThreadRequested;

    public event EventHandler<CommentTextRequestedEventArgs>? CommentReplyRequested;

    public event EventHandler<CommentThreadRequestedEventArgs>? CommentResolveRequested;

    public event EventHandler<CommentThreadRequestedEventArgs>? CommentDeleteRequested;

    public event EventHandler<DevCodeSnippetRequestedEventArgs>? DevSnippetRequested;

    public event EventHandler? CopyActiveDevSnippetRequested;

    public event EventHandler<EditorMainMenuCommandEventArgs>? DevCommandRequested;

    public event EventHandler<PaintStyleRequestedEventArgs>? PaintStyleRequested;

    public event EventHandler<PaintStyleCreateRequestedEventArgs>? PaintStyleCreateRequested;

    public event EventHandler<EditorStyleRequestedEventArgs>? StyleRequested;

    private void OnDesignInspectorClick(object sender, RoutedEventArgs e)
    {
        DesignInspectorRequested?.Invoke(sender, e);
    }

    private void OnPrototypeInspectorClick(object sender, RoutedEventArgs e)
    {
        PrototypeInspectorRequested?.Invoke(sender, e);
    }

    private void OnDevInspectorClick(object sender, RoutedEventArgs e)
    {
        DevInspectorRequested?.Invoke(sender, e);
    }

    private void OnCreateComponentClick(object sender, RoutedEventArgs e)
    {
        CreateComponentRequested?.Invoke(sender, e);
    }

    private void OnInsertComponentClick(object sender, RoutedEventArgs e)
    {
        InsertComponentRequested?.Invoke(sender, e);
    }

    private void OnSwapComponentClick(object sender, RoutedEventArgs e)
    {
        SwapComponentRequested?.Invoke(sender, e);
    }

    private void OnDetachComponentClick(object sender, RoutedEventArgs e)
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

    private void OnPropertyFilterTextChanged(object sender, TextChangedEventArgs e)
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

    private void OnAlignSelectionClick(object sender, RoutedEventArgs e)
    {
        AlignSelectionRequested?.Invoke(sender, e);
    }

    private void OnDistributeSelectionClick(object sender, RoutedEventArgs e)
    {
        DistributeSelectionRequested?.Invoke(sender, e);
    }

    private void OnInspectorCommandClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: not null } element)
        {
            return;
        }

        var parsed = element.Tag switch
        {
            EditorMainMenuCommand command => command,
            string rawValue when Enum.TryParse<EditorMainMenuCommand>(rawValue, true, out var command) => command,
            _ => (EditorMainMenuCommand?)null
        };

        if (parsed is { } commandValue)
        {
            CommandRequested?.Invoke(this, new EditorMainMenuCommandEventArgs(commandValue));
        }
    }

    private void OnVectorOperationsFlyoutOpening(object sender, object e)
    {
        var canBooleanCombine = ViewModel?.CanBooleanCombineSelection ?? false;
        var canFlatten = ViewModel?.CanFlattenSelectionToPath ?? false;

        UnionMenuItem.IsEnabled = canBooleanCombine;
        SubtractMenuItem.IsEnabled = canBooleanCombine;
        IntersectMenuItem.IsEnabled = canBooleanCombine;
        ExcludeMenuItem.IsEnabled = canBooleanCombine;
        FlattenMenuItem.IsEnabled = canFlatten;
    }

    private void OnInspectorSectionToggleClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string rawTag })
        {
            return;
        }

        switch (rawTag)
        {
            case "Stroke":
                _isStrokeExpanded = !_isStrokeExpanded;
                break;
            case "Effects":
                _isEffectsExpanded = !_isEffectsExpanded;
                break;
            case "SelectionColors":
                _isSelectionColorsExpanded = !_isSelectionColorsExpanded;
                break;
            case "LayoutGuide":
                _isLayoutGuideExpanded = !_isLayoutGuideExpanded;
                break;
            case "Export":
                _isExportExpanded = !_isExportExpanded;
                break;
            case "Advanced":
                _isAdvancedExpanded = !_isAdvancedExpanded;
                break;
            default:
                return;
        }

        UpdateDisclosureState();
    }

    private void OnAddEffectMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null
            || !ViewModel.CanEditEffects
            || sender is not FrameworkElement { Tag: string rawKind }
            || !Enum.TryParse<EditorEffectKind>(rawKind, true, out var kind))
        {
            return;
        }

        ViewModel.EffectItems.Add(EditorEffectItem.CreateDefault(kind));
        _isEffectsExpanded = true;
        UpdateDisclosureState();
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

    private void OnDevCommandRequested(object sender, EditorMainMenuCommandEventArgs e)
    {
        DevCommandRequested?.Invoke(sender, e);
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

    private void UpdateDisclosureState()
    {
        if (StrokeSectionContent is null)
        {
            return;
        }

        StrokeSectionContent.Visibility = _isStrokeExpanded ? Visibility.Visible : Visibility.Collapsed;
        EffectsSectionContent.Visibility = _isEffectsExpanded ? Visibility.Visible : Visibility.Collapsed;
        SelectionColorsSectionContent.Visibility = _isSelectionColorsExpanded ? Visibility.Visible : Visibility.Collapsed;
        LayoutGuideSectionContent.Visibility = _isLayoutGuideExpanded ? Visibility.Visible : Visibility.Collapsed;
        ExportSectionContent.Visibility = _isExportExpanded ? Visibility.Visible : Visibility.Collapsed;
        AdvancedSectionContent.Visibility = _isAdvancedExpanded ? Visibility.Visible : Visibility.Collapsed;

        StrokeToggleIcon.Kind = _isStrokeExpanded ? FigmaIconKind.Minus : FigmaIconKind.Add;
        EffectsToggleIcon.Kind = _isEffectsExpanded ? FigmaIconKind.Minus : FigmaIconKind.Add;
        SelectionColorsToggleGlyph.Text = _isSelectionColorsExpanded ? "⌄" : "›";
        LayoutGuideToggleIcon.Kind = _isLayoutGuideExpanded ? FigmaIconKind.Minus : FigmaIconKind.Add;
        ExportToggleIcon.Kind = _isExportExpanded ? FigmaIconKind.Minus : FigmaIconKind.Add;
        AdvancedToggleIcon.Kind = _isAdvancedExpanded ? FigmaIconKind.Minus : FigmaIconKind.Add;
    }
}
