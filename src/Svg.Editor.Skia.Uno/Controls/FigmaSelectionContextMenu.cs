using Microsoft.UI.Xaml.Controls.Primitives;
using Svg.Editor.Skia.Uno.Models;
using Windows.Foundation;

namespace Svg.Editor.Skia.Uno.Controls;

public sealed class FigmaSelectionContextMenu : MenuFlyout
{
    private readonly MenuFlyoutItem _copyItem;
    private readonly MenuFlyoutItem _pasteHereItem;
    private readonly MenuFlyoutItem _pasteReplaceItem;
    private readonly MenuFlyoutSubItem _selectLayerItem;
    private readonly MenuFlyoutSubItem _moveToPageItem;
    private readonly MenuFlyoutItem _bringToFrontItem;
    private readonly MenuFlyoutItem _sendToBackItem;
    private readonly MenuFlyoutItem _convertToSectionItem;
    private readonly MenuFlyoutItem _groupSelectionItem;
    private readonly MenuFlyoutItem _frameSelectionItem;
    private readonly MenuFlyoutItem _ungroupItem;
    private readonly MenuFlyoutItem _renameItem;
    private readonly MenuFlyoutItem _booleanUnionItem;
    private readonly MenuFlyoutItem _booleanSubtractItem;
    private readonly MenuFlyoutItem _booleanIntersectItem;
    private readonly MenuFlyoutItem _booleanExcludeItem;
    private readonly MenuFlyoutItem _flattenItem;
    private readonly MenuFlyoutItem _outlineStrokeItem;
    private readonly MenuFlyoutItem _useAsMaskItem;
    private readonly MenuFlyoutItem _removeAutoLayoutItem;
    private readonly MenuFlyoutSubItem _layoutOptionsItem;
    private readonly MenuFlyoutItem _layoutHorizontalItem;
    private readonly MenuFlyoutItem _layoutVerticalItem;
    private readonly MenuFlyoutItem _layoutWrapItem;
    private readonly MenuFlyoutItem _layoutGridItem;
    private readonly ToggleMenuFlyoutItem _layoutClipItem;
    private readonly MenuFlyoutItem _createComponentItem;
    private readonly MenuFlyoutItem _toggleVisibilityItem;
    private readonly MenuFlyoutItem _toggleLockItem;
    private readonly MenuFlyoutItem _flipHorizontalItem;
    private readonly MenuFlyoutItem _flipVerticalItem;

    private readonly MenuFlyoutSeparator _clipboardSeparator;
    private readonly MenuFlyoutSeparator _zOrderSeparator;
    private readonly MenuFlyoutSeparator _structureSeparator;
    private readonly MenuFlyoutSeparator _layoutSeparator;
    private readonly MenuFlyoutSeparator _visibilitySeparator;

    private SelectionContextMenuState _state = SelectionContextMenuState.Empty;

    public FigmaSelectionContextMenu()
    {
        _copyItem = CreateItem("Copy", SelectionContextMenuCommand.Copy);
        _pasteHereItem = CreateItem("Paste here", SelectionContextMenuCommand.PasteHere);
        _pasteReplaceItem = CreateItem("Paste to replace", SelectionContextMenuCommand.PasteReplace);
        _selectLayerItem = new MenuFlyoutSubItem { Text = "Select layer" };
        _moveToPageItem = new MenuFlyoutSubItem { Text = "Move to page" };
        _bringToFrontItem = CreateItem("Bring to front", SelectionContextMenuCommand.BringToFront);
        _sendToBackItem = CreateItem("Send to back", SelectionContextMenuCommand.SendToBack);
        _convertToSectionItem = CreateItem("Convert to section", SelectionContextMenuCommand.ConvertToSection);
        _groupSelectionItem = CreateItem("Group selection", SelectionContextMenuCommand.GroupSelection);
        _frameSelectionItem = CreateItem("Frame selection", SelectionContextMenuCommand.FrameSelection);
        _ungroupItem = CreateItem("Ungroup", SelectionContextMenuCommand.Ungroup);
        _renameItem = CreateItem("Rename", SelectionContextMenuCommand.Rename);
        _booleanUnionItem = CreateItem("Union", SelectionContextMenuCommand.BooleanUnion);
        _booleanSubtractItem = CreateItem("Subtract", SelectionContextMenuCommand.BooleanSubtract);
        _booleanIntersectItem = CreateItem("Intersect", SelectionContextMenuCommand.BooleanIntersect);
        _booleanExcludeItem = CreateItem("Exclude", SelectionContextMenuCommand.BooleanExclude);
        _flattenItem = CreateItem("Flatten", SelectionContextMenuCommand.Flatten);
        _outlineStrokeItem = CreateItem("Outline stroke", SelectionContextMenuCommand.OutlineStroke);
        _useAsMaskItem = CreateItem("Use as mask", SelectionContextMenuCommand.UseAsMask);
        _removeAutoLayoutItem = CreateItem("Remove auto layout", SelectionContextMenuCommand.RemoveAutoLayout);
        _layoutOptionsItem = new MenuFlyoutSubItem { Text = "More layout options" };
        _layoutHorizontalItem = CreateItem("Horizontal flow", SelectionContextMenuCommand.SetAutoLayoutHorizontal);
        _layoutVerticalItem = CreateItem("Vertical flow", SelectionContextMenuCommand.SetAutoLayoutVertical);
        _layoutWrapItem = CreateItem("Wrap flow", SelectionContextMenuCommand.SetAutoLayoutWrap);
        _layoutGridItem = CreateItem("Grid flow", SelectionContextMenuCommand.SetAutoLayoutGrid);
        _layoutClipItem = new ToggleMenuFlyoutItem { Text = "Clip content" };
        _layoutClipItem.Click += OnLayoutClipClick;
        _createComponentItem = CreateItem("Create component", SelectionContextMenuCommand.CreateComponent);
        _toggleVisibilityItem = CreateItem("Hide selection", SelectionContextMenuCommand.ToggleVisibility);
        _toggleLockItem = CreateItem("Lock selection", SelectionContextMenuCommand.ToggleLock);
        _flipHorizontalItem = CreateItem("Flip horizontal", SelectionContextMenuCommand.FlipHorizontal);
        _flipVerticalItem = CreateItem("Flip vertical", SelectionContextMenuCommand.FlipVertical);

        _clipboardSeparator = new MenuFlyoutSeparator();
        _zOrderSeparator = new MenuFlyoutSeparator();
        _structureSeparator = new MenuFlyoutSeparator();
        _layoutSeparator = new MenuFlyoutSeparator();
        _visibilitySeparator = new MenuFlyoutSeparator();

        _copyItem.KeyboardAccelerators.Add(CreateKeyboardAccelerator(Windows.System.VirtualKey.C, ctrl: true));
        _pasteReplaceItem.KeyboardAccelerators.Add(CreateKeyboardAccelerator(Windows.System.VirtualKey.R, ctrl: true, shift: true));
        _groupSelectionItem.KeyboardAccelerators.Add(CreateKeyboardAccelerator(Windows.System.VirtualKey.G, ctrl: true));
        _frameSelectionItem.KeyboardAccelerators.Add(CreateKeyboardAccelerator(Windows.System.VirtualKey.G, ctrl: true, shift: true));
        _renameItem.KeyboardAccelerators.Add(CreateKeyboardAccelerator(Windows.System.VirtualKey.R, ctrl: true));
        _booleanUnionItem.KeyboardAccelerators.Add(CreateKeyboardAccelerator(Windows.System.VirtualKey.U, ctrl: true, shift: true));
        _booleanSubtractItem.KeyboardAccelerators.Add(CreateKeyboardAccelerator(Windows.System.VirtualKey.D, ctrl: true, shift: true));
        _booleanIntersectItem.KeyboardAccelerators.Add(CreateKeyboardAccelerator(Windows.System.VirtualKey.I, ctrl: true, shift: true));
        _booleanExcludeItem.KeyboardAccelerators.Add(CreateKeyboardAccelerator(Windows.System.VirtualKey.E, ctrl: true, shift: true));
        _outlineStrokeItem.KeyboardAccelerators.Add(CreateKeyboardAccelerator(Windows.System.VirtualKey.O, ctrl: true, shift: true));
        _removeAutoLayoutItem.KeyboardAccelerators.Add(CreateKeyboardAccelerator(Windows.System.VirtualKey.A, ctrl: true, shift: true));
        _createComponentItem.KeyboardAccelerators.Add(CreateKeyboardAccelerator(Windows.System.VirtualKey.K, ctrl: true, shift: true));
        _toggleVisibilityItem.KeyboardAccelerators.Add(CreateKeyboardAccelerator(Windows.System.VirtualKey.H, ctrl: true, shift: true));
        _toggleLockItem.KeyboardAccelerators.Add(CreateKeyboardAccelerator(Windows.System.VirtualKey.L, ctrl: true, shift: true));
        _flipHorizontalItem.KeyboardAccelerators.Add(CreateKeyboardAccelerator(Windows.System.VirtualKey.H, shift: true));
        _flipVerticalItem.KeyboardAccelerators.Add(CreateKeyboardAccelerator(Windows.System.VirtualKey.V, shift: true));

        _layoutOptionsItem.Items.Add(_layoutHorizontalItem);
        _layoutOptionsItem.Items.Add(_layoutVerticalItem);
        _layoutOptionsItem.Items.Add(_layoutWrapItem);
        _layoutOptionsItem.Items.Add(_layoutGridItem);
        _layoutOptionsItem.Items.Add(new MenuFlyoutSeparator());
        _layoutOptionsItem.Items.Add(_layoutClipItem);

        Items.Add(_copyItem);
        Items.Add(_pasteHereItem);
        Items.Add(_pasteReplaceItem);
        Items.Add(_clipboardSeparator);
        Items.Add(_selectLayerItem);
        Items.Add(_moveToPageItem);
        Items.Add(_bringToFrontItem);
        Items.Add(_sendToBackItem);
        Items.Add(_zOrderSeparator);
        Items.Add(_convertToSectionItem);
        Items.Add(_groupSelectionItem);
        Items.Add(_frameSelectionItem);
        Items.Add(_ungroupItem);
        Items.Add(_renameItem);
        Items.Add(_booleanUnionItem);
        Items.Add(_booleanSubtractItem);
        Items.Add(_booleanIntersectItem);
        Items.Add(_booleanExcludeItem);
        Items.Add(_flattenItem);
        Items.Add(_outlineStrokeItem);
        Items.Add(_useAsMaskItem);
        Items.Add(_structureSeparator);
        Items.Add(_removeAutoLayoutItem);
        Items.Add(_layoutOptionsItem);
        Items.Add(_createComponentItem);
        Items.Add(_layoutSeparator);
        Items.Add(_toggleVisibilityItem);
        Items.Add(_toggleLockItem);
        Items.Add(_visibilitySeparator);
        Items.Add(_flipHorizontalItem);
        Items.Add(_flipVerticalItem);

        UpdateState();
    }

    public SelectionContextMenuState State
    {
        get => _state;
        set
        {
            _state = value ?? SelectionContextMenuState.Empty;
            UpdateState();
        }
    }

    public event EventHandler<SelectionContextMenuCommandEventArgs>? CommandRequested;

    public void ShowAt(FrameworkElement target, Point? position)
    {
        UpdateState();

        if (position.HasValue)
        {
            base.ShowAt(target, new FlyoutShowOptions
            {
                Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                Position = position.Value,
                ShowMode = FlyoutShowMode.Transient
            });
            return;
        }

        base.ShowAt(target, new FlyoutShowOptions
        {
            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
            ShowMode = FlyoutShowMode.Transient
        });
    }

    private void UpdateState()
    {
        _copyItem.IsEnabled = _state.CanCopy;
        _pasteHereItem.IsEnabled = _state.CanPasteHere;
        _pasteReplaceItem.IsEnabled = _state.CanPasteReplace;
        _moveToPageItem.IsEnabled = _state.CanMoveToPage;
        _bringToFrontItem.IsEnabled = _state.CanBringToFront;
        _sendToBackItem.IsEnabled = _state.CanSendToBack;
        _convertToSectionItem.IsEnabled = _state.CanConvertToSection;
        _groupSelectionItem.IsEnabled = _state.CanGroupSelection;
        _frameSelectionItem.IsEnabled = _state.CanFrameSelection;
        _ungroupItem.IsEnabled = _state.CanUngroup;
        _renameItem.IsEnabled = _state.CanRename;
        _booleanUnionItem.IsEnabled = _state.CanBooleanCombine;
        _booleanSubtractItem.IsEnabled = _state.CanBooleanCombine;
        _booleanIntersectItem.IsEnabled = _state.CanBooleanCombine;
        _booleanExcludeItem.IsEnabled = _state.CanBooleanCombine;
        _flattenItem.IsEnabled = _state.CanFlatten;
        _outlineStrokeItem.IsEnabled = _state.CanOutlineStroke;
        _useAsMaskItem.IsEnabled = _state.CanUseAsMask;
        _removeAutoLayoutItem.IsEnabled = _state.CanRemoveAutoLayout;
        _layoutHorizontalItem.IsEnabled = _state.CanSetAutoLayoutHorizontal;
        _layoutVerticalItem.IsEnabled = _state.CanSetAutoLayoutVertical;
        _layoutWrapItem.IsEnabled = _state.CanSetAutoLayoutWrap;
        _layoutGridItem.IsEnabled = _state.CanSetAutoLayoutGrid;
        _layoutClipItem.IsEnabled = _state.CanToggleAutoLayoutClipContent;
        _createComponentItem.IsEnabled = _state.CanCreateComponent;
        _toggleVisibilityItem.IsEnabled = _state.CanToggleVisibility;
        _toggleVisibilityItem.Text = _state.VisibilityText;
        _toggleLockItem.IsEnabled = _state.CanToggleLock;
        _toggleLockItem.Text = _state.LockText;
        _flipHorizontalItem.IsEnabled = _state.CanFlipHorizontal;
        _flipVerticalItem.IsEnabled = _state.CanFlipVertical;

        _selectLayerItem.Visibility = _state.LayerItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _moveToPageItem.Visibility = _state.PageItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _layoutOptionsItem.Visibility = _state.ShowLayoutOptions ? Visibility.Visible : Visibility.Collapsed;
        _removeAutoLayoutItem.Visibility = _state.CanRemoveAutoLayout ? Visibility.Visible : Visibility.Collapsed;

        _layoutClipItem.IsChecked = _state.IsAutoLayoutClipContent;

        _clipboardSeparator.Visibility = _state.LayerItems.Count > 0 || _state.PageItems.Count > 0 || _state.CanBringToFront || _state.CanSendToBack
            ? Visibility.Visible
            : Visibility.Collapsed;
        _zOrderSeparator.Visibility = _state.CanConvertToSection || _state.CanGroupSelection || _state.CanFrameSelection || _state.CanUngroup || _state.CanRename || _state.CanBooleanCombine || _state.CanFlatten || _state.CanOutlineStroke || _state.CanUseAsMask
            ? Visibility.Visible
            : Visibility.Collapsed;
        _structureSeparator.Visibility = _state.CanRemoveAutoLayout || _state.ShowLayoutOptions || _state.CanCreateComponent
            ? Visibility.Visible
            : Visibility.Collapsed;
        _layoutSeparator.Visibility = _state.CanToggleVisibility || _state.CanToggleLock
            ? Visibility.Visible
            : Visibility.Collapsed;
        _visibilitySeparator.Visibility = _state.CanFlipHorizontal || _state.CanFlipVertical
            ? Visibility.Visible
            : Visibility.Collapsed;

        _selectLayerItem.Items.Clear();
        foreach (var layer in _state.LayerItems)
        {
            var item = new MenuFlyoutItem
            {
                Text = layer.DisplayText,
                Tag = layer.Key
            };
            if (layer.IsSelected)
            {
                item.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            }

            item.Click += OnSelectLayerClick;
            _selectLayerItem.Items.Add(item);
        }

        _moveToPageItem.Items.Clear();
        foreach (var page in _state.PageItems)
        {
            var item = new MenuFlyoutItem
            {
                Text = page.DisplayText,
                Tag = page.Key
            };
            item.Click += OnMoveToPageClick;
            _moveToPageItem.Items.Add(item);
        }
    }

    private MenuFlyoutItem CreateItem(string text, SelectionContextMenuCommand command)
    {
        var item = new MenuFlyoutItem
        {
            Text = text,
            Tag = command
        };
        item.Click += OnCommandClick;
        return item;
    }

    private void OnCommandClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: SelectionContextMenuCommand command })
        {
            return;
        }

        CommandRequested?.Invoke(this, new SelectionContextMenuCommandEventArgs(command));
    }

    private void OnSelectLayerClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: string key })
        {
            return;
        }

        CommandRequested?.Invoke(this, new SelectionContextMenuCommandEventArgs(SelectionContextMenuCommand.SelectLayer, key));
    }

    private void OnMoveToPageClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: string key })
        {
            return;
        }

        CommandRequested?.Invoke(this, new SelectionContextMenuCommandEventArgs(SelectionContextMenuCommand.MoveToPage, key));
    }

    private void OnLayoutClipClick(object sender, RoutedEventArgs e)
    {
        CommandRequested?.Invoke(this, new SelectionContextMenuCommandEventArgs(SelectionContextMenuCommand.ToggleAutoLayoutClipContent));
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
