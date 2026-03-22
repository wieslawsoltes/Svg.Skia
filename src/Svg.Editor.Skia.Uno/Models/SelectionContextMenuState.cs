namespace Svg.Editor.Skia.Uno.Models;

public sealed class SelectionContextMenuState
{
    public static SelectionContextMenuState Empty { get; } = new();

    public bool CanCopy { get; init; }

    public bool CanPasteHere { get; init; }

    public bool CanPasteReplace { get; init; }

    public bool CanMoveToPage { get; init; }

    public bool CanBringToFront { get; init; }

    public bool CanSendToBack { get; init; }

    public bool CanConvertToSection { get; init; }

    public bool CanGroupSelection { get; init; }

    public bool CanFrameSelection { get; init; }

    public bool CanUngroup { get; init; }

    public bool CanRename { get; init; }

    public bool CanBooleanCombine { get; init; }

    public bool CanFlatten { get; init; }

    public bool CanOutlineStroke { get; init; }

    public bool CanUseAsMask { get; init; }

    public bool CanRemoveAutoLayout { get; init; }

    public bool ShowLayoutOptions { get; init; }

    public bool CanSetAutoLayoutHorizontal { get; init; }

    public bool CanSetAutoLayoutVertical { get; init; }

    public bool CanSetAutoLayoutWrap { get; init; }

    public bool CanSetAutoLayoutGrid { get; init; }

    public bool CanToggleAutoLayoutClipContent { get; init; }

    public bool IsAutoLayoutClipContent { get; init; }

    public bool CanCreateComponent { get; init; }

    public bool CanToggleVisibility { get; init; }

    public string VisibilityText { get; init; } = "Hide selection";

    public bool CanToggleLock { get; init; }

    public string LockText { get; init; } = "Lock selection";

    public bool CanFlipHorizontal { get; init; }

    public bool CanFlipVertical { get; init; }

    public IReadOnlyList<SelectionContextMenuPageItem> PageItems { get; init; } = Array.Empty<SelectionContextMenuPageItem>();

    public IReadOnlyList<SelectionContextMenuLayerItem> LayerItems { get; init; } = Array.Empty<SelectionContextMenuLayerItem>();
}
