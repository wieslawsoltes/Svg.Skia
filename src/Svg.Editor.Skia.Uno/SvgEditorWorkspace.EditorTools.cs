using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Svg.Editor.Core;
using Svg.Editor.Skia;
using Windows.Foundation;
using Windows.System;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private sealed record EditorToolEntry(string Id, string Label, SvgEditorToolKind? Tool = null,
        SvgEditorInteractionMode Mode = SvgEditorInteractionMode.None, string Symbol = "", string? Action = null);
    private sealed record EditorToolGroup(StudioToolButton Button, StudioToolIcon Icon, EditorToolEntry[] Entries);
    private readonly List<EditorToolGroup> _editorToolGroups = new();
    private StudioPaintChip? _editorFillChip, _editorStrokeChip;
    private TextBlock? _editorPaintTarget;
    private bool _editorToolStateUpdating;
    private readonly Dictionary<SvgEditorInteractionMode, int> _editorShapeDivisions = new();

    private static readonly EditorToolEntry[] s_editorTools =
    {
        new("tool-select", "Selection", SvgEditorToolKind.Select),
        new("tool-group-select", "Group Selection", SvgEditorToolKind.MultiSelect),
        new("tool-direct", "Direct Selection", SvgEditorToolKind.PathSelect),
        new("tool-polygon-select", "Polygon Nodes", SvgEditorToolKind.PolygonSelect),
        new("tool-polyline-select", "Polyline Nodes", SvgEditorToolKind.PolylineSelect),
        new("tool-magicwand", "Magic Wand", Mode: SvgEditorInteractionMode.MagicWand, Symbol: "MagicWand"),
        new("tool-lasso", "Lasso", Mode: SvgEditorInteractionMode.Lasso, Symbol: "Lasso"),
        new("tool-pen", "Pen", SvgEditorToolKind.PathCubic),
        new("tool-quadratic", "Quadratic Pen", SvgEditorToolKind.PathQuadratic),
        new("tool-straight-pen", "Straight Pen", SvgEditorToolKind.PathLine),
        new("tool-arc", "Arc Pen", SvgEditorToolKind.PathArc),
        new("tool-path-move", "Disjoint Pen", SvgEditorToolKind.PathMove),
        new("tool-add-anchor", "Add Anchor Point", SvgEditorToolKind.PathSelect, SvgEditorInteractionMode.AddAnchor, "AddAnchor"),
        new("tool-delete-anchor", "Delete Anchor Point", SvgEditorToolKind.PathSelect, SvgEditorInteractionMode.DeleteAnchor, "DeleteAnchor"),
        new("tool-anchor", "Anchor Point", SvgEditorToolKind.PathSelect, SvgEditorInteractionMode.AnchorPoint, "AnchorPoint"),
        new("tool-type", "Type", SvgEditorToolKind.Text),
        new("tool-area-type", "Area Type", SvgEditorToolKind.TextArea, SvgEditorInteractionMode.AreaType),
        new("tool-type-on-path", "Type on a Path…", SvgEditorToolKind.TextPath, Action: "typeonpath"),
        new("tool-line", "Line Segment", SvgEditorToolKind.Line),
        new("tool-spiral", "Spiral", Mode: SvgEditorInteractionMode.Spiral, Symbol: "Spiral"),
        new("tool-rectangular-grid", "Rectangular Grid", Mode: SvgEditorInteractionMode.RectangularGrid, Symbol: "RectangularGrid"),
        new("tool-polar-grid", "Polar Grid", Mode: SvgEditorInteractionMode.PolarGrid, Symbol: "PolarGrid"),
        new("tool-rectangle", "Rectangle", SvgEditorToolKind.Rect),
        new("tool-rounded-rectangle", "Rounded Rectangle", Mode: SvgEditorInteractionMode.RoundedRectangle, Symbol: "RoundedRectangle"),
        new("tool-ellipse", "Ellipse", SvgEditorToolKind.Ellipse),
        new("tool-circle", "Circle", SvgEditorToolKind.Circle),
        new("tool-polygon", "Polygon", Mode: SvgEditorInteractionMode.RegularPolygon, Symbol: "RegularPolygon"),
        new("tool-star", "Star", Mode: SvgEditorInteractionMode.Star, Symbol: "Star"),
        new("tool-polygon-path", "Polygon Path", SvgEditorToolKind.Polygon),
        new("tool-polyline", "Polyline", SvgEditorToolKind.Polyline),
        new("tool-brush", "Paintbrush", SvgEditorToolKind.Freehand, SvgEditorInteractionMode.Paintbrush, "Paintbrush"),
        new("tool-blob-brush", "Blob Brush", SvgEditorToolKind.Freehand, SvgEditorInteractionMode.BlobBrush, "BlobBrush"),
        new("tool-pencil", "Pencil", SvgEditorToolKind.Freehand),
        new("tool-smooth", "Smooth", Mode: SvgEditorInteractionMode.Smooth, Symbol: "Smooth"),
        new("tool-path-eraser", "Path Eraser", Mode: SvgEditorInteractionMode.PathEraser, Symbol: "PathEraser"),
        new("tool-join", "Join", Mode: SvgEditorInteractionMode.Join, Symbol: "Join"),
        new("tool-eraser", "Eraser", Mode: SvgEditorInteractionMode.Eraser, Symbol: "Eraser"),
        new("tool-scissors", "Scissors", Mode: SvgEditorInteractionMode.Scissors, Symbol: "Scissors"),
        new("tool-knife", "Knife", Mode: SvgEditorInteractionMode.Knife, Symbol: "Knife"),
        new("tool-rotate", "Rotate", Mode: SvgEditorInteractionMode.Rotate, Symbol: "Rotate"),
        new("tool-reflect", "Reflect", Mode: SvgEditorInteractionMode.Reflect, Symbol: "Reflect"),
        new("tool-scale", "Scale", Mode: SvgEditorInteractionMode.Scale, Symbol: "Scale"),
        new("tool-shear", "Shear", Mode: SvgEditorInteractionMode.Shear, Symbol: "Shear"),
        new("tool-free-transform", "Free Transform", SvgEditorToolKind.Select, SvgEditorInteractionMode.FreeTransform, "FreeTransform"),
        new("tool-width", "Width Profile…", Symbol: "Width", Action: "strokeprofile"),
        new("tool-warp", "Warp", Mode: SvgEditorInteractionMode.Warp, Symbol: "Warp"),
        new("tool-twirl", "Twirl", Mode: SvgEditorInteractionMode.Twirl, Symbol: "Spiral"),
        new("tool-pucker", "Pucker", Mode: SvgEditorInteractionMode.Pucker, Symbol: "Pucker"),
        new("tool-bloat", "Bloat", Mode: SvgEditorInteractionMode.Bloat, Symbol: "Bloat"),
        new("tool-scallop", "Scallop", Mode: SvgEditorInteractionMode.Scallop, Symbol: "Scallop"),
        new("tool-crystallize", "Crystallize", Mode: SvgEditorInteractionMode.Crystallize, Symbol: "Crystallize"),
        new("tool-wrinkle", "Wrinkle", Mode: SvgEditorInteractionMode.Wrinkle, Symbol: "Wrinkle"),
        new("tool-shape-builder", "Shape Builder", Mode: SvgEditorInteractionMode.ShapeBuilder, Symbol: "ShapeBuilder"),
        new("tool-blend", "Blend…", Symbol: "Blend", Action: "blend"),
        new("tool-gradient", "Gradient", Mode: SvgEditorInteractionMode.Gradient, Symbol: "Gradient"),
        new("tool-eyedropper", "Eyedropper", Mode: SvgEditorInteractionMode.Eyedropper, Symbol: "Eyedropper"),
        new("tool-measure", "Measure", Mode: SvgEditorInteractionMode.Measure, Symbol: "Measure"),
        new("tool-mesh", "Gradient Mesh…", Symbol: "Mesh", Action: "mesh"),
        new("tool-symbol", "Symbol Placement…", SvgEditorToolKind.Symbol, Symbol: "Symbol", Action: "placesymbol"),
        new("tool-symbol-sprayer", "Symbol Sprayer", Mode: SvgEditorInteractionMode.SymbolSprayer, Symbol: "SymbolSprayer"),
        new("tool-symbol-shifter", "Symbol Shifter", Mode: SvgEditorInteractionMode.SymbolShifter, Symbol: "SymbolShifter"),
        new("tool-symbol-scruncher", "Symbol Scruncher", Mode: SvgEditorInteractionMode.SymbolScruncher, Symbol: "SymbolScruncher"),
        new("tool-symbol-sizer", "Symbol Sizer", Mode: SvgEditorInteractionMode.SymbolSizer, Symbol: "SymbolSizer"),
        new("tool-symbol-spinner", "Symbol Spinner", Mode: SvgEditorInteractionMode.SymbolSpinner, Symbol: "SymbolSpinner"),
        new("tool-symbol-stainer", "Symbol Stainer", Mode: SvgEditorInteractionMode.SymbolStainer, Symbol: "SymbolStainer"),
        new("tool-symbol-screener", "Symbol Screener", Mode: SvgEditorInteractionMode.SymbolScreener, Symbol: "SymbolScreener"),
        new("tool-symbol-styler", "Symbol Styler", Mode: SvgEditorInteractionMode.SymbolStyler, Symbol: "SymbolStyler"),
        new("tool-artboard", "Artboard", Mode: SvgEditorInteractionMode.Artboard, Symbol: "Artboard"),
        new("tool-hand", "Hand", Symbol: "Hand"),
        new("tool-zoom", "Zoom", Mode: SvgEditorInteractionMode.Zoom, Symbol: "Zoom"),
        new("tool-place", "Place Image…", SvgEditorToolKind.Image, Action: "image")
    };

    private void BuildEditorToolCommands()
    {
        _commands.Add(new EditorCommand("typeonpath", "Type on a Path…", "Type", string.Empty, CreateEditorTextOnPathAsync));
        _commands.Add(new EditorCommand("placesymbol", "Place Symbol…", "Object", string.Empty, ChooseEditorSymbolAsync));
        foreach (var entry in s_editorTools)
        {
            _commands.Add(new EditorCommand(entry.Id, entry.Label, "Tools", EditorShortcutText(entry.Id), async () =>
            {
                if (entry.Mode == SvgEditorInteractionMode.SymbolSprayer && Controller?.Document?.GetElementById(Controller.Tools.SymbolId ?? string.Empty) is not SvgSymbol)
                {
                    await ChooseEditorSymbolAsync();
                    if (Controller?.Document?.GetElementById(Controller.Tools.SymbolId ?? string.Empty) is not SvgSymbol)
                        return;
                }
                if (entry.Action is { } action)
                    await RunCommandAsync(action);
                else
                    SelectEditorTool(entry);
            }));
        }
        _commands.Add(new EditorCommand("tool-options", "Tool Options…", "Edit", string.Empty, ShowEditorToolOptionsAsync));
    }

    private void BuildEditorToolbox()
    {
        InitializePathUtilityStatus();
        ToolRail.Spacing = 1;
        void Group(params string[] ids)
        {
            var entries = ids.Select(id => s_editorTools.Single(entry => entry.Id == id)).ToArray();
            var selected = entries[0];
            var button = new StudioToolButton { MinWidth = 0, Tag = selected.Id };
            var icon = new StudioToolIcon();
            icon.SetBinding(Control.ForegroundProperty, new Binding { Source = button, Path = new PropertyPath(nameof(Control.Foreground)) });
            button.Content = icon;
            var group = new EditorToolGroup(button, icon, entries);
            _editorToolGroups.Add(group);
            SetEditorToolButton(group, selected);
            // Preserve the stable automation IDs used by the existing native smoke suite.
            AutomationProperties.SetAutomationId(button, selected.Tool is { } tool && selected.Mode == SvgEditorInteractionMode.None
                ? "studio-tool-" + tool : "editor-" + selected.Id);
            var suppressClick = false;
            button.Click += async (_, _) =>
            {
                if (suppressClick)
                { suppressClick = false; RefreshEditorToolState(); return; }
                if (button.Tag is string command)
                    await RunCommandAsync(command);
                RefreshEditorToolState();
            };
            if (entries.Length > 1)
            {
                var flyout = new MenuFlyout();
                foreach (var entry in entries)
                {
                    var item = new MenuFlyoutItem { Text = entry.Label, KeyboardAcceleratorTextOverride = EditorShortcutText(entry.Id) };
                    EditorShortcutsChanged += (_, _) => item.KeyboardAcceleratorTextOverride = EditorShortcutText(entry.Id);
                    item.Click += async (_, _) =>
                    {
                        SetEditorToolButton(group, entry);
                        await RunCommandAsync(entry.Id);
                    };
                    flyout.Items.Add(item);
                }
                button.ContextFlyout = flyout;
                button.AlternateMark = "◢";
                var hold = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(420) };
                Point origin = default;
                hold.Tick += (_, _) => { hold.Stop(); suppressClick = true; flyout.ShowAt(button); };
                button.PointerPressed += (_, args) =>
                {
                    suppressClick = false;
                    if (args.GetCurrentPoint(button).Properties.IsLeftButtonPressed)
                    { origin = args.GetCurrentPoint(button).Position; hold.Start(); }
                };
                button.PointerMoved += (_, args) =>
                {
                    var current = args.GetCurrentPoint(button).Position;
                    if (Math.Abs(current.X - origin.X) + Math.Abs(current.Y - origin.Y) > 6)
                        hold.Stop();
                };
                button.PointerReleased += (_, _) => hold.Stop();
                button.PointerCanceled += (_, _) => hold.Stop();
                button.PointerExited += (_, _) => hold.Stop();
                button.Unloaded += (_, _) => hold.Stop();
                button.KeyDown += (_, args) =>
                {
                    if (args.Key == VirtualKey.Down && IsDown(VirtualKey.Menu))
                    { flyout.ShowAt(button); args.Handled = true; }
                };
            }
            button.DoubleTapped += async (_, args) =>
            {
                if (button.Tag is "tool-hand")
                    Canvas.FitActiveArtboard();
                else if (button.Tag is "tool-zoom")
                    Canvas.ResetZoom();
                else
                    await ShowEditorToolOptionsAsync();
                args.Handled = true;
            };
            if (selected.Id == "tool-hand")
                _studioHand = button;
            EditorShortcutsChanged += (_, _) =>
            {
                if (button.Tag is string id && entries.FirstOrDefault(entry => entry.Id == id) is { } current)
                    SetEditorToolButton(group, current);
            };
            if (selected.Tool is { } primary && selected.Mode == SvgEditorInteractionMode.None)
                _toolButtons.TryAdd(primary, button);
            ToolRail.Children.Add(button);
        }

        Group("tool-select", "tool-group-select");
        Group("tool-direct", "tool-polygon-select", "tool-polyline-select");
        Group("tool-magicwand");
        Group("tool-lasso");
        Group("tool-pen", "tool-add-anchor", "tool-delete-anchor", "tool-anchor", "tool-quadratic", "tool-straight-pen", "tool-arc", "tool-path-move");
        Group("tool-type", "tool-area-type", "tool-type-on-path");
        Group("tool-line", "tool-spiral", "tool-rectangular-grid", "tool-polar-grid");
        Group("tool-rectangle", "tool-rounded-rectangle", "tool-ellipse", "tool-circle", "tool-polygon", "tool-star", "tool-polygon-path", "tool-polyline");
        Group("tool-brush", "tool-blob-brush");
        Group("tool-pencil", "tool-smooth", "tool-path-eraser", "tool-join");
        Group("tool-eraser", "tool-scissors", "tool-knife");
        Group("tool-rotate", "tool-reflect");
        Group("tool-scale", "tool-shear", "tool-free-transform");
        Group("tool-width", "tool-warp", "tool-twirl", "tool-pucker", "tool-bloat", "tool-scallop", "tool-crystallize", "tool-wrinkle");
        Group("tool-shape-builder");
        Group("tool-blend");
        Group("tool-gradient");
        Group("tool-eyedropper", "tool-measure");
        Group("tool-mesh");
        Group("tool-symbol-sprayer", "tool-symbol-shifter", "tool-symbol-scruncher", "tool-symbol-sizer", "tool-symbol-spinner", "tool-symbol-stainer", "tool-symbol-screener", "tool-symbol-styler", "tool-symbol", "tool-place");
        Group("tool-artboard");
        Group("tool-hand");
        Group("tool-zoom");
        var allTools = new StudioIconButton { Label = "All tools", Content = new StudioToolIcon { Symbol = "More" }, Width = 30, Height = 32 };
        var allFlyout = new MenuFlyout();
        foreach (var group in _editorToolGroups)
        {
            var submenu = new MenuFlyoutSubItem { Text = group.Entries[0].Label };
            foreach (var entry in group.Entries)
            {
                var item = new MenuFlyoutItem { Text = entry.Label, KeyboardAcceleratorTextOverride = EditorShortcutText(entry.Id) };
                EditorShortcutsChanged += (_, _) => item.KeyboardAcceleratorTextOverride = EditorShortcutText(entry.Id);
                item.Click += async (_, _) => await RunCommandAsync(entry.Id);
                submenu.Items.Add(item);
            }
            allFlyout.Items.Add(submenu);
        }
        allTools.Flyout = allFlyout;
        AutomationProperties.SetAutomationId(allTools, "EditorAllTools");
        ToolRail.Children.Add(allTools);
        BuildEditorPaintShelf();
        Canvas.InteractionStarting += (_, _) =>
        {
            if (Canvas.InteractionMode == SvgEditorInteractionMode.SymbolStyler
                && _editorGraphicStyles?.SelectedItem is global::Svg.Editor.Svg.Models.StyleEntry style)
                Canvas.SymbolBrushOptions.GraphicStyleName = style.Name;
        };
        Canvas.TextEditRequested += (_, args) => DispatcherQueue.TryEnqueue(async () =>
        {
            if (_disposed || _dialogVisible || _closing || !ReferenceEquals(Controller, args.Controller)
                || !args.Controller.SelectedElements.Contains(args.Element))
                return;
            await RunCommandAsync(args.IsArea ? "textframes" : "text");
        });
        ToolRail.Loaded += (_, _) =>
        {
            for (var parent = VisualTreeHelper.GetParent(ToolRail); parent is not null; parent = VisualTreeHelper.GetParent(parent))
                if (parent is ScrollViewer scroller)
                { scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden; break; }
        };
        RefreshEditorToolState();
        ApplyStudioMetrics();
    }

    private void SetEditorToolButton(EditorToolGroup group, EditorToolEntry entry)
    {
        group.Button.Tag = entry.Id;
        group.Button.Label = entry.Label;
        group.Button.Shortcut = EditorShortcutText(entry.Id);
        group.Icon.Symbol = entry.Symbol;
        if (entry.Tool is { } tool)
            group.Icon.Kind = tool;
        var shortcut = group.Button.Shortcut;
        ToolTipService.SetToolTip(group.Button, entry.Label + (shortcut.Length > 0 ? " (" + shortcut + ")" : string.Empty)
            + (group.Entries.Length > 1 ? " · Hold, right-click, or Alt+Down for related tools" : string.Empty));
        AutomationProperties.SetHelpText(group.Button, group.Entries.Length > 1 ? "Hold or press Alt+Down to choose a related tool." : entry.Label);
    }

    private void SelectEditorTool(EditorToolEntry entry)
    {
        if (SvgEditorToolGeometry.IsShape(Canvas.InteractionMode))
            _editorShapeDivisions[Canvas.InteractionMode] = Canvas.ShapeDivisions;
        CommitInspectorEdits();
        Canvas.CancelGesture();
        if (entry.Id == "tool-hand")
            Canvas.PanMode = true;
        else
        {
            Canvas.PanMode = false;
            SetTool(entry.Tool ?? SvgEditorToolKind.Select);
            Canvas.InteractionMode = entry.Mode;
            Canvas.ShapeDivisions = _editorShapeDivisions.TryGetValue(entry.Mode, out var divisions) ? divisions
                : entry.Mode == SvgEditorInteractionMode.Star ? 5 : entry.Mode == SvgEditorInteractionMode.Spiral ? 3 : 6;
        }
        RefreshWorkspace();
        // The workspace owns unmodified canvas shortcuts. Buttons retain their focus
        // only while a popup is open; a selected drawing tool is immediately ready.
        Focus(FocusState.Programmatic);
    }

    private bool IsActiveEditorTool(EditorToolEntry entry)
    {
        if (Canvas.ZoomMode)
            return entry.Id == "tool-zoom";
        if (entry.Id == "tool-hand")
            return Canvas.PanMode;
        if (Canvas.PanMode || entry.Action is not null && entry.Tool is not (SvgEditorToolKind.Symbol or SvgEditorToolKind.Image))
            return false;
        if (entry.Mode != SvgEditorInteractionMode.None)
            return entry.Mode == Canvas.InteractionMode;
        return Canvas.InteractionMode == SvgEditorInteractionMode.None && entry.Tool == Canvas.Tool;
    }

    private void RefreshEditorToolState()
    {
        if (_editorToolStateUpdating)
            return;
        _editorToolStateUpdating = true;
        try
        {
            foreach (var group in _editorToolGroups)
            {
                var active = group.Entries.FirstOrDefault(IsActiveEditorTool);
                if (active is not null)
                    SetEditorToolButton(group, active);
                group.Button.IsChecked = active is not null;
            }
            var selected = Controller?.SelectedElements.FirstOrDefault();
            if (_editorFillChip is not null && _editorStrokeChip is not null)
            {
                _editorFillChip.Paint = selected is null ? Controller?.CurrentFill ?? "#FFFFFF" : PaintText(selected.Fill);
                _editorStrokeChip.Paint = selected is null ? Controller?.CurrentStroke ?? "#000000" : PaintText(selected.Stroke);
                _editorFillChip.IsMixed = Controller?.SelectedElements.Skip(1).Any(element => PaintText(element.Fill) != _editorFillChip.Paint) == true;
                _editorStrokeChip.IsMixed = Controller?.SelectedElements.Skip(1).Any(element => PaintText(element.Stroke) != _editorStrokeChip.Paint) == true;
                _editorFillChip.BorderThickness = new Thickness(Canvas.StrokePaintTarget ? 1 : 2);
                _editorStrokeChip.BorderThickness = new Thickness(Canvas.StrokePaintTarget ? 2 : 1);
                Microsoft.UI.Xaml.Controls.Canvas.SetZIndex(_editorFillChip, Canvas.StrokePaintTarget ? 0 : 1);
                Microsoft.UI.Xaml.Controls.Canvas.SetZIndex(_editorStrokeChip, Canvas.StrokePaintTarget ? 1 : 0);
                if (_editorPaintTarget is not null)
                    _editorPaintTarget.Text = Canvas.StrokePaintTarget ? "Stroke" : "Fill";
            }
        }
        finally { _editorToolStateUpdating = false; }
    }

    private string ToolContextLabel() => s_editorTools.FirstOrDefault(IsActiveEditorTool)?.Label ?? ToolLabel(Canvas.Tool);

    private string EditorToolHint() => Canvas.PanMode ? "Drag to pan · Double-click Hand to fit the artboard"
        : Canvas.InteractionMode switch
        {
            SvgEditorInteractionMode.Rotate => "Drag around the selection center · Shift constrains to 45°",
            SvgEditorInteractionMode.Scale => "Drag to scale around the selection center · Shift constrains proportions",
            SvgEditorInteractionMode.Shear => "Drag horizontally or vertically to shear around the center · Shift constrains to 15°",
            SvgEditorInteractionMode.Reflect => "Drag the mirror axis around the selection center · Shift constrains to 45°",
            SvgEditorInteractionMode.FreeTransform => "Drag artwork or its bounding-box handles · Shift constrains the transform",
            SvgEditorInteractionMode.Gradient => "Drag a gradient line on selected artwork · Shift constrains its angle",
            SvgEditorInteractionMode.Eyedropper => "Click to sample appearance · Shift-click samples only the active paint",
            SvgEditorInteractionMode.Artboard => "Drag handles to resize · Drag empty space to add · Alt-drag an artboard to duplicate",
            SvgEditorInteractionMode.Zoom => "Click to zoom in · Alt-click to zoom out · Drag to fit a region",
            SvgEditorInteractionMode.Paintbrush => "Drag to paint · Double-click for brush options",
            SvgEditorInteractionMode.BlobBrush => "Drag to paint filled shapes · Touching fills of the same color merge · Double-click for options",
            SvgEditorInteractionMode.Smooth => "Brush near anchors to smooth selected paths · Double-click for diameter and strength",
            SvgEditorInteractionMode.PathEraser => "Brush across paths to remove segments while retaining editable strokes",
            SvgEditorInteractionMode.Join => "Brush across two open endpoints to join them · Double-click for brush diameter",
            SvgEditorInteractionMode.Measure => "Drag to measure distance and angle in SVG user units · Shift constrains direction",
            SvgEditorInteractionMode.AreaType => "Drag a paragraph frame · Edit its text and linked flow in the text editor",
            SvgEditorInteractionMode.Scissors => "Click a path edge to cut · Select artwork first to restrict the target · Escape cancels",
            SvgEditorInteractionMode.Eraser => "Drag to erase vector artwork · Select artwork to restrict targets · Double-click to set diameter",
            SvgEditorInteractionMode.Knife => "Drag across filled artwork to divide it · Shift draws a straight cut at 45° increments",
            SvgEditorInteractionMode.Lasso => "Draw around artwork · Shift adds to selection · Alt subtracts · Escape cancels",
            SvgEditorInteractionMode.MagicWand => "Click to select matching artwork · Shift adds · Alt subtracts · Double-click for tolerance",
            SvgEditorInteractionMode.AddAnchor => "Click a path segment to insert an anchor while preserving its curve",
            SvgEditorInteractionMode.DeleteAnchor => "Click an anchor to remove it · Escape cancels",
            SvgEditorInteractionMode.AnchorPoint => "Click an anchor to make a corner · Drag to pull symmetric direction handles",
            SvgEditorInteractionMode.ShapeBuilder => "Select shapes, then drag through regions to join them · Alt-drag removes regions",
            SvgEditorInteractionMode.SymbolSprayer => "Drag or hold to spray the selected symbol · Double-click for density and size",
            _ when SvgEditorCanvas.TryGetSymbolBrushKind(Canvas.InteractionMode, out _) => "Brush symbol instances · Alt reverses the effect · Double-click for influence and style",
            _ when SvgEditorCanvas.TryGetDeformationKind(Canvas.InteractionMode, out _) => "Brush selected paths to reshape them · Alt reverses the effect · Double-click for diameter and strength",
            _ when SvgEditorToolGeometry.IsShape(Canvas.InteractionMode) => "Drag to draw · Shift constrains proportions · Double-click for options",
            _ => "Space to pan · Wheel to zoom · " + EditorShortcutText("commands") + " for commands"
        };

    private void SetActiveEditorPaint(bool stroke)
    {
        Canvas.StrokePaintTarget = stroke;
        RefreshEditorToolState();
        StatusText.Text = (stroke ? "Stroke" : "Fill") + " is active · " + EditorShortcutText("swappaints") + " swaps paints · "
            + EditorShortcutText("nonepaint") + " removes the active paint";
    }

    private void BuildEditorPaintShelf()
    {
        var paints = new Grid { Height = 49, Margin = new Thickness(3, 2, 3, 0) };
        _editorStrokeChip = new StudioPaintChip
        {
            Label = "Stroke",
            IsCompact = true,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(0),
            Width = 34,
            Height = 34,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        _editorFillChip = new StudioPaintChip
        {
            Label = "Fill",
            IsCompact = true,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(0),
            Width = 34,
            Height = 34,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        _editorFillChip.Click += (_, _) => { SetActiveEditorPaint(false); OpenEditorPaint(_editorFillChip, false); };
        _editorStrokeChip.Click += (_, _) => { SetActiveEditorPaint(true); OpenEditorPaint(_editorStrokeChip, true); };
        paints.Children.Add(_editorStrokeChip);
        paints.Children.Add(_editorFillChip);
        ToolPaintShelf.SizeChanged += (_, _) =>
        {
            var narrow = ToolPaintShelf.ActualWidth < 48;
            var size = narrow ? 24 : 34;
            _editorFillChip.Width = _editorFillChip.Height = _editorStrokeChip.Width = _editorStrokeChip.Height = size;
            paints.Height = narrow ? 38 : 49;
            paints.Margin = new Thickness(narrow ? 1 : 3, 2, narrow ? 1 : 3, 0);
        };
        ToolPaintShelf.Children.Add(paints);
        _editorPaintTarget = new TextBlock { Text = "Fill", FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center };
        ToolPaintShelf.Children.Add(_editorPaintTarget);
        var actions = new Grid { ColumnSpacing = 1 };
        actions.ColumnDefinitions.Add(new ColumnDefinition());
        actions.ColumnDefinitions.Add(new ColumnDefinition());
        var swap = new StudioIconButton { Label = "Swap Fill and Stroke", Shortcut = EditorShortcutText("swappaints"), Content = "⇄", MinWidth = 0, Padding = new Thickness(0), MinHeight = 24 };
        var defaults = new StudioIconButton { Label = "Default Fill and Stroke", Shortcut = EditorShortcutText("defaultpaints"), Content = "D", MinWidth = 0, Padding = new Thickness(0), MinHeight = 24 };
        swap.Click += async (_, _) => await RunCommandAsync("swappaints");
        defaults.Click += async (_, _) => await RunCommandAsync("defaultpaints");
        actions.Children.Add(swap);
        actions.Children.Add(defaults);
        Grid.SetColumn(defaults, 1);
        ToolPaintShelf.Children.Add(actions);
        var none = new StudioIconButton { Label = "No Paint", Shortcut = EditorShortcutText("nonepaint"), Content = "∅", MinWidth = 0, MinHeight = 24, Padding = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Stretch };
        none.Click += async (_, _) => await RunCommandAsync("nonepaint");
        ToolPaintShelf.Children.Add(none);
        EditorShortcutsChanged += (_, _) =>
        {
            swap.Shortcut = EditorShortcutText("swappaints");
            defaults.Shortcut = EditorShortcutText("defaultpaints");
            none.Shortcut = EditorShortcutText("nonepaint");
        };
    }

    private void OpenEditorPaint(StudioPaintChip anchor, bool stroke) => OpenStudioPaint(anchor, stroke);

    private async Task ShowEditorToolOptionsAsync()
    {
        if (await ShowPathUtilityOptionsAsync())
            return;
        if (await ShowAdvancedEditorToolOptionsAsync())
            return;
        if (Canvas.PanMode)
        { Canvas.FitActiveArtboard(); return; }
        if (Canvas.InteractionMode == SvgEditorInteractionMode.Zoom)
        { Canvas.ResetZoom(); return; }
        if (Canvas.InteractionMode == SvgEditorInteractionMode.Artboard)
        { await ArtboardDialogAsync(); return; }
        if (Canvas.InteractionMode == SvgEditorInteractionMode.Paintbrush)
        { OpenStudioPanel("brushes"); return; }
        if (Canvas.InteractionMode == SvgEditorInteractionMode.AreaType)
        { await TextFramesDialogAsync(); return; }
        if (Canvas.InteractionMode == SvgEditorInteractionMode.Gradient)
        { await GradientDialogAsync(); return; }
        if (Canvas.InteractionMode == SvgEditorInteractionMode.Eraser)
        {
            var diameter = NumberInput("Diameter", Canvas.EraserDiameter, 0.1, 10000);
            var panel = new StackPanel { Spacing = 10, Width = DialogContentWidth(360) };
            panel.Children.Add(diameter);
            panel.Children.Add(StudioHint("The eraser cuts editable vector geometry. Stroked lines become filled outlines with their original caps, joins and dashes."));
            if (await ShowDialogAsync(MakeDialog("Eraser Options", panel)) == ContentDialogResult.Primary)
                Canvas.EraserDiameter = (float)diameter.Value;
            return;
        }
        if (Canvas.InteractionMode == SvgEditorInteractionMode.Lasso)
        {
            var contained = new CheckBox { Content = "Select only artwork fully inside the lasso", IsChecked = Canvas.LassoFullyContained };
            var panel = new StackPanel { Spacing = 10, Width = DialogContentWidth(400) };
            panel.Children.Add(contained);
            panel.Children.Add(StudioHint("When off, the lasso also selects artwork crossing its boundary. Shift adds to selection; Alt subtracts."));
            if (await ShowDialogAsync(MakeDialog("Lasso Options", panel)) == ContentDialogResult.Primary)
                Canvas.LassoFullyContained = contained.IsChecked == true;
            return;
        }
        if (Canvas.InteractionMode == SvgEditorInteractionMode.MagicWand)
        {
            var options = Canvas.MagicWandOptions;
            var fill = new CheckBox { Content = "Fill color", IsChecked = options.MatchFill };
            var stroke = new CheckBox { Content = "Stroke color", IsChecked = options.MatchStroke };
            var width = new CheckBox { Content = "Stroke width", IsChecked = options.MatchStrokeWidth };
            var opacity = new CheckBox { Content = "Opacity", IsChecked = options.MatchOpacity };
            var colorTolerance = NumberInput("Color tolerance (0–255)", options.ColorTolerance, 0, 255);
            var widthTolerance = NumberInput("Stroke width tolerance", options.StrokeWidthTolerance, 0, 10000);
            var opacityTolerance = NumberInput("Opacity tolerance (%)", options.OpacityTolerance * 100, 0, 100);
            var panel = new StackPanel { Spacing = 10, Width = DialogContentWidth(380) };
            foreach (var field in new UIElement[] { fill, stroke, width, opacity, colorTolerance, widthTolerance, opacityTolerance })
                panel.Children.Add(field);
            panel.Children.Add(StudioHint("Matching uses the artwork's effective appearance, including inherited paints and group opacity."));
            var dialog = MakeDialog("Magic Wand Options", panel);
            void ValidateOptions() => dialog.IsPrimaryButtonEnabled = fill.IsChecked == true || stroke.IsChecked == true || width.IsChecked == true || opacity.IsChecked == true;
            foreach (var check in new[] { fill, stroke, width, opacity })
            { check.Checked += (_, _) => ValidateOptions(); check.Unchecked += (_, _) => ValidateOptions(); }
            ValidateOptions();
            if (await ShowDialogAsync(dialog) == ContentDialogResult.Primary)
                Canvas.MagicWandOptions = new SvgMagicWandOptions
                {
                    MatchFill = fill.IsChecked == true,
                    MatchStroke = stroke.IsChecked == true,
                    MatchStrokeWidth = width.IsChecked == true,
                    MatchOpacity = opacity.IsChecked == true,
                    ColorTolerance = (float)colorTolerance.Value,
                    StrokeWidthTolerance = (float)widthTolerance.Value,
                    OpacityTolerance = (float)(opacityTolerance.Value / 100)
                };
            return;
        }
        if (Canvas.InteractionMode is SvgEditorInteractionMode.Rotate or SvgEditorInteractionMode.Scale or SvgEditorInteractionMode.Reflect or SvgEditorInteractionMode.FreeTransform or SvgEditorInteractionMode.Shear)
        { await TransformDialogAsync(); return; }
        if (SvgEditorToolGeometry.IsShape(Canvas.InteractionMode))
        {
            var divisions = NumberInput("Sides, points, grid divisions, or spiral turns", Canvas.ShapeDivisions, 3, 64);
            var panel = new StackPanel { Spacing = 10, Width = DialogContentWidth(360) };
            panel.Children.Add(divisions);
            panel.Children.Add(StudioHint("Hold Shift while drawing to constrain proportions. The next drag uses these settings."));
            if (await ShowDialogAsync(MakeDialog(ToolContextLabel() + " Options", panel)) == ContentDialogResult.Primary)
                Canvas.ShapeDivisions = (int)Math.Clamp(divisions.Value, 3, 64);
            return;
        }
        if (Canvas.Tool == SvgEditorToolKind.Text && Controller?.SelectedElements.OfType<SvgTextBase>().Any() == true)
        { await EditTextAsync(); return; }
        if (Canvas.Tool == SvgEditorToolKind.Freehand)
        { await BrushLibraryDialogAsync(); return; }
        await ShowHelpAsync();
    }

    private async Task CreateEditorTextOnPathAsync()
    {
        if (Controller is not { } controller || controller.SelectedElements.Count != 1 || controller.SelectedElements[0] is not SvgPath)
            throw new InvalidOperationException("Select one path with Direct Selection before placing type on it.");
        controller.EnsureSelectionEditable();
        var context = new SvgEditorSelectionContext(controller);
        var text = new TextBox { Header = "Text", Text = "Type on a path", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxLength = 100000, Width = DialogContentWidth(420) };
        if (await ShowDialogAsync(MakeDialog("Type on a Path", text, "Create")) != ContentDialogResult.Primary)
            return;
        if (!context.IsCurrent(Controller))
            throw new InvalidOperationException("The selected path changed while the text dialog was open.");
        controller.AddTextOnSelectedPath(text.Text);
        SetTool(SvgEditorToolKind.Select);
    }

    private async Task ChooseEditorSymbolAsync()
    {
        if (Controller is not { Document: { } document } controller)
            return;
        var symbols = document.Descendants().OfType<SvgSymbol>().Where(symbol => !string.IsNullOrWhiteSpace(symbol.ID)).ToArray();
        if (symbols.Length == 0)
        {
            if (controller.SelectedElements.Count > 0)
                await CreateSymbolAsync();
            else
                throw new InvalidOperationException("Create a symbol from selected artwork before placing instances.");
            return;
        }
        var choice = new ComboBox { Header = "Document symbol", ItemsSource = symbols.Select(symbol => symbol.ID).ToArray(), SelectedIndex = 0, Width = DialogContentWidth(320) };
        if (await ShowDialogAsync(MakeDialog("Place Symbol", choice, "Place")) != ContentDialogResult.Primary)
            return;
        if (!ReferenceEquals(controller, Controller) || !ReferenceEquals(document, controller.Document))
            throw new InvalidOperationException("The document changed while the symbol picker was open.");
        controller.Tools.SymbolId = (string)choice.SelectedItem;
        SetTool(SvgEditorToolKind.Symbol);
        Focus(FocusState.Programmatic);
        StatusText.Text = "Click the artboard to place the selected symbol. Instances share their document definition.";
    }
}
