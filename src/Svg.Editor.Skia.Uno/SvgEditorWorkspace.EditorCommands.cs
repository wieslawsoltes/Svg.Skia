using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Svg.Editor.Skia;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private double _editorKeyboardIncrement = 1;
    private double EditorKeyboardIncrement => _editorKeyboardIncrement;
    private bool _editorControlBarVisible = true;
    private bool _editorToolbarVisible = true;
    private bool _editorSingleToolColumn;
    private int _editorScreenMode;

    private void RefreshEditorShellShortcuts()
    {
        CommandsShortcut.Text = EditorShortcutText("commands");
        ToolTipService.SetToolTip(EditorCommandSearchButton, "Search commands (" + EditorShortcutText("commands") + ")");
    }

    private void BuildEditorCommands()
    {
        ReadEditorPreferences();
        InitializeEditorZoom();
        BuildEditorUtilityCommands();
        BuildEditorGuideCommands();
        void Add(string id, string title, string group, Action action)
            => _commands.Add(new EditorCommand(id, title, group, string.Empty, () => { action(); return Task.CompletedTask; }));
        void Async(string id, string title, string group, Func<Task> action)
            => _commands.Add(new EditorCommand(id, title, group, string.Empty, action));

        Async("close", "Close document", "File", () => DocumentTabs.SelectedItem is TabViewItem tab ? CloseEditorDocumentAsync(tab) : Task.CompletedTask);
        Async("pasteinplace", "Paste in place", "Edit", () => PasteEditorAsync(SvgPastePlacement.InPlace));
        Async("pasteinfront", "Paste in front", "Edit", () => PasteEditorAsync(SvgPastePlacement.InFront));
        Async("pastebehind", "Paste in back", "Edit", () => PasteEditorAsync(SvgPastePlacement.Behind));
        Add("transformagain", "Transform again", "Object", () => Controller?.RepeatTransform());
        Add("unlockall", "Unlock all", "Object", () => Controller?.UnlockAll());
        Add("showall", "Show all", "Object", () => Controller?.ShowAll());
        Add("cancel-action", "Cancel drawing or screen mode", "View", () =>
        {
            Canvas.CancelGesture();
            if (_editorScreenMode != 0)
            {
                _editorScreenMode = 0;
                if (HostWindow is { } window && !OperatingSystem.IsBrowser())
                    window.AppWindow.SetPresenter(AppWindowPresenterKind.Default);
                ApplyStudioMetrics();
            }
            else
                Controller?.ClearSelection();
        });
        Add("selectinverse", "Inverse selection", "Select", () => Controller?.InvertSelection());
        foreach (var (id, label, match) in new[]
        {
            ("samefill", "Same fill color", SvgSelectionMatch.Fill), ("samestroke", "Same stroke color", SvgSelectionMatch.Stroke),
            ("samestrokeweight", "Same stroke weight", SvgSelectionMatch.StrokeWidth), ("sameopacity", "Same opacity", SvgSelectionMatch.Opacity),
            ("sameappearance", "Same appearance", SvgSelectionMatch.Appearance), ("sametype", "Same object type", SvgSelectionMatch.ObjectType)
        })
            Add(id, label, "Select", () => Controller?.SelectMatching(match));
        Add("selecttext", "Select text objects", "Select", () => Controller?.SelectObjectsOfType(typeof(SvgTextBase)));
        Add("selectimages", "Select images", "Select", () => Controller?.SelectObjectsOfType(typeof(SvgImage)));
        Add("selectpaths", "Select paths", "Select", () => Controller?.SelectObjectsOfType(typeof(SvgPath)));
        foreach (var (value, label) in new[] { ("start", "Align text left"), ("middle", "Align text center"), ("end", "Align text right") })
            Add("text-align-" + value, label, "Type", () => Controller?.SetAttribute("text-anchor", value));
        Add("fitall", "Fit all artboards", "View", Canvas.FitDocument);
        Add("snapgrid", "Snap to grid", "View", ToggleEditorSnap);
        Add("screenmode", "Change screen mode", "View", CycleEditorScreenMode);
        Add("controlbar", "Control bar", "Window", () => { _editorControlBarVisible = !_editorControlBarVisible; ApplyStudioMetrics(); SaveEditorPreferences(); });
        Add("toolbar", "Tools panel", "Window", () => { _editorToolbarVisible = !_editorToolbarVisible; ApplyStudioMetrics(); SaveEditorPreferences(); });
        Add("toolbar-columns", "Switch tool columns", "Window", () => { _editorSingleToolColumn = !_editorSingleToolColumn; ApplyStudioMetrics(); SaveEditorPreferences(); });
        Async("preferences", "Preferences…", "Edit", ShowEditorPreferencesAsync);
        Async("shortcutsettings", "Keyboard shortcuts…", "Edit", ShowEditorShortcutSettingsAsync);
        Async("about", "About UnoDraw…", "Help", () => ShowDialogAsync(MakeDialog("UnoDraw", new TextBlock
        {
            Text = "Vector editing with Uno Platform and Svg.Skia.\n\nEditorTheme provides the shared controls, workspace chrome and keyboard workflows.\n\nSVG document editing, paint resources, paths, filters and animation use the existing editor libraries.",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 460
        }, string.Empty, "Close")));
        ConfigureEditorCommandStates();
    }

    private void ConfigureEditorCommandStates()
    {
        bool Document() => Controller?.Document is not null;
        bool Selected() => Controller?.SelectedElements.Count > 0;
        bool Editable(int count = 1) => Controller is { } controller && controller.SelectedElements.Count >= count
            && !controller.SelectedElements.Any(SvgEditorController.IsEditLocked);
        foreach (var command in _commands)
        {
            if (command.CanExecute is not null)
                continue;
            command.CanExecute = command.Id switch
            {
                "new" or "open" or "help" or "about" or "preferences" or "shortcutsettings" or "commands" or "control-gallery" => () => true,
                "undo" => () => Controller?.CanUndo == true,
                "redo" => () => Controller?.CanRedo == true,
                "copy" or "exportselection" or "fitselection" => Selected,
                "transformagain" => () => Controller?.CanRepeatTransform == true && Editable(),
                "union" or "subtract" or "intersect" or "exclude" or "divide" or "clip" or "alphamask" or "luminancemask" or "blend" => () => Editable(2),
                "ungroup" => () => Editable() && Controller!.SelectedElements.Any(element => element is SvgGroup),
                "text" or "text-align-start" or "text-align-middle" or "text-align-end" => () => Editable() && Controller!.SelectedElements.All(element => element is SvgTextBase),
                "smooth" or "corner" => () => Editable() && Canvas.ActiveNodeIndex >= 0,
                "cut" or "delete" or "duplicate" or "group" or "front" or "back" or "forward" or "backward"
                    or "fliph" or "flipv" or "rotate" or "transform" or "lock" or "hide" or "show" or "movetolayer" or "moveout"
                    or "convertpath" or "simplify" or "offset" or "strokeprofile" or "symbol" or "unclip" or "mesh"
                    or "pattern" or "releasemask" or "disablemask" or "effect" or "filterstudio" or "removefilters" => () => Editable(),
                _ when command.Id.StartsWith("same", StringComparison.Ordinal) => Selected,
                _ when command.Id.StartsWith("align", StringComparison.Ordinal) => () => Editable(2),
                _ when command.Id.StartsWith("distribute", StringComparison.Ordinal) => () => Editable(3),
                _ when command.Id.StartsWith("theme", StringComparison.Ordinal) || command.Id.StartsWith("workspace", StringComparison.Ordinal)
                    || command.Id.StartsWith("panel-", StringComparison.Ordinal) || command.Id is "toolbar" or "controlbar" or "screenmode" => () => true,
                _ => Document
            };
            command.IsChecked = command.Id switch
            {
                "grid" => () => Canvas.ShowGrid,
                "transparency" => () => Canvas.ShowTransparency,
                "wireframe" => () => Controller?.Renderer.Wireframe == true,
                "filters" => () => Controller?.Session.Settings.FiltersDisabled == false,
                "snapgrid" => () => Controller?.Session.Settings.SnapToGrid == true,
                "workspace-rulers" => () => _studioLayout.ShowRulers,
                "workspace-density" => () => _studioLayout.TouchDensity,
                "workspace-hidepanels" => () => _studioLayout.PanelsHidden,
                "zen" => () => _zenMode,
                "controlbar" => () => _editorControlBarVisible,
                "toolbar" => () => _editorToolbarVisible,
                "theme-dark" => () => RequestedTheme == ElementTheme.Dark,
                "theme-light" => () => RequestedTheme == ElementTheme.Light,
                "theme-system" => () => RequestedTheme == ElementTheme.Default,
                _ when command.Id.StartsWith("panel-", StringComparison.Ordinal) => () => !_studioLayout.PanelsHidden
                    && !_zenMode && _studioLayout.Find(command.Id[6..]).Dock != SvgPanelDock.Hidden,
                _ => command.IsChecked
            };
        }
    }

    private async Task PasteEditorAsync(SvgPastePlacement placement)
    {
        if (Controller is not { } controller)
            return;
        var content = Clipboard.GetContent();
        string? svg = null;
        if (content.Contains("image/svg+xml"))
            svg = await content.GetDataAsync("image/svg+xml") as string;
        else if (content.Contains(StandardDataFormats.Text))
        {
            var text = await content.GetTextAsync();
            if (text.TrimStart().StartsWith('<'))
                svg = text;
            else
                throw new InvalidOperationException("The clipboard does not contain SVG artwork.");
        }
        // Clipboard reads are asynchronous. Never paste into a document selected later.
        if (!_disposed && ReferenceEquals(Controller, controller))
            controller.PasteAt(svg, placement);
    }

    private void ToggleEditorSnap()
    {
        if (Controller is not { } controller)
            return;
        var enabled = !controller.Session.Settings.SnapToGrid;
        controller.Session.Settings.SnapToGrid = enabled;
        controller.Selection.SnapToGrid = enabled;
        SnapToggle.IsChecked = enabled;
    }

    private void CycleEditorScreenMode()
    {
        _editorScreenMode = (_editorScreenMode + 1) % 3;
        if (HostWindow is { } window && !OperatingSystem.IsBrowser())
            window.AppWindow.SetPresenter(_editorScreenMode == 0 ? AppWindowPresenterKind.Default : AppWindowPresenterKind.FullScreen);
        ApplyStudioMetrics();
    }

    private void ReadEditorPreferences()
    {
        try
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            if (values.TryGetValue("UnoDraw.Editor.KeyboardIncrement", out var step) && step is double number && double.IsFinite(number) && number is >= 0.001 and <= 10000)
                _editorKeyboardIncrement = number;
            if (values.TryGetValue("UnoDraw.Editor.ControlBar", out var control) && control is bool controlVisible)
                _editorControlBarVisible = controlVisible;
            if (values.TryGetValue("UnoDraw.Editor.Toolbar", out var toolbar) && toolbar is bool toolsVisible)
                _editorToolbarVisible = toolsVisible;
            if (values.TryGetValue("UnoDraw.Editor.SingleToolColumn", out var columns) && columns is bool singleColumn)
                _editorSingleToolColumn = singleColumn;
            if (values.TryGetValue("UnoDraw.Editor.GuidesVisible", out var guideVisibility) && guideVisibility is bool guidesVisible)
                Canvas.GuidesVisible = guidesVisible;
            if (values.TryGetValue("UnoDraw.Editor.GuidesLocked", out var guideLock) && guideLock is bool guidesLocked)
                Canvas.GuidesLocked = guidesLocked;
            if (values.TryGetValue("UnoDraw.Editor.SnapToGuides", out var guideSnapping) && guideSnapping is bool snapToGuides)
                Canvas.SnapToGuides = snapToGuides;
            if (values.TryGetValue("UnoDraw.Editor.SmartGuides", out var smartSnapping) && smartSnapping is bool smartGuides)
                Canvas.SmartGuides = smartGuides;
            if (values.TryGetValue("UnoDraw.Editor.Theme", out var appearance) && appearance is string name
                && Enum.TryParse<ElementTheme>(name, out var theme) && Enum.IsDefined(theme))
                RequestedTheme = theme;
        }
        catch (Exception exception) { StatusText.Text = "Preferences could not be restored: " + exception.Message; }
    }

    private void SaveEditorPreferences()
    {
        try
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            values["UnoDraw.Editor.KeyboardIncrement"] = _editorKeyboardIncrement;
            values["UnoDraw.Editor.ControlBar"] = _editorControlBarVisible;
            values["UnoDraw.Editor.Toolbar"] = _editorToolbarVisible;
            values["UnoDraw.Editor.SingleToolColumn"] = _editorSingleToolColumn;
            values["UnoDraw.Editor.GuidesVisible"] = Canvas.GuidesVisible;
            values["UnoDraw.Editor.GuidesLocked"] = Canvas.GuidesLocked;
            values["UnoDraw.Editor.SnapToGuides"] = Canvas.SnapToGuides;
            values["UnoDraw.Editor.SmartGuides"] = Canvas.SmartGuides;
            values["UnoDraw.Editor.Theme"] = RequestedTheme.ToString();
        }
        catch (Exception exception) { StatusText.Text = "Preferences could not be saved: " + exception.Message; }
    }

    private async Task ShowEditorPreferencesAsync()
    {
        var panel = new StackPanel { Spacing = 14, Width = DialogContentWidth(430) };
        var appearance = new ComboBox { Header = "Interface", ItemsSource = new[] { "System", "Light", "Dark" }, SelectedIndex = (int)RequestedTheme, HorizontalAlignment = HorizontalAlignment.Stretch };
        var increment = NumberInput("Keyboard increment · SVG units", _editorKeyboardIncrement, 0.001, 10000);
        var grid = NumberInput("Grid spacing · SVG units", Controller?.Session.Settings.GridSize ?? 10, 0.1, 100000);
        var targets = new CheckBox { Content = "Larger tool targets", IsChecked = _studioLayout.TouchDensity };
        var rulers = new CheckBox { Content = "Show rulers", IsChecked = _studioLayout.ShowRulers };
        var control = new CheckBox { Content = "Show context control bar", IsChecked = _editorControlBarVisible };
        var tools = new CheckBox { Content = "Show Tools panel", IsChecked = _editorToolbarVisible };
        foreach (var item in new UIElement[] { appearance, targets, rulers, control, tools, increment, grid })
            panel.Children.Add(item);
        panel.Children.Add(StudioHint("Shift + arrow keys move ten times the keyboard increment. Changes to document grid spacing apply to the current document."));
        var owner = Controller;
        if (await ShowDialogAsync(MakeDialog("Preferences", panel, "OK")) != ContentDialogResult.Primary)
            return;
        if (!double.IsFinite(increment.Value) || increment.Value is < 0.001 or > 10000 || !double.IsFinite(grid.Value) || grid.Value <= 0)
            throw new InvalidOperationException("Enter finite, positive keyboard and grid increments.");
        _editorKeyboardIncrement = increment.Value;
        RequestedTheme = (ElementTheme)appearance.SelectedIndex;
        _studioLayout.TouchDensity = targets.IsChecked == true;
        _studioLayout.ShowRulers = rulers.IsChecked == true;
        _editorControlBarVisible = control.IsChecked == true;
        _editorToolbarVisible = tools.IsChecked == true;
        if (owner is not null && ReferenceEquals(owner, Controller))
        {
            owner.Session.Settings.GridSize = grid.Value;
            owner.Selection.GridSize = grid.Value;
            Canvas.Invalidate();
        }
        ApplyStudioMetrics();
        SaveStudioLayout();
        SaveEditorPreferences();
    }
}
