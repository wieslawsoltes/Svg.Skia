using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Svg.Editor.Core;
using Svg.Editor.Skia.Uno.Models;
using Svg.Editor.Svg.Models;
using Windows.Foundation;
using Windows.UI;

namespace Svg.Editor.Skia.Uno;

public interface ISvgEditorShellViewModel : INotifyPropertyChanged
{
    string DocumentTitle { get; }

    string PageTitle { get; }

    string PageSubtitle { get; }

    string CanvasLabel { get; }

    string CurrentToolLabel { get; }

    string ViewportLabel { get; }

    string SelectionSummary { get; }

    string CanvasStatus { get; }

    string SelectedTitle { get; }

    string SelectedSubtitle { get; }

    string SelectedIconGlyph { get; }

    string QuickX { get; }

    string QuickY { get; }

    string QuickWidth { get; }

    string QuickHeight { get; }

    string QuickRotation { get; }

    string QuickOpacity { get; }

    string QuickCornerRadius { get; }

    string SelectedBlendModeId { get; set; }

    string QuickFill { get; }

    string QuickStroke { get; }

    string QuickStrokeWidth { get; }

    string ComponentPrimaryLabel { get; }

    string ComponentSecondaryLabel { get; }

    EditorComponentItem? SelectedComponentAsset { get; set; }

    string LibrariesSummary { get; }

    string CommentsSummary { get; }

    Color FillColor { get; set; }

    bool IsFillEnabled { get; set; }

    bool IsFillColorEditable { get; }

    Color StrokeColor { get; set; }

    bool IsStrokeEnabled { get; set; }

    bool IsStrokeColorEditable { get; }

    bool CanEditFrame { get; }

    bool CanUseFramePresets { get; }

    FrameContainerKind FrameKind { get; set; }

    string SelectedFramePresetId { get; set; }

    string FrameSummary { get; }

    bool CanEditEffects { get; }

    bool CanEditBlendMode { get; }

    bool CanAlignSelection { get; }

    bool CanDistributeSelection { get; }

    bool CanRotateSelection { get; }

    bool CanBooleanCombineSelection { get; }

    bool CanFlattenSelectionToPath { get; }

    bool CanShowVectorOperations { get; }

    bool CanEditCornerRadius { get; }

    bool CanEditAutoLayout { get; }

    bool IsAutoLayoutEnabled { get; set; }

    AutoLayoutFlow AutoLayoutFlow { get; set; }

    AutoLayoutSizeMode AutoLayoutWidthMode { get; set; }

    AutoLayoutSizeMode AutoLayoutHeightMode { get; set; }

    AutoLayoutAlignment AutoLayoutHorizontalAlignment { get; set; }

    AutoLayoutAlignment AutoLayoutVerticalAlignment { get; set; }

    string AutoLayoutWidth { get; set; }

    string AutoLayoutHeight { get; set; }

    string AutoLayoutGap { get; set; }

    string AutoLayoutPaddingHorizontal { get; set; }

    string AutoLayoutPaddingVertical { get; set; }

    bool IsAutoLayoutClipContent { get; set; }

    bool IsGridVisible { get; set; }

    bool IsSnapEnabled { get; set; }

    bool IsWireframeEnabled { get; set; }

    bool AreFiltersDisabled { get; set; }

    bool AreRulersVisible { get; set; }

    GridLength HorizontalRulerLength { get; }

    GridLength VerticalRulerLength { get; }

    bool CanCreateComponent { get; }

    bool CanInsertComponentInstance { get; }

    bool CanSwapComponentInstance { get; }

    bool CanDetachComponentInstance { get; }

    bool IsLayersViewActive { get; }

    bool IsAssetsViewActive { get; }

    bool IsLeftPanelCollapsed { get; }

    GridLength UtilityRailColumnWidth { get; }

    GridLength SidebarColumnWidth { get; }

    Visibility LayersPanelVisibility { get; }

    Visibility AssetsPanelVisibility { get; }

    Visibility LeftPanelVisibility { get; }

    bool IsDesignInspectorActive { get; }

    bool IsPrototypeInspectorActive { get; }

    bool IsDevInspectorActive { get; }

    bool IsCommentsInspectorActive { get; }

    Visibility DesignInspectorVisibility { get; }

    Visibility PrototypeInspectorVisibility { get; }

    Visibility DevInspectorVisibility { get; }

    Visibility CommentsInspectorVisibility { get; }

    Visibility InspectorTabsVisibility { get; }

    Visibility InspectorSelectionVisibility { get; }

    string CommentDraftText { get; }

    bool IsCommentDraftVisible { get; }

    Point CommentDraftViewPosition { get; }

    EditorCommentThread? SelectedCommentThread { get; }

    string DevSelectionPath { get; }

    string DevMeasurementSummary { get; }

    string SelectedDevCodeSnippetId { get; set; }

    string SelectedDevCodeSnippetTitle { get; }

    string SelectedDevCodeSnippetLanguage { get; }

    string SelectedDevCodeSnippetContent { get; }

    ObservableCollection<EditorToolDefinition> ToolButtons { get; }

    ObservableCollection<EditorToolGroupDefinition> ToolGroups { get; }

    ObservableCollection<EditorObjectNode> ObjectNodes { get; }

    ObservableCollection<PropertyEntry> FilteredProperties { get; }

    ObservableCollection<EditorBlendModeItem> BlendModes { get; }

    ObservableCollection<ColorSwatchItem> DocumentColorSwatches { get; }

    ObservableCollection<EditorTextStyleItem> TextStyles { get; }

    ObservableCollection<ColorSwatchItem> ColorStyles { get; }

    ObservableCollection<EditorEffectStyleItem> EffectStyles { get; }

    ObservableCollection<EditorLayoutGuideStyleItem> LayoutGuideStyles { get; }

    ObservableCollection<ColorSwatchItem> SelectionColorSwatches { get; }

    ObservableCollection<EditorSelectionColorItem> SelectionColorItems { get; }

    ObservableCollection<ColorSwatchItem> LibraryPaintStyles { get; }

    string SelectionColorOverflowLabel { get; }

    ObservableCollection<EditorEffectItem> EffectItems { get; }

    ObservableCollection<EditorComponentItem> ComponentAssets { get; }

    ObservableCollection<EditorLibraryItem> Libraries { get; }

    ObservableCollection<EditorFramePresetItem> FramePresets { get; }

    ObservableCollection<EditorPageItem> Pages { get; }

    ObservableCollection<EditorCommentThread> CommentThreads { get; }

    ObservableCollection<EditorDevSpecItem> DevSpecs { get; }

    ObservableCollection<EditorDevCodeSnippet> DevCodeSnippets { get; }

    ObservableCollection<RulerMark> HorizontalRulerMarks { get; }

    ObservableCollection<RulerMark> VerticalRulerMarks { get; }

    ObservableCollection<RulerMarker> HorizontalRulerMarkers { get; }

    ObservableCollection<RulerMarker> VerticalRulerMarkers { get; }
}
