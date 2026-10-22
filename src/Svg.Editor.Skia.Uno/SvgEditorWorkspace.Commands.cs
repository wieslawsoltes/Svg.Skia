using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using Svg.Editor.Core;
using Svg.Editor.Skia;
using Svg.Editor.Svg;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Core;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private void BuildCommands()
    {
        void Add(string id, string label, string group, Action execute, string shortcut = "")
            => _commands.Add(new EditorCommand(id, label, group, shortcut, () => { execute(); return Task.CompletedTask; }));
        void AddAsync(string id, string label, string group, Func<Task> execute, string shortcut = "")
            => _commands.Add(new EditorCommand(id, label, group, shortcut, execute));

        AddAsync("new", "New document…", "File", NewDocumentDialogAsync, "Ctrl+N");
        AddAsync("open", "Open SVG…", "File", OpenPickerAsync, "Ctrl+O");
        AddAsync("save", "Save", "File", async () => { if (_current is { } d) await SaveAsync(d); }, "Ctrl+S");
        AddAsync("saveas", "Save as…", "File", async () => { if (_current is { } d) await SaveAsync(d, true); }, "Ctrl+Shift+S");
        AddAsync("savecompressed", "Save compressed SVG…", "File", async () => { if (_current is { } d) await SaveAsync(d, true, ".svgz"); });
        AddAsync("export", "Export image or PDF…", "File", ExportAsync, "Ctrl+E");
        AddAsync("exportselection", "Export selection…", "File", () => ExportAsync(true));
        AddAsync("print", "Print and page setup…", "File", PrintDialogAsync, "Ctrl+P");
        AddAsync("preflight", "Preflight document…", "File", PreflightDialogAsync);
        AddAsync("image", "Place image…", "File", PlaceImageAsync);
        AddAsync("source", "Edit SVG source…", "File", EditSourceAsync);

        Add("undo", "Undo", "Edit", () => Controller?.Undo(), "Ctrl+Z");
        Add("redo", "Redo", "Edit", () => Controller?.Redo(), "Ctrl+Shift+Z");
        AddAsync("cut", "Cut", "Edit", () => CopyAsync(true), "Ctrl+X");
        AddAsync("copy", "Copy", "Edit", () => CopyAsync(false), "Ctrl+C");
        AddAsync("paste", "Paste", "Edit", PasteAsync, "Ctrl+V");
        Add("duplicate", "Duplicate", "Edit", () => Controller?.DuplicateSelection(), "Ctrl+D");
        Add("delete", "Delete", "Edit", () =>
        {
            if (Canvas.Tool == SvgEditorToolKind.PathSelect && Canvas.ActiveNodeIndex >= 0)
                Canvas.DeleteActiveNode();
            else
                Controller?.DeleteSelection();
        }, "Delete");
        Add("selectall", "Select all", "Edit", () => Controller?.SelectAll(), "Ctrl+A");
        Add("deselect", "Clear selection", "Edit", () => { Canvas.CancelGesture(); Controller?.ClearSelection(); }, "Esc");
        AddAsync("text", "Edit text…", "Edit", EditTextAsync, "Ctrl+T");
        AddAsync("textframes", "Paragraphs and linked text frames…", "Edit", TextFramesDialogAsync);
        AddAsync("insert", "Insert SVG element…", "Edit", InsertElementAsync);

        Add("group", "Group", "Object", () => Controller?.GroupSelection(), "Ctrl+G");
        Add("ungroup", "Ungroup", "Object", () => Controller?.UngroupSelection(), "Ctrl+Shift+G");
        Add("forward", "Bring forward", "Object", () => Controller?.BringForward(), "Ctrl+]");
        Add("backward", "Send backward", "Object", () => Controller?.SendBackward(), "Ctrl+[");
        Add("front", "Bring to front", "Object", () => Controller?.BringToFront());
        Add("back", "Send to back", "Object", () => Controller?.SendToBack());
        Add("fliph", "Flip horizontal", "Object", () => Controller?.FlipHorizontal());
        Add("flipv", "Flip vertical", "Object", () => Controller?.FlipVertical());
        Add("rotate", "Rotate 90°", "Object", () => Controller?.RotateSelection(90));
        AddAsync("transform", "Transform precisely…", "Object", TransformDialogAsync);
        Add("lock", "Lock selection", "Object", () => Controller?.SetLocked(true));
        Add("unlock", "Unlock selection", "Object", () => Controller?.SetLocked(false));
        Add("hide", "Hide selection", "Object", () => Controller?.SetVisibility(false));
        Add("show", "Show selection", "Object", () => Controller?.SetVisibility(true));
        Add("layer", "New layer", "Object", CreateLayer);
        AddAsync("movetolayer", "Move to layer…", "Object", MoveToLayerDialogAsync);
        Add("moveout", "Move out of group", "Object", MoveSelectionOutOfGroup);
        AddAsync("artboard", "Artboard size…", "Object", ArtboardDialogAsync);
        AddAsync("gradient", "Gradient fill…", "Object", GradientDialogAsync);
        AddAsync("mesh", "Gradient mesh…", "Object", GradientMeshDialogAsync);
        AddAsync("swatches", "Document swatches…", "Object", SwatchesDialogAsync);
        AddAsync("pattern", "Pattern fill…", "Object", PatternDialogAsync);
        AddAsync("symbol", "Create symbol…", "Object", CreateSymbolAsync);
        AddAsync("style", "Edit document CSS…", "Object", EditStylesAsync);
        Add("clip", "Create clipping mask", "Object", CreateClippingMask);
        Add("unclip", "Release clipping mask", "Object", () => Controller?.SetAttribute("clip-path", "none"));
        AddAsync("effect", "Drop shadow…", "Object", ShadowDialogAsync);

        Add("union", "Unite", "Path", () => Controller?.ApplyBoolean(SKPathOp.Union));
        Add("subtract", "Subtract", "Path", () => Controller?.ApplyBoolean(SKPathOp.Difference));
        Add("intersect", "Intersect", "Path", () => Controller?.ApplyBoolean(SKPathOp.Intersect));
        Add("exclude", "Exclude", "Path", () => Controller?.ApplyBoolean(SKPathOp.Xor));
        Add("convertpath", "Convert to paths", "Path", () => Controller?.ConvertSelectionToPaths());
        Add("simplify", "Simplify path", "Path", () => Controller?.SimplifySelection());
        AddAsync("offset", "Offset path…", "Path", OffsetPathAsync);
        AddAsync("strokeprofile", "Variable width stroke…", "Path", StrokeProfileDialogAsync);
        AddAsync("brushes", "Brush library…", "Path", BrushLibraryDialogAsync);
        AddAsync("blend", "Blend paths…", "Path", BlendPathsAsync);
        Add("smooth", "Smooth active node", "Path", () => EditNode(true));
        Add("corner", "Corner active node", "Path", () => EditNode(false));
        Add("finishpath", "Finish path", "Path", Canvas.FinishPath, "Enter");

        foreach (var alignment in Enum.GetValues<AlignService.AlignType>())
            Add($"align{alignment}", $"Align {alignment}", "Arrange", () => Controller?.Align(alignment));
        foreach (var direction in Enum.GetValues<AlignService.DistributeType>())
            Add($"distribute{direction}", $"Distribute {direction}", "Arrange", () => Controller?.Distribute(direction));

        Add("fit", "Fit artboard", "View", Canvas.FitDocument, "Shift+1");
        Add("layers", "Layers and objects…", "View", () => ShowWorkspacePanel(OutlinePanel, "Layers and objects"));
        Add("inspector", "Object inspector…", "View", () => ShowWorkspacePanel(InspectorPanel, "Object inspector"));
        Add("fitselection", "Fit selection", "View", Canvas.FitSelection, "Shift+2");
        Add("zoomin", "Zoom in", "View", Canvas.ZoomIn, "+");
        Add("zoomout", "Zoom out", "View", Canvas.ZoomOut, "−");
        Add("zoom100", "Actual size", "View", Canvas.ResetZoom, "Ctrl+0");
        Add("grid", "Toggle grid", "View", () =>
        {
            Canvas.ShowGrid = !Canvas.ShowGrid;
            if (Controller is { } c)
                c.Session.Settings.ShowGrid = Canvas.ShowGrid;
        });
        Add("transparency", "Toggle transparency grid", "View", () => Canvas.ShowTransparency = !Canvas.ShowTransparency);
        Add("wireframe", "Toggle outlines", "View", () =>
        {
            if (Controller is not { } c)
                return;
            c.Renderer.Wireframe = !c.Renderer.Wireframe;
            Canvas.Invalidate();
        });
        Add("filters", "Toggle filter rendering", "View", () =>
        {
            if (Controller is not { } c)
                return;
            c.Session.Settings.FiltersDisabled = !c.Session.Settings.FiltersDisabled;
            c.Renderer.IgnoreAttributes = c.Session.Settings.FiltersDisabled ? global::Svg.Model.DrawAttributes.Filter : global::Svg.Model.DrawAttributes.None;
            c.Refresh();
        });
        Add("animate", "Play / pause SVG animation", "View", ToggleAnimation);
        AddAsync("timeline", "Animation timeline…", "View", ShowTimelineAsync);
        Add("bakeframe", "Bake current animation frame", "Object", BakeAnimationFrame);
        Add("theme", "Switch theme", "View", () =>
        {
            RequestedTheme = ActualTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
            Canvas.IsDarkTheme = RequestedTheme == ElementTheme.Dark;
        });
        Add("zen", "Focus canvas", "View", () => { _zenMode = !_zenMode; UpdateResponsiveLayout(); }, "Tab");
        AddAsync("commands", "Search commands…", "View", ShowCommandsAsync, "Ctrl+K");
        AddAsync("help", "Keyboard shortcuts…", "View", ShowHelpAsync, "F1");
    }

    private void BuildMenus()
    {
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
                compactItem.Click += async (_, _) => await RunCommandAsync(command.Id);
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
        if (_dialogVisible || _closing)
            return;
        try
        {
            var command = _commands.FirstOrDefault(candidate => candidate.Id == id);
            if (command is null)
                return;
            CommitInspectorEdits();
            // A command is a gesture boundary: a half-drawn object must never leak
            // into a save, undo, document switch or unrelated document command.
            if (id != "finishpath")
                Canvas.CancelGesture();
            await command.Execute();
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(exception.Message);
        }
    }

    private async Task CopyAsync(bool cut)
    {
        if (Controller is not { } controller || controller.SelectedElements.Count == 0)
            return;
        var svg = controller.CopySelection();
        var package = new DataPackage { RequestedOperation = cut ? DataPackageOperation.Move : DataPackageOperation.Copy };
        package.SetText(svg);
        package.SetData("image/svg+xml", svg);
        Clipboard.SetContent(package);
        if (cut)
            controller.DeleteSelection();
    }

    private async Task PasteAsync()
    {
        if (Controller is not { } controller)
            return;
        var content = Clipboard.GetContent();
        if (content.Contains("image/svg+xml") && await content.GetDataAsync("image/svg+xml") is string svg)
            controller.Paste(svg);
        else if (content.Contains(StandardDataFormats.Text))
        {
            var text = await content.GetTextAsync();
            if (text.TrimStart().StartsWith('<'))
                controller.Paste(text);
            else if (controller.SelectedElements.OfType<SvgTextBase>().Any())
                controller.SetText(text);
        }
        else
            controller.Paste();
    }

    private void ToggleAnimation()
    {
        if (_animationTimer.IsEnabled)
        {
            _animationTimer.Stop();
            _animationClock.Stop();
            return;
        }
        _animationClock.Start();
        _animationTimer.Start();
    }

    private async void Workspace_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_closing)
        {
            e.Handled = true;
            return;
        }
        if (_dialogVisible || e.Handled)
            return;
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
                if (!_spacePanning)
                { _spacePanning = true; Canvas.PanMode = true; }
                e.Handled = true;
                return;
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
                switch (e.Key)
                {
                    case VirtualKey.Left:
                        controller.MoveSelection(-step, 0);
                        e.Handled = true;
                        return;
                    case VirtualKey.Right:
                        controller.MoveSelection(step, 0);
                        e.Handled = true;
                        return;
                    case VirtualKey.Up:
                        controller.MoveSelection(0, -step);
                        e.Handled = true;
                        return;
                    case VirtualKey.Down:
                        controller.MoveSelection(0, step);
                        e.Handled = true;
                        return;
                    case VirtualKey.H:
                        Canvas.PanMode = true;
                        e.Handled = true;
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
                if (tool is { } selected)
                { SetTool(selected); e.Handled = true; }
            }
        }
        if (command is not null)
        {
            e.Handled = true;
            await RunCommandAsync(command);
        }
    }

    private static bool IsDown(VirtualKey key) => (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) != 0;

    private void Workspace_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Space || !_spacePanning)
            return;
        _spacePanning = false;
        Canvas.PanMode = false;
        e.Handled = true;
    }
}
