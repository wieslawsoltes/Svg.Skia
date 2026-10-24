using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Svg.Editor.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Core;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private string? _pendingCompactMenuCommand;

    private void BuildMenus()
    {
        CompactMenu.Closed += (_, _) =>
        {
            var pending = _pendingCompactMenuCommand;
            _pendingCompactMenuCommand = null;
            if (pending is null) return;
            // Wait for the entire submenu chain to close before opening another popup.
            DispatcherQueue.TryEnqueue(() => { if (!_disposed) _ = RunCommandAsync(pending); });
        };
        foreach (var group in _commands.GroupBy(command => command.Group))
        {
            var menu = new MenuBarItem { Title = group.Key };
            var compact = new MenuFlyoutSubItem { Text = group.Key };
            foreach (var command in group)
            {
                var item = new MenuFlyoutItem { Text = command.Label, KeyboardAcceleratorTextOverride = command.Shortcut };
                item.Click += async (_, _) => await RunCommandAsync(command.Id);
                menu.Items.Add(item);
                var compactItem = new MenuFlyoutItem { Text = command.Label, KeyboardAcceleratorTextOverride = command.Shortcut };
                compactItem.Click += (_, _) => { _pendingCompactMenuCommand = command.Id; CompactMenu.Hide(); };
                compact.Items.Add(compactItem);
            }
            WorkspaceMenu.Items.Add(menu);
            CompactMenu.Items.Add(compact);
        }
    }

    private async void Command_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string id })
        {
            _panelFlyout?.Hide();
            await RunCommandAsync(id);
        }
    }

    private async Task RunCommandAsync(string id)
    {
        if (_dialogVisible || _closing) return;
        try
        {
            var command = _commands.FirstOrDefault(candidate => candidate.Id == id);
            if (command is null) return;
            CommitInspectorEdits();
            // Saves, history and unrelated commands must not adopt provisional drawing.
            if (id != "finishpath") Canvas.CancelGesture();
            await command.Execute();
        }
        catch (Exception exception) { await ShowErrorAsync(exception.Message); }
    }

    private async Task CopyAsync(bool cut)
    {
        if (Controller is not { } controller || controller.SelectedElements.Count == 0) return;
        var svg = controller.CopySelection();
        var package = new DataPackage { RequestedOperation = cut ? DataPackageOperation.Move : DataPackageOperation.Copy };
        package.SetText(svg);
        package.SetData("image/svg+xml", svg);
        Clipboard.SetContent(package);
        if (cut) controller.DeleteSelection();
    }

    private async Task PasteAsync()
    {
        if (Controller is not { } controller) return;
        var content = Clipboard.GetContent();
        if (content.Contains("image/svg+xml") && await content.GetDataAsync("image/svg+xml") is string svg)
            controller.Paste(svg);
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
        if (_animationClock.IsRunning)
        {
            _animationTimer.Stop(); _animationClock.Stop(); return;
        }
        _animationClock.Start(); _animationTimer.Start();
    }

    private async void Workspace_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_closing) { e.Handled = true; return; }
        if (_dialogVisible || e.Handled) return;
        var focused = XamlRoot is { } root ? FocusManager.GetFocusedElement(root) : null;
        var editingText = focused is TextBox or PasswordBox or NumberBox || e.OriginalSource is TextBox;
        var control = IsDown(VirtualKey.Control) || IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows);
        var shift = IsDown(VirtualKey.Shift);
        string? command = null;
        if (control)
        {
            command = e.Key switch
            {
                VirtualKey.N => "new",
                VirtualKey.O => "open",
                VirtualKey.S => shift ? "saveas" : "save",
                VirtualKey.E => "export",
                VirtualKey.P => "print",
                VirtualKey.K => "commands",
                VirtualKey.Z when !editingText => shift ? "redo" : "undo",
                VirtualKey.Y when !editingText => "redo",
                VirtualKey.A when !editingText => "selectall",
                VirtualKey.C when !editingText => "copy",
                VirtualKey.X when !editingText => "cut",
                VirtualKey.V when !editingText => "paste",
                VirtualKey.D when !editingText => "duplicate",
                VirtualKey.G when !editingText => shift ? "ungroup" : "group",
                VirtualKey.T when !editingText => "text",
                VirtualKey.Number0 => "zoom100",
                (VirtualKey)221 when !editingText => shift ? "front" : "forward",
                (VirtualKey)219 when !editingText => shift ? "back" : "backward",
                _ => null
            };
        }
        else if (!editingText)
        {
            if (e.Key == VirtualKey.Space)
            {
                if (!_spacePanning) { _spacePanning = true; Canvas.PanMode = true; }
                e.Handled = true; return;
            }
            command = e.Key switch
            {
                VirtualKey.Delete or VirtualKey.Back => "delete",
                VirtualKey.Escape => "deselect",
                VirtualKey.Enter => "finishpath",
                VirtualKey.F1 => "help",
                VirtualKey.Tab => "zen",
                VirtualKey.Number1 when shift => "fit",
                VirtualKey.Number2 when shift => "fitselection",
                VirtualKey.Add or (VirtualKey)187 => "zoomin",
                VirtualKey.Subtract or (VirtualKey)189 => "zoomout",
                _ => null
            };
            if (command is null && Controller is { } controller)
            {
                var step = shift ? 10 : 1;
                try
                {
                    switch (e.Key)
                    {
                        case VirtualKey.Left: controller.MoveSelection(-step, 0); e.Handled = true; return;
                        case VirtualKey.Right: controller.MoveSelection(step, 0); e.Handled = true; return;
                        case VirtualKey.Up: controller.MoveSelection(0, -step); e.Handled = true; return;
                        case VirtualKey.Down: controller.MoveSelection(0, step); e.Handled = true; return;
                        case VirtualKey.H: Canvas.PanMode = true; e.Handled = true; return;
                    }
                }
                catch (Exception exception)
                {
                    e.Handled = true;
                    await ShowErrorAsync(exception.Message);
                    return;
                }
                var tool = e.Key switch
                {
                    VirtualKey.V => SvgEditorToolKind.Select,
                    VirtualKey.A => SvgEditorToolKind.PathSelect,
                    VirtualKey.R => SvgEditorToolKind.Rect,
                    VirtualKey.E => SvgEditorToolKind.Ellipse,
                    VirtualKey.L => SvgEditorToolKind.Line,
                    VirtualKey.P => SvgEditorToolKind.PathCubic,
                    VirtualKey.B => SvgEditorToolKind.Freehand,
                    VirtualKey.G => SvgEditorToolKind.Polygon,
                    VirtualKey.T => SvgEditorToolKind.Text,
                    VirtualKey.C => SvgEditorToolKind.Circle,
                    VirtualKey.Q => SvgEditorToolKind.PathQuadratic,
                    VirtualKey.O => SvgEditorToolKind.Polyline,
                    _ => (SvgEditorToolKind?)null
                };
                if (tool is { } selected) { SetTool(selected); e.Handled = true; }
            }
        }
        if (command is not null) { e.Handled = true; await RunCommandAsync(command); }
    }

    private static bool IsDown(VirtualKey key)
        => (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) != 0;

    private void Workspace_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Space || !_spacePanning) return;
        _spacePanning = false; Canvas.PanMode = false; e.Handled = true;
    }
}
