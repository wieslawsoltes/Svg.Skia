using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Svg;
using Svg.Editor.Skia.Uno.Models;
using Windows.System;
using Windows.UI.Core;

namespace Svg.Editor.Skia.Uno.Controls;

public sealed partial class SvgEditorSidebar : UserControl
{
    private SvgElement? _focusedElement;
    private SvgElement? _rangeAnchorElement;

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(ISvgEditorShellViewModel),
            typeof(SvgEditorSidebar),
            new PropertyMetadata(null));

    public SvgEditorSidebar()
    {
        InitializeComponent();
    }

    public ISvgEditorShellViewModel? ViewModel
    {
        get => (ISvgEditorShellViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public event RoutedEventHandler? LayersTabRequested;

    public event RoutedEventHandler? AssetsTabRequested;

    public event RoutedEventHandler? ManageLibrariesRequested;

    public event RoutedEventHandler? PageAddRequested;

    public event EventHandler<PageRequestedEventArgs>? PageSelectionRequested;

    public event EventHandler<ComponentRequestedEventArgs>? ComponentAssetRequested;

    public event ItemClickEventHandler? OutlineItemInvoked;

    public event EventHandler<OutlineSelectionRequestedEventArgs>? OutlineSelectionRequested;

    public event TextChangedEventHandler? OutlineFilterChanged;

    public event RoutedEventHandler? ObjectVisibilityChanged;

    public event RoutedEventHandler? ObjectLockChanged;

    public event EventHandler<OutlineNodeRequestEventArgs>? OutlineNodeExpansionRequested;

    public event EventHandler<OutlineContextRequestedEventArgs>? OutlineContextRequested;

    private void OnAddPageClick(object sender, RoutedEventArgs e)
    {
        PageAddRequested?.Invoke(sender, e);
    }

    private void OnPageClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is EditorPageItem page)
        {
            PageSelectionRequested?.Invoke(this, new PageRequestedEventArgs(page));
        }
    }

    private void OnComponentAssetClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is EditorComponentItem component)
        {
            ComponentAssetRequested?.Invoke(this, new ComponentRequestedEventArgs(component));
        }
    }

    private void OnLayersTabClick(object sender, RoutedEventArgs e)
    {
        LayersTabRequested?.Invoke(sender, e);
    }

    private void OnAssetsTabClick(object sender, RoutedEventArgs e)
    {
        AssetsTabRequested?.Invoke(sender, e);
    }

    private void OnManageLibrariesClick(object sender, RoutedEventArgs e)
    {
        ManageLibrariesRequested?.Invoke(sender, e);
    }

    private void OnAssetsBrowserManageLibrariesRequested(object sender, RoutedEventArgs e)
    {
        ManageLibrariesRequested?.Invoke(sender, e);
    }

    private void OnAssetsBrowserComponentRequested(object sender, ComponentRequestedEventArgs e)
    {
        ComponentAssetRequested?.Invoke(sender, e);
    }

    private void OnOutlineItemClick(object sender, ItemClickEventArgs e)
    {
        OutlineItemInvoked?.Invoke(sender, e);
    }

    private void OnOutlineFilterTextChanged(object sender, TextChangedEventArgs e)
    {
        OutlineFilterChanged?.Invoke(sender, e);
    }

    private void OnObjectVisibilityChanged(object sender, RoutedEventArgs e)
    {
        ObjectVisibilityChanged?.Invoke(sender, e);
    }

    private void OnOutlineExpansionClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not EditorObjectNode node)
        {
            return;
        }

        OutlineNodeExpansionRequested?.Invoke(sender, new OutlineNodeRequestEventArgs(node));
    }

    private void OnObjectVisibilityClick(object sender, RoutedEventArgs e)
    {
        ObjectVisibilityChanged?.Invoke(sender, e);
    }

    private void OnObjectLockClick(object sender, RoutedEventArgs e)
    {
        ObjectLockChanged?.Invoke(sender, e);
    }

    private void OnOutlineItemPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is EditorObjectNode node)
        {
            node.IsPointerOver = true;
        }
    }

    private void OnOutlineItemPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is EditorObjectNode node)
        {
            node.IsPointerOver = false;
        }
    }

    private void OnOutlineItemPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not EditorObjectNode node
            || IsFromInteractiveChild(e.OriginalSource as DependencyObject))
        {
            return;
        }

        OutlineListView.Focus(FocusState.Programmatic);

        var selection = BuildSelectionForNode(node, e.KeyModifiers);
        SetFocusedNode(node, updateAnchor: !e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift));
        OutlineSelectionRequested?.Invoke(this, new OutlineSelectionRequestedEventArgs(selection, node));
        e.Handled = true;
    }

    private void OnOutlineItemRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement element
            || element.DataContext is not EditorObjectNode node)
        {
            return;
        }

        OutlineListView.Focus(FocusState.Programmatic);

        var currentSelection = GetNodes().Where(static item => item.IsSelected).ToList();
        var selection = node.IsSelected && currentSelection.Count > 0
            ? currentSelection
            : BuildSelectionForNode(node, VirtualKeyModifiers.None);

        SetFocusedNode(node, updateAnchor: true);
        OutlineSelectionRequested?.Invoke(this, new OutlineSelectionRequestedEventArgs(selection, node));
        var position = e.GetPosition(element);
        OutlineContextRequested?.Invoke(this, new OutlineContextRequestedEventArgs(node, element, position));
        e.Handled = true;
    }

    private void OnOutlineListRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var nodes = GetNodes();
        SyncSelectionState(nodes);
        var node = GetFocusedNode(nodes);
        if (node is null)
        {
            return;
        }

        var selection = node.IsSelected
            ? nodes.Where(static item => item.IsSelected).ToList()
            : BuildSelectionForNode(node, VirtualKeyModifiers.None);
        SetFocusedNode(node, updateAnchor: true);
        OutlineSelectionRequested?.Invoke(this, new OutlineSelectionRequestedEventArgs(selection, node));
        OutlineContextRequested?.Invoke(this, new OutlineContextRequestedEventArgs(node, OutlineListView, e.GetPosition(OutlineListView)));
        e.Handled = true;
    }

    private void OnOutlineListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var nodes = GetNodes();
        if (nodes.Count == 0)
        {
            return;
        }

        SyncSelectionState(nodes);
        var current = GetFocusedNode(nodes) ?? nodes.First();

        switch (e.Key)
        {
            case VirtualKey.Up:
                MoveSelection(nodes, current, -1, GetKeyboardModifiers());
                e.Handled = true;
                break;
            case VirtualKey.Down:
                MoveSelection(nodes, current, 1, GetKeyboardModifiers());
                e.Handled = true;
                break;
            case VirtualKey.Home:
                ApplySelection(nodes.First(), GetKeyboardModifiers());
                e.Handled = true;
                break;
            case VirtualKey.End:
                ApplySelection(nodes[^1], GetKeyboardModifiers());
                e.Handled = true;
                break;
            case VirtualKey.Left:
                if (current.HasChildren && current.IsExpanded)
                {
                    OutlineNodeExpansionRequested?.Invoke(this, new OutlineNodeRequestEventArgs(current));
                }
                else
                {
                    var parent = FindParentNode(nodes, current);
                    if (parent is not null)
                    {
                        ApplySelection(parent, VirtualKeyModifiers.None);
                    }
                }
                e.Handled = true;
                break;
            case VirtualKey.Right:
                if (current.HasChildren && !current.IsExpanded)
                {
                    OutlineNodeExpansionRequested?.Invoke(this, new OutlineNodeRequestEventArgs(current));
                }
                else
                {
                    var child = FindFirstChildNode(nodes, current);
                    if (child is not null)
                    {
                        ApplySelection(child, VirtualKeyModifiers.None);
                    }
                }
                e.Handled = true;
                break;
            case VirtualKey.Enter:
            case VirtualKey.Space:
                ApplySelection(current, GetKeyboardModifiers());
                e.Handled = true;
                break;
            case VirtualKey.A when IsToggleModifier(GetKeyboardModifiers()):
                SetFocusedNode(nodes.LastOrDefault(node => node.IsSelected) ?? nodes.First(), updateAnchor: true);
                OutlineSelectionRequested?.Invoke(this, new OutlineSelectionRequestedEventArgs(nodes, nodes.LastOrDefault() ?? nodes.First()));
                e.Handled = true;
                break;
        }
    }

    private void MoveSelection(IReadOnlyList<EditorObjectNode> nodes, EditorObjectNode current, int delta, VirtualKeyModifiers modifiers)
    {
        var index = IndexOf(nodes, current);
        if (index < 0)
        {
            index = 0;
        }

        var nextIndex = Math.Clamp(index + delta, 0, nodes.Count - 1);
        ApplySelection(nodes[nextIndex], modifiers);
    }

    private void ApplySelection(EditorObjectNode node, VirtualKeyModifiers modifiers)
    {
        var selection = BuildSelectionForNode(node, modifiers);
        SetFocusedNode(node, updateAnchor: !modifiers.HasFlag(VirtualKeyModifiers.Shift));
        OutlineSelectionRequested?.Invoke(this, new OutlineSelectionRequestedEventArgs(selection, node));
    }

    private IReadOnlyList<EditorObjectNode> BuildSelectionForNode(EditorObjectNode node, VirtualKeyModifiers modifiers)
    {
        var nodes = GetNodes();
        SyncSelectionState(nodes);
        var currentSelection = nodes.Where(static node => node.IsSelected).ToList();
        var isRange = modifiers.HasFlag(VirtualKeyModifiers.Shift);
        var isToggle = IsToggleModifier(modifiers);

        if (isRange)
        {
            var anchor = FindNode(nodes, _rangeAnchorElement);
            if (anchor is null || (currentSelection.Count > 0 && !anchor.IsSelected))
            {
                anchor = currentSelection.LastOrDefault() ?? node;
            }

            var range = GetRange(nodes, anchor, node);
            if (isToggle)
            {
                return MergeSelections(currentSelection, range);
            }

            return range;
        }

        if (isToggle)
        {
            var existingIndex = currentSelection.FindIndex(selected => ReferenceEquals(selected.Element, node.Element));
            if (existingIndex >= 0)
            {
                currentSelection.RemoveAt(existingIndex);
                return currentSelection;
            }

            currentSelection.Add(node);
            return currentSelection;
        }

        return [node];
    }

    private void SetFocusedNode(EditorObjectNode? node, bool updateAnchor)
    {
        _focusedElement = node?.Element;
        if (updateAnchor && node is not null)
        {
            _rangeAnchorElement = node.Element;
        }
    }

    private void SyncSelectionState(IReadOnlyList<EditorObjectNode> nodes)
    {
        var selectedNode = nodes.LastOrDefault(static node => node.IsSelected);
        var focusedNode = FindNode(nodes, _focusedElement);
        if (focusedNode is null || (selectedNode is not null && !focusedNode.IsSelected))
        {
            _focusedElement = selectedNode?.Element ?? nodes.FirstOrDefault()?.Element;
        }

        var anchorNode = FindNode(nodes, _rangeAnchorElement);
        if (anchorNode is null || (selectedNode is not null && !anchorNode.IsSelected))
        {
            _rangeAnchorElement = selectedNode?.Element ?? nodes.FirstOrDefault()?.Element;
        }
    }

    private EditorObjectNode? GetFocusedNode(IReadOnlyList<EditorObjectNode> nodes)
    {
        return FindNode(nodes, _focusedElement)
            ?? nodes.LastOrDefault(static node => node.IsSelected)
            ?? nodes.FirstOrDefault();
    }

    private static IReadOnlyList<EditorObjectNode> GetRange(IReadOnlyList<EditorObjectNode> nodes, EditorObjectNode anchor, EditorObjectNode target)
    {
        var anchorIndex = IndexOf(nodes, anchor);
        var targetIndex = IndexOf(nodes, target);
        if (anchorIndex < 0 || targetIndex < 0)
        {
            return [target];
        }

        var start = Math.Min(anchorIndex, targetIndex);
        var end = Math.Max(anchorIndex, targetIndex);
        var range = new List<EditorObjectNode>();
        for (var index = start; index <= end; index++)
        {
            range.Add(nodes[index]);
        }

        return range;
    }

    private static IReadOnlyList<EditorObjectNode> MergeSelections(IEnumerable<EditorObjectNode> current, IEnumerable<EditorObjectNode> added)
    {
        var result = new List<EditorObjectNode>();
        var seen = new HashSet<SvgElement>(ReferenceEqualityComparer.Instance);

        foreach (var node in current.Concat(added))
        {
            if (seen.Add(node.Element))
            {
                result.Add(node);
            }
        }

        return result;
    }

    private static EditorObjectNode? FindParentNode(IReadOnlyList<EditorObjectNode> nodes, EditorObjectNode node)
    {
        var index = IndexOf(nodes, node);
        if (index <= 0)
        {
            return null;
        }

        for (var candidate = index - 1; candidate >= 0; candidate--)
        {
            if (nodes[candidate].Depth < node.Depth)
            {
                return nodes[candidate];
            }
        }

        return null;
    }

    private static EditorObjectNode? FindFirstChildNode(IReadOnlyList<EditorObjectNode> nodes, EditorObjectNode node)
    {
        var index = IndexOf(nodes, node);
        if (index < 0)
        {
            return null;
        }

        for (var candidate = index + 1; candidate < nodes.Count; candidate++)
        {
            if (nodes[candidate].Depth <= node.Depth)
            {
                return null;
            }

            if (nodes[candidate].Depth == node.Depth + 1)
            {
                return nodes[candidate];
            }
        }

        return null;
    }

    private IReadOnlyList<EditorObjectNode> GetNodes()
    {
        return ViewModel?.ObjectNodes.ToList() ?? [];
    }

    private static EditorObjectNode? FindNode(IEnumerable<EditorObjectNode> nodes, SvgElement? element)
    {
        if (element is null)
        {
            return null;
        }

        return nodes.FirstOrDefault(node => ReferenceEquals(node.Element, element));
    }

    private static bool IsToggleModifier(VirtualKeyModifiers modifiers)
    {
        return modifiers.HasFlag(VirtualKeyModifiers.Control)
            || modifiers.HasFlag(VirtualKeyModifiers.Windows);
    }

    private static int IndexOf(IReadOnlyList<EditorObjectNode> nodes, EditorObjectNode node)
    {
        for (var index = 0; index < nodes.Count; index++)
        {
            if (ReferenceEquals(nodes[index], node))
            {
                return index;
            }
        }

        return -1;
    }

    private static VirtualKeyModifiers GetKeyboardModifiers()
    {
        var modifiers = VirtualKeyModifiers.None;

        if ((InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down)
        {
            modifiers |= VirtualKeyModifiers.Shift;
        }

        if ((InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down)
        {
            modifiers |= VirtualKeyModifiers.Control;
        }

        if ((InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.LeftWindows) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down
            || (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.RightWindows) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down)
        {
            modifiers |= VirtualKeyModifiers.Windows;
        }

        if ((InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down)
        {
            modifiers |= VirtualKeyModifiers.Menu;
        }

        return modifiers;
    }

    private static bool IsFromInteractiveChild(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ButtonBase or TextBox)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }
}
