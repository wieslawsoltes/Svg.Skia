using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Svg.Editor.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Core;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private string? _pendingCompactMenuCommand;
    private bool _spacePreviousPanMode;

    private void BuildMenus()
    {
        CompactMenu.Closed += (_, _) =>
        {
            var pending = _pendingCompactMenuCommand; _pendingCompactMenuCommand = null;
            if (pending is null) return;
            DispatcherQueue.TryEnqueue(() => { if (!_disposed) _ = RunCommandAsync(pending); });
        };
        foreach (var group in _commands.GroupBy(command => command.Group))
        {
            var menu = new MenuBarItem { Title = group.Key };
            var compact = new MenuFlyoutSubItem { Text = group.Key };
            foreach (var command in group)
            {
                var item = new MenuFlyoutItem { Text = command.Label, KeyboardAcceleratorTextOverride = command.Shortcut };
                item.Click += async (_, _) => await RunCommandAsync(command.Id); menu.Items.Add(item);
                var compactItem = new MenuFlyoutItem { Text = command.Label, KeyboardAcceleratorTextOverride = command.Shortcut };
                compactItem.Click += (_, _) => { _pendingCompactMenuCommand = command.Id; CompactMenu.Hide(); }; compact.Items.Add(compactItem);
            }
            WorkspaceMenu.Items.Add(menu); CompactMenu.Items.Add(compact);
        }
    }

    private async void Command_Click(object sender, RoutedEventArgs args)
    { if (sender is FrameworkElement { Tag: string id }) await RunCommandAsync(id); }

    private async Task RunCommandAsync(string id)
    {
        if (_dialogVisible || _closing) return;
        try
        {
            var command = _commands.FirstOrDefault(candidate => candidate.Id == id);
            if (command is null) return;
            CommitInspectorEdits();
            if (id != "finishpath") Canvas.CancelGesture();
            await command.Execute();
        }
        catch (Exception exception) { await ShowErrorAsync(exception.Message); }
    }

    private Task CopyAsync(bool cut)
    {
        if (Controller is not { } controller || controller.SelectedElements.Count == 0) return Task.CompletedTask;
        var svg = controller.CopySelection();
        var package = new DataPackage { RequestedOperation = cut ? DataPackageOperation.Move : DataPackageOperation.Copy };
        package.SetText(svg); package.SetData("image/svg+xml", svg); Clipboard.SetContent(package);
        if (cut) controller.DeleteSelection();
        return Task.CompletedTask;
    }

    private async Task PasteAsync()
    {
        if (Controller is not { } controller) return;
        var content = Clipboard.GetContent();
        if (content.Contains("image/svg+xml") && await content.GetDataAsync("image/svg+xml") is string svg) controller.Paste(svg);
        else if (content.Contains(StandardDataFormats.Text))
        {
            var text = await content.GetTextAsync();
            if (text.TrimStart().StartsWith('<')) controller.Paste(text);
            else if (controller.SelectedElements.OfType<SvgTextBase>().Any()) controller.SetText(text);
        }
        else controller.Paste();
    }

    private void ToggleAnimation()
    {
        if (_animationClock.IsRunning) { _animationTimer.Stop(); _animationClock.Stop(); return; }
        _animationClock.Start(); _animationTimer.Start();
    }

    private static bool StudioContainsFocus(DependencyObject? focused, DependencyObject ancestor)
    {
        for (var item = focused; item is not null; item = VisualTreeHelper.GetParent(item))
            if (ReferenceEquals(item, ancestor)) return true;
        return false;
    }

    private static bool StudioIsTextInput(DependencyObject? focused)
    {
        for (var item = focused; item is not null; item = VisualTreeHelper.GetParent(item))
            if (item is TextBox or PasswordBox or NumberBox or RichEditBox or ComboBox) return true;
        return false;
    }

    private async void Workspace_KeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (_closing) { args.Handled = true; return; }
        if (_dialogVisible || args.Handled) return;
        var focused = XamlRoot is { } root ? FocusManager.GetFocusedElement(root) as DependencyObject : null;
        var editingText = StudioIsTextInput(focused) || args.OriginalSource is TextBox;
        var canvasFocus = focused is null || ReferenceEquals(focused, this) || StudioContainsFocus(focused, Canvas);
        var control = IsDown(VirtualKey.Control) || IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows);
        var shift = IsDown(VirtualKey.Shift);
        string? command = null;
        if (control)
        {
            command = args.Key switch
            {
                VirtualKey.N => "new", VirtualKey.O => "open", VirtualKey.S => shift ? "saveas" : "save",
                VirtualKey.E => "export", VirtualKey.P => "print", VirtualKey.K => "commands",
                VirtualKey.Z when !editingText => shift ? "redo" : "undo", VirtualKey.Y when !editingText => "redo",
                VirtualKey.A when !editingText => "selectall", VirtualKey.C when !editingText => "copy",
                VirtualKey.X when !editingText => "cut", VirtualKey.V when !editingText => "paste",
                VirtualKey.D when !editingText => "duplicate", VirtualKey.G when !editingText => shift ? "ungroup" : "group",
                VirtualKey.T when !editingText => "text", VirtualKey.Number0 => "zoom100",
                (VirtualKey)221 when !editingText => shift ? "front" : "forward",
                (VirtualKey)219 when !editingText => shift ? "back" : "backward", _ => null
            };
        }
        else if (!editingText)
        {
            if (args.Key == VirtualKey.Escape && _temporaryStudioPanel is not null)
            {
                ApplySafely(() => { CommitInspectorEdits(); _temporaryStudioPanel = null; RebuildStudioDocks(); ApplyStudioMetrics(); });
                args.Handled = true; return;
            }
            // Tabs, splitters, tree nodes, buttons and numeric editors retain their
            // native navigation keys. Only the drawing surface owns unmodified tools,
            // Space panning, Enter finishing, arrow nudges and Tab canvas focus mode.
            if (!canvasFocus)
            {
                if (args.Key == VirtualKey.F1) command = "help";
                else if (args.Key == VirtualKey.Delete && StudioContainsFocus(focused, DocumentOutline)) command = "delete";
                else return;
            }
            else
            {
                if (args.Key == VirtualKey.Space)
                {
                    if (!_spacePanning)
                    { _spacePanning = true; _spacePreviousPanMode = Canvas.PanMode; Canvas.PanMode = true; }
                    if (_studioHand is not null) _studioHand.IsChecked = true;
                    args.Handled = true; return;
                }
                command = args.Key switch
                {
                    VirtualKey.Delete or VirtualKey.Back => "delete", VirtualKey.Escape => "deselect",
                    VirtualKey.Enter => "finishpath", VirtualKey.F1 => "help", VirtualKey.Tab => shift ? "workspace-hidepanels" : "zen",
                    VirtualKey.X => "swappaints", VirtualKey.D => "defaultpaints",
                    VirtualKey.Number1 when shift => "fit", VirtualKey.Number2 when shift => "fitselection",
                    VirtualKey.Add or (VirtualKey)187 => "zoomin", VirtualKey.Subtract or (VirtualKey)189 => "zoomout", _ => null
                };
                if (command is null && Controller is { } controller)
                {
                    var step = shift ? 10 : 1;
                    try
                    {
                        switch (args.Key)
                        {
                            case VirtualKey.Left: CommitInspectorEdits(); controller.MoveSelection(-step, 0); args.Handled = true; return;
                            case VirtualKey.Right: CommitInspectorEdits(); controller.MoveSelection(step, 0); args.Handled = true; return;
                            case VirtualKey.Up: CommitInspectorEdits(); controller.MoveSelection(0, -step); args.Handled = true; return;
                            case VirtualKey.Down: CommitInspectorEdits(); controller.MoveSelection(0, step); args.Handled = true; return;
                            case VirtualKey.H: Canvas.CancelGesture(); Canvas.PanMode = true; RefreshWorkspace(); args.Handled = true; return;
                        }
                    }
                    catch (Exception exception) { args.Handled = true; await ShowErrorAsync(exception.Message); return; }
                    var tool = args.Key switch
                    {
                        VirtualKey.V => SvgEditorToolKind.Select, VirtualKey.A => SvgEditorToolKind.PathSelect,
                        VirtualKey.R => SvgEditorToolKind.Rect, VirtualKey.E => SvgEditorToolKind.Ellipse,
                        VirtualKey.L => SvgEditorToolKind.Line, VirtualKey.P => SvgEditorToolKind.PathCubic,
                        VirtualKey.B => SvgEditorToolKind.Freehand, VirtualKey.G => SvgEditorToolKind.Polygon,
                        VirtualKey.T => SvgEditorToolKind.Text, VirtualKey.C => SvgEditorToolKind.Circle,
                        VirtualKey.Q => SvgEditorToolKind.PathQuadratic, VirtualKey.O => SvgEditorToolKind.Polyline,
                        _ => (SvgEditorToolKind?)null
                    };
                    if (tool is { } selected) { ApplySafely(() => { CommitInspectorEdits(); SetTool(selected); }); args.Handled = true; }
                }
            }
        }
        if (command is not null) { args.Handled = true; await RunCommandAsync(command); }
    }

    private static bool IsDown(VirtualKey key) => (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) != 0;
    private void Workspace_KeyUp(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key != VirtualKey.Space || !_spacePanning) return;
        _spacePanning = false; Canvas.PanMode = _spacePreviousPanMode;
        if (_studioHand is not null) _studioHand.IsChecked = Canvas.PanMode;
        args.Handled = true;
    }
}
