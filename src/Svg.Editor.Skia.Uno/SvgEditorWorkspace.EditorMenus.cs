using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private sealed record EditorMenuNode(string Title, string? Command = null, params EditorMenuNode[] Children);
    private readonly List<(MenuFlyoutItem Item, EditorCommand Command)> _editorMenuItems = new();

    private static EditorMenuNode Item(string id, string title = "") => new(title, id);
    private static EditorMenuNode Menu(string title, params EditorMenuNode[] items) => new(title, null, items);
    private static EditorMenuNode Separator() => new("-");

    private void BuildEditorMenus()
    {
        var menus = new[]
        {
            Menu("File", Item("new", "New…"), Item("open", "Open…"), Separator(),
                Item("close", "Close"), Item("save", "Save"), Item("saveas", "Save As…"),
                Item("savecompressed", "Save Compressed SVG…"), Separator(), Item("image", "Place…"),
                Menu("Export", Item("export", "Export As…"), Item("exportselection", "Export Selection…")),
                Separator(), Item("artboard", "Document Setup…"), Item("preflight", "Document Preflight…"),
                Item("source", "Edit SVG Source…"), Separator(), Item("print", "Print…")),
            Menu("Edit", Item("undo"), Item("redo"), Separator(), Item("cut"), Item("copy"), Item("paste"),
                Item("pasteinfront", "Paste in Front"), Item("pastebehind", "Paste in Back"), Item("pasteinplace", "Paste in Place"),
                Item("duplicate", "Duplicate"), Item("delete", "Clear"), Separator(),
                Menu("Edit Colors", Item("swappaints", "Swap Fill and Stroke"), Item("defaultpaints", "Default Fill and Stroke"),
                    Item("nonepaint", "None"), Item("colormixer", "Color Mixer")),
                Separator(), Item("shortcutsettings", "Keyboard Shortcuts…"), Item("preferences", "Preferences…")),
            Menu("Object", Menu("Transform", Item("transformagain", "Transform Again"), Item("transform", "Transform…"),
                    Item("rotate", "Rotate 90°"), Item("fliph", "Reflect Horizontal"), Item("flipv", "Reflect Vertical")),
                Menu("Arrange", Item("front", "Bring to Front"), Item("forward", "Bring Forward"),
                    Item("backward", "Send Backward"), Item("back", "Send to Back"), Separator(),
                    Item("movetolayer", "Move to Layer…"), Item("moveout", "Move Out of Group")),
                Separator(), Item("group"), Item("ungroup"),
                Menu("Lock", Item("lock", "Selection")), Item("unlockall", "Unlock All"),
                Menu("Hide", Item("hide", "Selection")), Item("showall", "Show All"), Separator(),
                Menu("Path", Item("convertpath", "Convert to Paths"), Item("simplify", "Simplify"), Item("offset", "Offset Path…"),
                    Item("strokeprofile", "Width Profile…"), Separator(), Item("smooth", "Smooth Anchor"),
                    Item("corner", "Corner Anchor"), Item("finishpath", "Finish Path")),
                Menu("Pathfinder", Item("union", "Unite"), Item("subtract", "Minus Front"), Item("intersect"),
                    Item("exclude"), Item("divide", "Divide…")),
                Menu("Blend", Item("blend", "Make Blend…")),
                Menu("Clipping Mask", Item("clip", "Make"), Item("unclip", "Release")),
                Menu("Opacity Mask", Item("alphamask", "Make Alpha Mask"), Item("luminancemask", "Make Luminance Mask"),
                    Item("releasemask", "Release"), Item("disablemask", "Disable")),
                Separator(), Item("mesh", "Create Gradient Mesh…"), Item("pattern", "Create Pattern…"),
                Item("symbol", "Create Symbol…"), Item("bakeframe", "Bake Animation Frame")),
            Menu("Type", Item("text", "Edit Text…"), Item("textframes", "Area Type and Linked Frames…"), Separator(),
                Item("panel-typography", "Character"), Item("panel-paragraph", "Paragraph"),
                Menu("Paragraph Alignment", Item("text-align-start", "Align Left"), Item("text-align-middle", "Align Center"),
                    Item("text-align-end", "Align Right")), Separator(), Item("selecttext", "Select Text Objects")),
            Menu("Select", Item("selectall", "All"), Item("deselect", "Deselect"), Item("selectinverse", "Inverse"),
                Menu("Same", Item("samefill", "Fill Color"), Item("samestroke", "Stroke Color"),
                    Item("samestrokeweight", "Stroke Weight"), Item("sameopacity", "Opacity"),
                    Item("sameappearance", "Appearance"), Item("sametype", "Object Type")),
                Menu("Object", Item("selecttext", "Text Objects"), Item("selectimages", "Images"), Item("selectpaths", "Paths"))),
            Menu("Effect", Item("filterstudio", "Filter Studio…"), Item("effect", "Drop Shadow…"),
                Item("removefilters", "Clear Effects"), Separator(), Item("filters", "Render Effects")),
            Menu("View", Item("wireframe", "Outline"), Item("transparency", "Transparency Grid"), Separator(),
                Item("zoomin", "Zoom In"), Item("zoomout", "Zoom Out"), Item("fit", "Fit Artboard in Window"),
                Item("fitall", "Fit All in Window"), Item("fitselection", "Fit Selection in Window"), Item("zoom100", "Actual Size"),
                Separator(), Menu("Rulers", Item("workspace-rulers", "Show Rulers")), Item("grid", "Show Grid"),
                Item("snapgrid", "Snap to Grid"), Separator(), Item("zen", "Hide All Panels"),
                Item("workspace-hidepanels", "Hide Side Panels"), Item("screenmode", "Change Screen Mode"),
                Separator(), Menu("Interface", Item("theme-dark", "Dark"), Item("theme-light", "Light"),
                    Item("theme-system", "Use System Setting"), Item("workspace-density", "Larger Tool Targets"))),
            Menu("Window", Menu("Workspace", Item("workspace-essentials", "Essentials"), Item("workspace-drawing", "Drawing"),
                    Item("workspace-typography", "Typography"), Separator(), Item("workspace-reset", "Reset Workspace"),
                    Item("workspace-save", "New Workspace…"), Item("workspace-restore-saved", "Restore Saved Workspace"),
                    Item("workspace-manage", "Manage Workspaces…"), Separator(), Item("workspace-import", "Import Workspaces…"),
                    Item("workspace-export", "Export Workspaces…")),
                Item("controlbar", "Control"), Item("toolbar", "Tools"), Separator(),
                Item("panel-properties", "Properties"), Item("panel-layers", "Layers"), Item("panel-appearance", "Appearance"),
                Item("panel-transform", "Transform"), Item("panel-align", "Align"), Item("panel-geometry", "Pathfinder"),
                Item("panel-color", "Color"), Item("panel-swatches", "Swatches"), Item("panel-gradient", "Gradient"),
                Item("panel-stroke", "Stroke"), Item("panel-brushes", "Brushes"), Item("panel-symbols", "Symbols"),
                Item("panel-transparency", "Transparency"), Item("panel-typography", "Character"), Item("panel-paragraph", "Paragraph"),
                Item("panel-artboards", "Artboards"), Item("panel-links", "Links"),
                Item("panel-resources", "Document Resources"), Item("panel-effects", "Effects"), Item("panel-attributes", "SVG Attributes"),
                Separator(), Item("timeline", "Animation Timeline…")),
            Menu("Help", Item("help", "Tools and Shortcuts…"), Item("commands", "Search Commands…"),
                Item("control-gallery", "EditorTheme Control Gallery…"), Item("about", "About UnoDraw…"))
        };

        WorkspaceMenu.Items.Clear();
        CompactMenu.Items.Clear();
        _editorMenuItems.Clear();
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in menus)
        {
            var menu = new MenuBarItem
            {
                Title = definition.Title,
                AccessKey = definition.Title == "Effect" ? "C" : definition.Title[..1]
            };
            AutomationProperties.SetAutomationId(menu, "editor-menu-" + definition.Title.ToLowerInvariant());
            var compact = new MenuFlyoutSubItem { Text = definition.Title };
            AppendEditorMenu(menu.Items, definition.Children, used, false);
            AppendEditorMenu(compact.Items, definition.Children, used, true);
            WorkspaceMenu.Items.Add(menu);
            CompactMenu.Items.Add(compact);
        }

        // Extension commands remain reachable without flattening the core menus.
        var other = _commands.Where(command => !used.Contains(command.Id) && command.Id != "cancel-action"
            && !command.Id.StartsWith("nudge-", StringComparison.Ordinal)).ToArray();
        if (other.Length > 0)
        {
            var extensions = other.GroupBy(command => command.Group).Select(group =>
                Menu(group.Key, group.Select(command => Item(command.Id)).ToArray())).ToArray();
            var desktop = new MenuFlyoutSubItem { Text = "More Commands" };
            var compact = new MenuFlyoutSubItem { Text = "More Commands" };
            AppendEditorMenu(desktop.Items, extensions, used, false);
            AppendEditorMenu(compact.Items, extensions, used, true);
            WorkspaceMenu.Items[^1].Items.Add(desktop);
            ((MenuFlyoutSubItem)CompactMenu.Items[^1]).Items.Add(compact);
        }

        CompactMenu.Opening += (_, _) => RefreshEditorMenus();
        CompactMenu.Closed += (_, _) =>
        {
            var pending = _pendingCompactMenuCommand;
            _pendingCompactMenuCommand = null;
            if (pending is not null)
                EnqueueEditorMenuCommand(pending);
        };
        var context = new MenuFlyout();
        AppendEditorMenu(context.Items, new[]
        {
            Item("undo"), Item("redo"), Separator(), Item("cut"), Item("copy"), Item("pasteinplace", "Paste in Place"),
            Separator(), Item("group"), Item("ungroup"), Item("clip", "Make Clipping Mask"),
            Menu("Arrange", Item("front"), Item("forward"), Item("backward"), Item("back")),
            Item("transform", "Transform…"), Separator(), Item("panel-properties", "Properties"), Item("deselect", "Deselect")
        }, used, false);
        context.Opening += (_, _) => RefreshEditorMenus();
        Canvas.ContextFlyout = context;
        RefreshEditorMenus();
    }

    private void AppendEditorMenu(IList<MenuFlyoutItemBase> target, IEnumerable<EditorMenuNode> nodes, HashSet<string> used, bool compact)
    {
        foreach (var node in nodes)
        {
            if (node.Title == "-")
            {
                if (target.Count > 0 && target[^1] is not MenuFlyoutSeparator)
                    target.Add(new MenuFlyoutSeparator());
                continue;
            }
            if (node.Command is null)
            {
                var submenu = new MenuFlyoutSubItem { Text = node.Title };
                AppendEditorMenu(submenu.Items, node.Children, used, compact);
                if (submenu.Items.Count > 0)
                    target.Add(submenu);
                continue;
            }
            if (_commands.FirstOrDefault(command => command.Id == node.Command) is not { } action)
                continue;
            used.Add(action.Id);
            MenuFlyoutItem item = action.IsChecked is null ? new MenuFlyoutItem() : new ToggleMenuFlyoutItem();
            item.Text = string.IsNullOrWhiteSpace(node.Title) ? action.Label : node.Title;
            item.KeyboardAcceleratorTextOverride = action.Shortcut;
            AutomationProperties.SetAutomationId(item, "editor-command-" + action.Id);
            item.Click += (_, _) =>
            {
                if (compact)
                {
                    _pendingCompactMenuCommand = action.Id;
                    CompactMenu.Hide();
                }
                else
                    EnqueueEditorMenuCommand(action.Id);
            };
            _editorMenuItems.Add((item, action));
            target.Add(item);
        }
        if (target.Count > 0 && target[^1] is MenuFlyoutSeparator)
            target.RemoveAt(target.Count - 1);
    }

    private void EnqueueEditorMenuCommand(string id)
        => DispatcherQueue.TryEnqueue(async () => { if (!_disposed) await RunCommandAsync(id); });

    private void RefreshEditorMenus()
    {
        foreach (var (item, command) in _editorMenuItems)
        {
            item.IsEnabled = !_closing && (command.CanExecute?.Invoke() ?? true);
            item.KeyboardAcceleratorTextOverride = command.Shortcut;
            if (item is ToggleMenuFlyoutItem toggle)
                toggle.IsChecked = command.IsChecked?.Invoke() == true;
        }
    }
}
