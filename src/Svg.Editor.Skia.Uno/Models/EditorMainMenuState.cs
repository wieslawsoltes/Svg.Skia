namespace Svg.Editor.Skia.Uno.Models;

public sealed class EditorMainMenuState
{
    public static EditorMainMenuState Empty { get; } = new();

    public bool CanOpenSvgFile { get; init; }

    public bool CanSaveSvgFile { get; init; }

    public bool CanCreatePage { get; init; }

    public bool CanInsertFrame { get; init; }

    public bool CanDuplicateFrame { get; init; }

    public bool CanResetArtwork { get; init; }

    public bool CanCopyDocumentSvg { get; init; }

    public bool CanCopySelectionSvg { get; init; }

    public bool CanCopyDevSvgSnippet { get; init; }

    public bool CanCopyDevCssSnippet { get; init; }

    public bool CanCopyDevXamlSnippet { get; init; }

    public bool CanCopyDevCSharpSnippet { get; init; }

    public bool CanCopySelection { get; init; }

    public bool CanPasteHere { get; init; }

    public bool CanPasteReplace { get; init; }

    public bool CanDuplicateSelection { get; init; }

    public bool CanDeleteSelection { get; init; }

    public bool CanSelectAll { get; init; }

    public bool CanSelectNone { get; init; }

    public bool IsGridVisible { get; init; }

    public bool IsSnapEnabled { get; init; }

    public bool IsWireframeEnabled { get; init; }

    public bool AreFiltersDisabled { get; init; }

    public bool IsRulersVisible { get; init; }

    public bool CanZoomToSelection { get; init; }

    public bool IsLayersPanelVisible { get; init; }

    public bool IsAssetsPanelVisible { get; init; }

    public bool IsDesignInspectorActive { get; init; }

    public bool IsPrototypeInspectorActive { get; init; }

    public bool IsDevInspectorActive { get; init; }

    public bool IsCommentsInspectorActive { get; init; }

    public bool CanGroupSelection { get; init; }

    public bool CanFrameSelection { get; init; }

    public bool CanUngroupSelection { get; init; }

    public bool CanUseAsMask { get; init; }

    public bool CanToggleVisibility { get; init; }

    public string VisibilityText { get; init; } = "Hide selection";

    public bool CanToggleLock { get; init; }

    public string LockText { get; init; } = "Lock selection";

    public bool CanBringToFront { get; init; }

    public bool CanBringForward { get; init; }

    public bool CanSendBackward { get; init; }

    public bool CanSendToBack { get; init; }

    public bool CanFlipHorizontal { get; init; }

    public bool CanFlipVertical { get; init; }

    public bool CanRotateSelection { get; init; }

    public bool CanAlignSelection { get; init; }

    public bool CanDistributeSelection { get; init; }

    public bool CanBooleanCombineSelection { get; init; }

    public bool CanFlattenSelection { get; init; }

    public bool CanOutlineStroke { get; init; }

    public bool CanManageLibraries { get; init; }

    public bool CanPublishCurrentFileLibrary { get; init; }

    public bool CanUpdateLibraries { get; init; }
}
