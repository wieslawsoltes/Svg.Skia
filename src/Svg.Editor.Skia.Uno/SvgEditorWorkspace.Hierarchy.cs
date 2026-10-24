using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Svg.Editor.Skia;
using Windows.ApplicationModel.DataTransfer;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private bool _rebuildingOutline;
    private bool _outlineDragging;
    private SvgEditorController? _outlineDragController;
    private long _outlineDragRevision;
    private SvgElement[] _outlineDragged = Array.Empty<SvgElement>();

    private void InitializeHierarchy()
    {
        DocumentOutline.CanDragItems = true;
        DocumentOutline.CanReorderItems = true;
        DocumentOutline.AllowDrop = true;
        DocumentOutline.SelectionMode = TreeViewSelectionMode.Multiple;
        DocumentOutline.DragItemsStarting += Outline_DragItemsStarting;
        DocumentOutline.DragItemsCompleted += Outline_DragItemsCompleted;
        DocumentOutline.SelectionChanged += Outline_SelectionChanged;
    }

    private void Outline_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
    {
        if (_rebuildingOutline || _refreshing || _outlineDragging || Controller is not { } controller)
            return;
        ApplySafely(() =>
        {
            CommitInspectorEdits();
            Canvas.CancelGesture();
            var selected = sender.SelectedNodes.Select(node => node.Content).OfType<ElementItem>().Select(item => item.Element).ToArray();
            controller.ClearSelection();
            foreach (var visual in selected.OfType<SvgVisualElement>())
                controller.Select(visual, additive: true, allowLocked: true);
            _inspectedElement = selected.FirstOrDefault() ?? controller.Document;
            RefreshInspector();
        });
    }

    private void Outline_DragItemsStarting(TreeView sender, TreeViewDragItemsStartingEventArgs args)
    {
        try
        {
            if (_dialogVisible || _closing || Controller is not { } controller || OutlineSearch.Text.Trim().Length > 0)
                throw new InvalidOperationException("Clear the outline search before dragging document branches.");
            _outlineDragging = true;
            CommitInspectorEdits();
            Canvas.CancelGesture();
            _outlineDragged = args.Items.Select(item => item is TreeViewNode node ? node.Content : item)
                .OfType<ElementItem>().Select(item => item.Element).Distinct().ToArray();
            if (_outlineDragged.Length == 0 || _outlineDragged.Any(SvgEditorController.IsEditLocked))
                throw new InvalidOperationException("Select unlocked branches before dragging them.");
            _outlineDragController = controller;
            _outlineDragRevision = controller.DocumentRevision;
            args.Data.RequestedOperation = DataPackageOperation.Move;
        }
        catch (Exception exception)
        {
            args.Cancel = true;
            _outlineDragging = false;
            _outlineDragged = Array.Empty<SvgElement>();
            StatusText.Text = exception.Message;
        }
    }

    private async void Outline_DragItemsCompleted(TreeView sender, TreeViewDragItemsCompletedEventArgs args)
    {
        string? error = null;
        try
        {
            if (args.DropResult != DataPackageOperation.Move || _outlineDragged.Length == 0)
                return;
            if (Controller is not { } controller || !ReferenceEquals(controller, _outlineDragController) ||
                controller.DocumentRevision != _outlineDragRevision)
                throw new InvalidOperationException("The document changed during the drag. Drag the branches again.");
            var moving = new HashSet<SvgElement>(_outlineDragged);
            var nodes = EnumerateOutlineNodes(DocumentOutline.RootNodes).Where(node => node.Content is ElementItem item && moving.Contains(item.Element)).ToArray();
            var branch = nodes.FirstOrDefault(node => node.Parent is null || node.Parent.Content is not ElementItem parent || !moving.Contains(parent.Element));
            if (branch is null)
                return;
            var destination = (branch.Parent?.Content as ElementItem)?.Element ?? controller.Document!;
            var siblings = (branch.Parent is { } parentNode ? parentNode.Children : DocumentOutline.RootNodes).Reverse().ToArray();
            var insertion = 0;
            foreach (var sibling in siblings)
            {
                if (sibling.Content is not ElementItem item)
                    continue;
                if (moving.Contains(item.Element))
                    break;
                insertion++;
            }
            controller.MoveHierarchyElements(_outlineDragged, destination, insertion);
        }
        catch (Exception exception)
        {
            error = exception.Message;
        }
        finally
        {
            _outlineDragging = false;
            _outlineDragged = Array.Empty<SvgElement>();
            _outlineDragController = null;
            _outlineRevision = -1;
            RefreshWorkspace();
        }
        if (error is not null)
            await ShowErrorAsync(error);
    }

    private static IEnumerable<TreeViewNode> EnumerateOutlineNodes(IEnumerable<TreeViewNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in EnumerateOutlineNodes(node.Children))
                yield return child;
        }
    }

    private async Task MoveToLayerDialogAsync()
    {
        if (Controller is not { Document: { } document } controller || controller.SelectedElements.Count == 0)
            throw new InvalidOperationException("Select objects to move into another group or layer.");
        controller.EnsureSelectionEditable();
        var targets = new[] { (SvgElement)document }.Concat(document.Descendants().OfType<SvgGroup>())
            .Where(element => !SvgEditorController.IsLocked(element) && !controller.SelectedElements.Any(selected =>
                ReferenceEquals(selected, element) || selected.Descendants().Contains(element))).ToArray();
        var list = new ListView { MaxHeight = 320, Width = DialogContentWidth(360), ItemsSource = targets.Select(element => new ElementItem(element)).ToArray() };
        list.SelectedIndex = 0;
        var position = new ComboBox { Header = "Stacking position", HorizontalAlignment = HorizontalAlignment.Stretch };
        position.Items.Add("Front of destination");
        position.Items.Add("Back of destination");
        position.SelectedIndex = 0;
        var preserve = new CheckBox { Content = "Keep position on the canvas", IsChecked = true };
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = "Destination group or layer", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(list);
        panel.Children.Add(position);
        panel.Children.Add(preserve);
        var dialog = MakeDialog("Move objects to layer", panel, "Move");
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(error);
        dialog.PrimaryButtonClick += (_, args) =>
        {
            try
            {
                if (list.SelectedItem is not ElementItem selected)
                    throw new InvalidOperationException("Choose a destination.");
                controller.ReparentSelection(selected.Element, position.SelectedIndex == 0 ? -1 : 0, preserve.IsChecked == true);
            }
            catch (Exception exception) { args.Cancel = true; error.Text = exception.Message; }
        };
        await ShowDialogAsync(dialog);
    }

    private void MoveSelectionOutOfGroup()
    {
        if (Controller is not { } controller || controller.SelectedElements.Count == 0)
            return;
        var parent = controller.SelectedElements[0].Parent;
        if (parent?.Parent is not { } destination || controller.SelectedElements.Any(element => !ReferenceEquals(element.Parent, parent)))
            throw new InvalidOperationException("Select objects in one group to move them to its parent.");
        controller.ReparentSelection(destination, destination.Children.IndexOf(parent) + 1);
    }
}
