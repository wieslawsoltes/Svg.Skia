using Microsoft.UI.Xaml.Controls.Primitives;
using Svg.Editor.Skia.Uno.Models;

namespace Svg.Editor.Skia.Uno.Controls;

public sealed class SvgEditorMainMenu : MenuFlyout
{
    private readonly Dictionary<EditorMainMenuCommand, MenuFlyoutItemBase> _items = [];
    private EditorMainMenuState _state = EditorMainMenuState.Empty;

    public SvgEditorMainMenu()
    {
        Items.Add(CreateItem("Actions…", EditorMainMenuCommand.OpenActionsPalette, CreateKeyboardAccelerator(Windows.System.VirtualKey.K, ctrl: true)));
        Items.Add(new MenuFlyoutSeparator());
        Items.Add(CreateFileMenu());
        Items.Add(CreateEditMenu());
        Items.Add(CreateViewMenu());
        Items.Add(CreateInsertMenu());
        Items.Add(CreateObjectMenu());
        Items.Add(CreateArrangeMenu());
        Items.Add(CreateVectorMenu());
        Items.Add(CreateLibrariesMenu());
        Items.Add(CreateHelpMenu());
        UpdateState();
    }

    public EditorMainMenuState State
    {
        get => _state;
        set
        {
            _state = value ?? EditorMainMenuState.Empty;
            UpdateState();
        }
    }

    public event EventHandler<EditorMainMenuCommandEventArgs>? CommandRequested;

    public new void ShowAt(FrameworkElement target)
    {
        UpdateState();
        base.ShowAt(target, new FlyoutShowOptions
        {
            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
            ShowMode = FlyoutShowMode.Transient
        });
    }

    private MenuFlyoutSubItem CreateFileMenu()
    {
        var menu = new MenuFlyoutSubItem { Text = "File" };
        menu.Items.Add(CreateItem("Open SVG...", EditorMainMenuCommand.OpenSvgFile, CreateKeyboardAccelerator(Windows.System.VirtualKey.O, ctrl: true)));
        menu.Items.Add(CreateItem("Save SVG", EditorMainMenuCommand.SaveSvgFile, CreateKeyboardAccelerator(Windows.System.VirtualKey.S, ctrl: true)));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(CreateItem("New page", EditorMainMenuCommand.NewPage, CreateKeyboardAccelerator(Windows.System.VirtualKey.N, ctrl: true)));
        menu.Items.Add(CreateItem("Insert frame", EditorMainMenuCommand.InsertFrame, CreateKeyboardAccelerator(Windows.System.VirtualKey.F, ctrl: true, shift: true)));
        menu.Items.Add(CreateItem("Duplicate active frame", EditorMainMenuCommand.DuplicateFrame));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(CreateItem("Reset artwork", EditorMainMenuCommand.ResetArtwork));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(CreateItem("Copy document SVG", EditorMainMenuCommand.CopyDocumentSvg));
        menu.Items.Add(CreateItem("Copy selection SVG", EditorMainMenuCommand.CopySelectionSvg));
        menu.Items.Add(CreateItem("Copy Dev SVG", EditorMainMenuCommand.CopyDevSvgSnippet));
        menu.Items.Add(CreateItem("Copy CSS", EditorMainMenuCommand.CopyDevCssSnippet));
        menu.Items.Add(CreateItem("Copy Uno XAML", EditorMainMenuCommand.CopyDevXamlSnippet));
        menu.Items.Add(CreateItem("Copy C#", EditorMainMenuCommand.CopyDevCSharpSnippet));
        return menu;
    }

    private MenuFlyoutSubItem CreateEditMenu()
    {
        var menu = new MenuFlyoutSubItem { Text = "Edit" };
        menu.Items.Add(CreateItem("Copy selection", EditorMainMenuCommand.CopySelection, CreateKeyboardAccelerator(Windows.System.VirtualKey.C, ctrl: true)));
        menu.Items.Add(CreateItem("Paste here", EditorMainMenuCommand.PasteHere, CreateKeyboardAccelerator(Windows.System.VirtualKey.V, ctrl: true)));
        menu.Items.Add(CreateItem("Paste to replace", EditorMainMenuCommand.PasteReplace, CreateKeyboardAccelerator(Windows.System.VirtualKey.R, ctrl: true, shift: true)));
        menu.Items.Add(CreateItem("Duplicate selection", EditorMainMenuCommand.DuplicateSelection, CreateKeyboardAccelerator(Windows.System.VirtualKey.D, ctrl: true)));
        menu.Items.Add(CreateItem("Delete selection", EditorMainMenuCommand.DeleteSelection, CreateKeyboardAccelerator(Windows.System.VirtualKey.Delete)));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(CreateItem("Select all", EditorMainMenuCommand.SelectAll, CreateKeyboardAccelerator(Windows.System.VirtualKey.A, ctrl: true)));
        menu.Items.Add(CreateItem("Select none", EditorMainMenuCommand.SelectNone, CreateKeyboardAccelerator(Windows.System.VirtualKey.Escape)));
        return menu;
    }

    private MenuFlyoutSubItem CreateViewMenu()
    {
        var menu = new MenuFlyoutSubItem { Text = "View" };
        menu.Items.Add(CreateToggleItem("Grid", EditorMainMenuCommand.ToggleGrid));
        menu.Items.Add(CreateToggleItem("Snap", EditorMainMenuCommand.ToggleSnap));
        menu.Items.Add(CreateToggleItem("Rulers", EditorMainMenuCommand.ToggleRulers, CreateKeyboardAccelerator(Windows.System.VirtualKey.R, shift: true)));
        menu.Items.Add(CreateToggleItem("Wireframe", EditorMainMenuCommand.ToggleWireframe));
        menu.Items.Add(CreateToggleItem("Flat filters", EditorMainMenuCommand.ToggleDisableFilters));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(CreateItem("Zoom in", EditorMainMenuCommand.ZoomIn, CreateKeyboardAccelerator(Windows.System.VirtualKey.Add, ctrl: true)));
        menu.Items.Add(CreateItem("Zoom out", EditorMainMenuCommand.ZoomOut, CreateKeyboardAccelerator(Windows.System.VirtualKey.Subtract, ctrl: true)));
        menu.Items.Add(CreateItem("Zoom to 50%", EditorMainMenuCommand.ZoomTo50));
        menu.Items.Add(CreateItem("Zoom to 100%", EditorMainMenuCommand.ZoomTo100, CreateKeyboardAccelerator(Windows.System.VirtualKey.Number0, ctrl: true)));
        menu.Items.Add(CreateItem("Zoom to 200%", EditorMainMenuCommand.ZoomTo200));
        menu.Items.Add(CreateItem("Zoom to fit", EditorMainMenuCommand.ZoomToFit, CreateKeyboardAccelerator(Windows.System.VirtualKey.Number1, shift: true)));
        menu.Items.Add(CreateItem("Zoom to selection", EditorMainMenuCommand.ZoomToSelection, CreateKeyboardAccelerator(Windows.System.VirtualKey.Number2, shift: true)));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(CreateToggleItem("Layers panel", EditorMainMenuCommand.ToggleLayersPanel));
        menu.Items.Add(CreateToggleItem("Assets panel", EditorMainMenuCommand.ToggleAssetsPanel));
        menu.Items.Add(CreateToggleItem("Design inspector", EditorMainMenuCommand.ToggleDesignInspector));
        menu.Items.Add(CreateToggleItem("Prototype inspector", EditorMainMenuCommand.TogglePrototypeInspector));
        menu.Items.Add(CreateToggleItem("Dev mode", EditorMainMenuCommand.ToggleDevInspector, CreateKeyboardAccelerator(Windows.System.VirtualKey.D, shift: true)));
        menu.Items.Add(CreateToggleItem("Comments", EditorMainMenuCommand.ToggleCommentsInspector));
        return menu;
    }

    private MenuFlyoutSubItem CreateInsertMenu()
    {
        var menu = new MenuFlyoutSubItem { Text = "Insert" };
        menu.Items.Add(CreateItem("Select tool", EditorMainMenuCommand.SelectTool, CreateKeyboardAccelerator(Windows.System.VirtualKey.V)));
        menu.Items.Add(CreateItem("Hand tool", EditorMainMenuCommand.HandTool, CreateKeyboardAccelerator(Windows.System.VirtualKey.H)));
        menu.Items.Add(CreateItem("Scale tool", EditorMainMenuCommand.ScaleTool, CreateKeyboardAccelerator(Windows.System.VirtualKey.K)));
        menu.Items.Add(CreateItem("Comment tool", EditorMainMenuCommand.CommentTool, CreateKeyboardAccelerator(Windows.System.VirtualKey.C)));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(CreateItem("Frame tool", EditorMainMenuCommand.FrameTool, CreateKeyboardAccelerator(Windows.System.VirtualKey.F)));
        menu.Items.Add(CreateItem("Section tool", EditorMainMenuCommand.SectionTool, CreateKeyboardAccelerator(Windows.System.VirtualKey.S, shift: true)));
        menu.Items.Add(CreateItem("Slice tool", EditorMainMenuCommand.SliceTool, CreateKeyboardAccelerator(Windows.System.VirtualKey.S)));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(CreateItem("Rectangle tool", EditorMainMenuCommand.RectangleTool, CreateKeyboardAccelerator(Windows.System.VirtualKey.R)));
        menu.Items.Add(CreateItem("Line tool", EditorMainMenuCommand.LineTool, CreateKeyboardAccelerator(Windows.System.VirtualKey.L)));
        menu.Items.Add(CreateItem("Arrow tool", EditorMainMenuCommand.ArrowTool, CreateKeyboardAccelerator(Windows.System.VirtualKey.L, shift: true)));
        menu.Items.Add(CreateItem("Ellipse tool", EditorMainMenuCommand.EllipseTool, CreateKeyboardAccelerator(Windows.System.VirtualKey.O)));
        menu.Items.Add(CreateItem("Polygon tool", EditorMainMenuCommand.PolygonTool));
        menu.Items.Add(CreateItem("Star tool", EditorMainMenuCommand.StarTool));
        menu.Items.Add(CreateItem("Image / video tool", EditorMainMenuCommand.ImageTool));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(CreateItem("Pen tool", EditorMainMenuCommand.PenTool, CreateKeyboardAccelerator(Windows.System.VirtualKey.P)));
        menu.Items.Add(CreateItem("Pencil tool", EditorMainMenuCommand.PencilTool, CreateKeyboardAccelerator(Windows.System.VirtualKey.P, shift: true)));
        menu.Items.Add(CreateItem("Brush tool", EditorMainMenuCommand.BrushTool, CreateKeyboardAccelerator(Windows.System.VirtualKey.B)));
        menu.Items.Add(CreateItem("Text tool", EditorMainMenuCommand.TextTool, CreateKeyboardAccelerator(Windows.System.VirtualKey.T)));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(CreateItem("Instance tool", EditorMainMenuCommand.InstanceTool, CreateKeyboardAccelerator(Windows.System.VirtualKey.U)));
        menu.Items.Add(CreateItem("Insert frame", EditorMainMenuCommand.InsertFrame));
        return menu;
    }

    private MenuFlyoutSubItem CreateObjectMenu()
    {
        var menu = new MenuFlyoutSubItem { Text = "Object" };
        menu.Items.Add(CreateItem("Group selection", EditorMainMenuCommand.GroupSelection, CreateKeyboardAccelerator(Windows.System.VirtualKey.G, ctrl: true)));
        menu.Items.Add(CreateItem("Frame selection", EditorMainMenuCommand.FrameSelection, CreateKeyboardAccelerator(Windows.System.VirtualKey.G, ctrl: true, shift: true)));
        menu.Items.Add(CreateItem("Ungroup selection", EditorMainMenuCommand.UngroupSelection));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(CreateItem("Use as mask", EditorMainMenuCommand.UseAsMask, CreateKeyboardAccelerator(Windows.System.VirtualKey.M, ctrl: true, shift: true)));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(CreateItem("Hide selection", EditorMainMenuCommand.ToggleVisibility, CreateKeyboardAccelerator(Windows.System.VirtualKey.H, ctrl: true, shift: true)));
        menu.Items.Add(CreateItem("Lock selection", EditorMainMenuCommand.ToggleLock, CreateKeyboardAccelerator(Windows.System.VirtualKey.L, ctrl: true, shift: true)));
        return menu;
    }

    private MenuFlyoutSubItem CreateArrangeMenu()
    {
        var menu = new MenuFlyoutSubItem { Text = "Arrange" };
        menu.Items.Add(CreateItem("Bring to front", EditorMainMenuCommand.BringToFront));
        menu.Items.Add(CreateItem("Bring forward", EditorMainMenuCommand.BringForward));
        menu.Items.Add(CreateItem("Send backward", EditorMainMenuCommand.SendBackward));
        menu.Items.Add(CreateItem("Send to back", EditorMainMenuCommand.SendToBack));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(CreateItem("Flip horizontal", EditorMainMenuCommand.FlipHorizontal, CreateKeyboardAccelerator(Windows.System.VirtualKey.H, shift: true)));
        menu.Items.Add(CreateItem("Flip vertical", EditorMainMenuCommand.FlipVertical, CreateKeyboardAccelerator(Windows.System.VirtualKey.V, shift: true)));
        menu.Items.Add(CreateItem("Rotate 90° left", EditorMainMenuCommand.RotateLeft90));
        menu.Items.Add(CreateItem("Rotate 90° right", EditorMainMenuCommand.RotateRight90));
        menu.Items.Add(CreateItem("Rotate 180°", EditorMainMenuCommand.Rotate180));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(CreateAlignMenu());
        menu.Items.Add(CreateDistributeMenu());
        return menu;
    }

    private MenuFlyoutSubItem CreateAlignMenu()
    {
        var menu = new MenuFlyoutSubItem { Text = "Align" };
        menu.Items.Add(CreateItem("Left", EditorMainMenuCommand.AlignLeft));
        menu.Items.Add(CreateItem("Horizontal centers", EditorMainMenuCommand.AlignHorizontalCenters));
        menu.Items.Add(CreateItem("Right", EditorMainMenuCommand.AlignRight));
        menu.Items.Add(CreateItem("Top", EditorMainMenuCommand.AlignTop));
        menu.Items.Add(CreateItem("Vertical centers", EditorMainMenuCommand.AlignVerticalCenters));
        menu.Items.Add(CreateItem("Bottom", EditorMainMenuCommand.AlignBottom));
        return menu;
    }

    private MenuFlyoutSubItem CreateDistributeMenu()
    {
        var menu = new MenuFlyoutSubItem { Text = "Distribute" };
        menu.Items.Add(CreateItem("Horizontal", EditorMainMenuCommand.DistributeHorizontal));
        menu.Items.Add(CreateItem("Vertical", EditorMainMenuCommand.DistributeVertical));
        return menu;
    }

    private MenuFlyoutSubItem CreateVectorMenu()
    {
        var menu = new MenuFlyoutSubItem { Text = "Vector" };
        menu.Items.Add(CreateItem("Union", EditorMainMenuCommand.BooleanUnion, CreateKeyboardAccelerator(Windows.System.VirtualKey.U, ctrl: true, shift: true)));
        menu.Items.Add(CreateItem("Subtract", EditorMainMenuCommand.BooleanSubtract, CreateKeyboardAccelerator(Windows.System.VirtualKey.D, ctrl: true, shift: true)));
        menu.Items.Add(CreateItem("Intersect", EditorMainMenuCommand.BooleanIntersect, CreateKeyboardAccelerator(Windows.System.VirtualKey.I, ctrl: true, shift: true)));
        menu.Items.Add(CreateItem("Exclude", EditorMainMenuCommand.BooleanExclude, CreateKeyboardAccelerator(Windows.System.VirtualKey.E, ctrl: true, shift: true)));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(CreateItem("Flatten selection", EditorMainMenuCommand.FlattenSelection, CreateKeyboardAccelerator(Windows.System.VirtualKey.F, ctrl: true, shift: true)));
        menu.Items.Add(CreateItem("Outline stroke", EditorMainMenuCommand.OutlineStroke, CreateKeyboardAccelerator(Windows.System.VirtualKey.O, ctrl: true, shift: true)));
        return menu;
    }

    private MenuFlyoutSubItem CreateHelpMenu()
    {
        var menu = new MenuFlyoutSubItem { Text = "Help" };
        menu.Items.Add(CreateItem("Keyboard shortcuts", EditorMainMenuCommand.ShowKeyboardShortcuts));
        menu.Items.Add(CreateItem("About", EditorMainMenuCommand.ShowAbout));
        return menu;
    }

    private MenuFlyoutSubItem CreateLibrariesMenu()
    {
        var menu = new MenuFlyoutSubItem { Text = "Libraries" };
        menu.Items.Add(CreateItem("Manage libraries", EditorMainMenuCommand.ManageLibraries, CreateKeyboardAccelerator(Windows.System.VirtualKey.L, ctrl: true, shift: true, alt: true)));
        menu.Items.Add(CreateItem("Publish this file", EditorMainMenuCommand.PublishCurrentFileLibrary));
        menu.Items.Add(CreateItem("Update connected libraries", EditorMainMenuCommand.UpdateLibraries));
        return menu;
    }

    private MenuFlyoutItem CreateItem(string text, EditorMainMenuCommand command, params KeyboardAccelerator[] accelerators)
    {
        var item = new MenuFlyoutItem
        {
            Text = text,
            Tag = command
        };

        foreach (var accelerator in accelerators)
        {
            item.KeyboardAccelerators.Add(accelerator);
        }

        item.Click += OnItemClick;
        _items[command] = item;
        return item;
    }

    private ToggleMenuFlyoutItem CreateToggleItem(string text, EditorMainMenuCommand command, params KeyboardAccelerator[] accelerators)
    {
        var item = new ToggleMenuFlyoutItem
        {
            Text = text,
            Tag = command
        };

        foreach (var accelerator in accelerators)
        {
            item.KeyboardAccelerators.Add(accelerator);
        }

        item.Click += OnItemClick;
        _items[command] = item;
        return item;
    }

    private void OnItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItemBase { Tag: EditorMainMenuCommand command })
        {
            return;
        }

        CommandRequested?.Invoke(this, new EditorMainMenuCommandEventArgs(command));
    }

    private void UpdateState()
    {
        SetEnabled(EditorMainMenuCommand.OpenSvgFile, _state.CanOpenSvgFile);
        SetEnabled(EditorMainMenuCommand.SaveSvgFile, _state.CanSaveSvgFile);
        SetEnabled(EditorMainMenuCommand.NewPage, _state.CanCreatePage);
        SetEnabled(EditorMainMenuCommand.InsertFrame, _state.CanInsertFrame);
        SetEnabled(EditorMainMenuCommand.DuplicateFrame, _state.CanDuplicateFrame);
        SetEnabled(EditorMainMenuCommand.ResetArtwork, _state.CanResetArtwork);
        SetEnabled(EditorMainMenuCommand.CopyDocumentSvg, _state.CanCopyDocumentSvg);
        SetEnabled(EditorMainMenuCommand.CopySelectionSvg, _state.CanCopySelectionSvg);
        SetEnabled(EditorMainMenuCommand.CopyDevSvgSnippet, _state.CanCopyDevSvgSnippet);
        SetEnabled(EditorMainMenuCommand.CopyDevCssSnippet, _state.CanCopyDevCssSnippet);
        SetEnabled(EditorMainMenuCommand.CopyDevXamlSnippet, _state.CanCopyDevXamlSnippet);
        SetEnabled(EditorMainMenuCommand.CopyDevCSharpSnippet, _state.CanCopyDevCSharpSnippet);
        SetEnabled(EditorMainMenuCommand.CopySelection, _state.CanCopySelection);
        SetEnabled(EditorMainMenuCommand.PasteHere, _state.CanPasteHere);
        SetEnabled(EditorMainMenuCommand.PasteReplace, _state.CanPasteReplace);
        SetEnabled(EditorMainMenuCommand.DuplicateSelection, _state.CanDuplicateSelection);
        SetEnabled(EditorMainMenuCommand.DeleteSelection, _state.CanDeleteSelection);
        SetEnabled(EditorMainMenuCommand.SelectAll, _state.CanSelectAll);
        SetEnabled(EditorMainMenuCommand.SelectNone, _state.CanSelectNone);

        SetChecked(EditorMainMenuCommand.ToggleGrid, _state.IsGridVisible);
        SetChecked(EditorMainMenuCommand.ToggleSnap, _state.IsSnapEnabled);
        SetChecked(EditorMainMenuCommand.ToggleRulers, _state.IsRulersVisible);
        SetChecked(EditorMainMenuCommand.ToggleWireframe, _state.IsWireframeEnabled);
        SetChecked(EditorMainMenuCommand.ToggleDisableFilters, _state.AreFiltersDisabled);
        SetEnabled(EditorMainMenuCommand.ZoomToSelection, _state.CanZoomToSelection);
        SetChecked(EditorMainMenuCommand.ToggleLayersPanel, _state.IsLayersPanelVisible);
        SetChecked(EditorMainMenuCommand.ToggleAssetsPanel, _state.IsAssetsPanelVisible);
        SetChecked(EditorMainMenuCommand.ToggleDesignInspector, _state.IsDesignInspectorActive);
        SetChecked(EditorMainMenuCommand.TogglePrototypeInspector, _state.IsPrototypeInspectorActive);
        SetChecked(EditorMainMenuCommand.ToggleDevInspector, _state.IsDevInspectorActive);
        SetChecked(EditorMainMenuCommand.ToggleCommentsInspector, _state.IsCommentsInspectorActive);

        SetEnabled(EditorMainMenuCommand.InstanceTool, true);

        SetEnabled(EditorMainMenuCommand.GroupSelection, _state.CanGroupSelection);
        SetEnabled(EditorMainMenuCommand.FrameSelection, _state.CanFrameSelection);
        SetEnabled(EditorMainMenuCommand.UngroupSelection, _state.CanUngroupSelection);
        SetEnabled(EditorMainMenuCommand.UseAsMask, _state.CanUseAsMask);
        SetEnabled(EditorMainMenuCommand.ToggleVisibility, _state.CanToggleVisibility);
        SetText(EditorMainMenuCommand.ToggleVisibility, _state.VisibilityText);
        SetEnabled(EditorMainMenuCommand.ToggleLock, _state.CanToggleLock);
        SetText(EditorMainMenuCommand.ToggleLock, _state.LockText);

        SetEnabled(EditorMainMenuCommand.BringToFront, _state.CanBringToFront);
        SetEnabled(EditorMainMenuCommand.BringForward, _state.CanBringForward);
        SetEnabled(EditorMainMenuCommand.SendBackward, _state.CanSendBackward);
        SetEnabled(EditorMainMenuCommand.SendToBack, _state.CanSendToBack);
        SetEnabled(EditorMainMenuCommand.FlipHorizontal, _state.CanFlipHorizontal);
        SetEnabled(EditorMainMenuCommand.FlipVertical, _state.CanFlipVertical);
        SetEnabled(EditorMainMenuCommand.RotateLeft90, _state.CanRotateSelection);
        SetEnabled(EditorMainMenuCommand.RotateRight90, _state.CanRotateSelection);
        SetEnabled(EditorMainMenuCommand.Rotate180, _state.CanRotateSelection);
        SetEnabled(EditorMainMenuCommand.AlignLeft, _state.CanAlignSelection);
        SetEnabled(EditorMainMenuCommand.AlignHorizontalCenters, _state.CanAlignSelection);
        SetEnabled(EditorMainMenuCommand.AlignRight, _state.CanAlignSelection);
        SetEnabled(EditorMainMenuCommand.AlignTop, _state.CanAlignSelection);
        SetEnabled(EditorMainMenuCommand.AlignVerticalCenters, _state.CanAlignSelection);
        SetEnabled(EditorMainMenuCommand.AlignBottom, _state.CanAlignSelection);
        SetEnabled(EditorMainMenuCommand.DistributeHorizontal, _state.CanDistributeSelection);
        SetEnabled(EditorMainMenuCommand.DistributeVertical, _state.CanDistributeSelection);

        SetEnabled(EditorMainMenuCommand.BooleanUnion, _state.CanBooleanCombineSelection);
        SetEnabled(EditorMainMenuCommand.BooleanSubtract, _state.CanBooleanCombineSelection);
        SetEnabled(EditorMainMenuCommand.BooleanIntersect, _state.CanBooleanCombineSelection);
        SetEnabled(EditorMainMenuCommand.BooleanExclude, _state.CanBooleanCombineSelection);
        SetEnabled(EditorMainMenuCommand.FlattenSelection, _state.CanFlattenSelection);
        SetEnabled(EditorMainMenuCommand.OutlineStroke, _state.CanOutlineStroke);
        SetEnabled(EditorMainMenuCommand.ManageLibraries, _state.CanManageLibraries);
        SetEnabled(EditorMainMenuCommand.PublishCurrentFileLibrary, _state.CanPublishCurrentFileLibrary);
        SetEnabled(EditorMainMenuCommand.UpdateLibraries, _state.CanUpdateLibraries);
    }

    private void SetEnabled(EditorMainMenuCommand command, bool isEnabled)
    {
        if (_items.TryGetValue(command, out var item))
        {
            item.IsEnabled = isEnabled;
        }
    }

    private void SetChecked(EditorMainMenuCommand command, bool isChecked)
    {
        if (_items.TryGetValue(command, out var item)
            && item is ToggleMenuFlyoutItem toggle)
        {
            toggle.IsChecked = isChecked;
        }
    }

    private void SetText(EditorMainMenuCommand command, string text)
    {
        if (!_items.TryGetValue(command, out var item))
        {
            return;
        }

        if (item is MenuFlyoutItem menuItem)
        {
            menuItem.Text = text;
        }
    }

    private static KeyboardAccelerator CreateKeyboardAccelerator(Windows.System.VirtualKey key, bool ctrl = false, bool shift = false, bool alt = false)
    {
        var accelerator = new KeyboardAccelerator
        {
            Key = key
        };

        if (ctrl)
        {
            accelerator.Modifiers |= Windows.System.VirtualKeyModifiers.Control;
        }

        if (shift)
        {
            accelerator.Modifiers |= Windows.System.VirtualKeyModifiers.Shift;
        }

        if (alt)
        {
            accelerator.Modifiers |= Windows.System.VirtualKeyModifiers.Menu;
        }

        return accelerator;
    }
}
