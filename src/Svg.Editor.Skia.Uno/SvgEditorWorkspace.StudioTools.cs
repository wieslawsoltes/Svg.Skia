using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Svg.Editor.Core;
using Svg.Editor.Skia;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private StudioToolButton? _studioHand;

    private void BuildStudioTools()
    {
        var tools = new (SvgEditorToolKind Tool, string Glyph, string Shortcut)[]
        {
            (SvgEditorToolKind.Select, "\uE8B0", "V"), (SvgEditorToolKind.PathSelect, "\uE809", "A"),
            (SvgEditorToolKind.PathCubic, "\uED63", "P"), (SvgEditorToolKind.Freehand, "\uE76D", "B"),
            (SvgEditorToolKind.Rect, "\uE003", "R"), (SvgEditorToolKind.Ellipse, "\uEA3A", "E"),
            (SvgEditorToolKind.Line, "\uE738", "L"), (SvgEditorToolKind.Polygon, "\uE7C1", "G"),
            (SvgEditorToolKind.Text, "\uE8D2", "T"), (SvgEditorToolKind.Image, "\uE91B", "")
        };
        foreach (var tool in tools)
        {
            var button = new StudioToolButton { Label = ToolLabel(tool.Tool), Shortcut = tool.Shortcut,
                Content = new FontIcon { Glyph = tool.Glyph, FontSize = 16 }, Tag = tool.Tool };
            button.Click += async (_, _) =>
            {
                if (tool.Tool == SvgEditorToolKind.Image) await RunCommandAsync("image");
                else ApplySafely(() => { CommitInspectorEdits(); SetTool(tool.Tool); });
            };
            var alternates = tool.Tool switch
            {
                SvgEditorToolKind.Rect or SvgEditorToolKind.Ellipse or SvgEditorToolKind.Polygon => new[] { SvgEditorToolKind.Rect, SvgEditorToolKind.Ellipse, SvgEditorToolKind.Circle, SvgEditorToolKind.Polygon, SvgEditorToolKind.Polyline },
                SvgEditorToolKind.PathCubic => new[] { SvgEditorToolKind.PathCubic, SvgEditorToolKind.PathQuadratic, SvgEditorToolKind.PathLine, SvgEditorToolKind.PathArc },
                _ => Array.Empty<SvgEditorToolKind>()
            };
            if (alternates.Length > 0)
            {
                var menu = new MenuFlyout();
                foreach (var alternate in alternates)
                {
                    var item = new MenuFlyoutItem { Text = ToolLabel(alternate) };
                    item.Click += (_, _) => ApplySafely(() => { CommitInspectorEdits(); SetTool(alternate); });
                    menu.Items.Add(item);
                }
                button.ContextFlyout = menu; button.AlternateMark = "◢";
                ToolTipService.SetToolTip(button, button.Label + " (" + button.Shortcut + ") · right-click / hold for related tools");
            }
            ToolRail.Children.Add(button); _toolButtons.Add(tool.Tool, button);
        }
        _studioHand = new StudioToolButton { Label = "Hand", Shortcut = "H / hold Space", Content = new FontIcon { Glyph = "\uE7C9", FontSize = 16 } };
        _studioHand.Click += (_, _) =>
        {
            ApplySafely(() => { CommitInspectorEdits(); Canvas.CancelGesture(); Canvas.PanMode = _studioHand.IsChecked == true; RefreshWorkspace(); });
        };
        ToolRail.Children.Add(_studioHand);
        var fit = new StudioIconButton { Label = "Fit artboard", Shortcut = "Shift+1", Content = new FontIcon { Glyph = "\uE9A6", FontSize = 16 }, Width = 30, Height = 32 };
        fit.Click += (_, _) => Canvas.FitDocument(); ToolRail.Children.Add(fit);
        var fill = StudioPaint(false); fill.MinWidth = 0; fill.Width = 58;
        var stroke = StudioPaint(true); stroke.MinWidth = 0; stroke.Width = 58;
        ToolPaintShelf.Children.Add(fill); ToolPaintShelf.Children.Add(stroke);
        ToolPaintShelf.Children.Add(StudioCommand("swappaints", "⇄", 1));
        ToolPaintShelf.Children.Add(StudioCommand("defaultpaints", "D", 1));
        ApplyStudioMetrics();
    }

    private void BuildStudioPanelRail()
    {
        var icons = new Dictionary<string, string>
        {
            ["properties"] = "\uE713", ["transform"] = "\uE7C2", ["appearance"] = "\uE790",
            ["stroke"] = "\uE76D", ["align"] = "\uE8E4", ["geometry"] = "\uE8B0",
            ["layers"] = "\uE81E", ["resources"] = "\uE8B7", ["typography"] = "\uE8D2",
            ["effects"] = "\uE7B8", ["attributes"] = "\uE943"
        };
        foreach (var id in SvgWorkspaceLayout.PanelIds)
        {
            var panel = _studioPanels[id];
            var button = new StudioIconButton { Label = panel.Title, Content = new FontIcon { Glyph = icons[id], FontSize = 14 }, Width = 28, Height = 32 };
            button.Click += (_, _) => ApplySafely(() => OpenStudioPanel(id)); PanelRail.Children.Add(button);
        }
    }

    private void BuildStudioCommands()
    {
        void Add(string id, string title, string group, Action execute)
            => _commands.Add(new EditorCommand(id, title, group, string.Empty, () => { execute(); return Task.CompletedTask; }));
        foreach (var id in SvgWorkspaceLayout.PanelIds)
        {
            var panel = _studioPanels[id];
            Add("panel-" + id, panel.Title + " panel", "Window", () => OpenStudioPanel(id));
        }
        Add("workspacepanels", "Panels and workspace…", "Window", ShowStudioPanelMenu);
        Add("workspace-reset", "Reset workspace", "Window", () => ResetStudioLayout(SvgWorkspacePreset.Essentials));
        foreach (var preset in Enum.GetValues<SvgWorkspacePreset>())
            Add("workspace-" + preset.ToString().ToLowerInvariant(), preset + " workspace", "Window", () => ResetStudioLayout(preset));
        Add("workspace-density", "Toggle larger tool targets", "Window", () => { _studioLayout.TouchDensity = !_studioLayout.TouchDensity; ApplyStudioMetrics(); SaveStudioLayout(); });
        Add("workspace-rulers", "Toggle rulers", "View", () => { _studioLayout.ShowRulers = !_studioLayout.ShowRulers; ApplyStudioMetrics(); SaveStudioLayout(); });
        Add("workspace-hidepanels", "Hide / restore panels", "Window", () => { _studioLayout.PanelsHidden = !_studioLayout.PanelsHidden; ApplyStudioMetrics(); });
        Add("swappaints", "Swap fill and stroke", "Object", () => StudioEdit(controller => controller.Execute("Swap fill and stroke", () =>
        {
            foreach (var element in controller.SelectedElements)
            { var fill = element.Fill; element.Fill = element.Stroke; element.Stroke = fill; }
        })));
        Add("defaultpaints", "Default fill and stroke", "Object", () => StudioEdit(controller => controller.Execute("Default fill and stroke", () =>
        { controller.SetFill("#FFFFFF"); controller.SetStroke("#000000"); controller.SetStrokeWidth(1); })));
    }

    private void ShowStudioPanelMenu()
    {
        var menu = new MenuFlyout();
        foreach (var id in SvgWorkspaceLayout.PanelIds)
        {
            var item = new MenuFlyoutItem { Text = _studioPanels[id].Title };
            item.Click += (_, _) => ApplySafely(() => OpenStudioPanel(id)); menu.Items.Add(item);
        }
        menu.Items.Add(new MenuFlyoutSeparator());
        var reset = new MenuFlyoutItem { Text = "Reset Essentials workspace" };
        reset.Click += (_, _) => ApplySafely(() => ResetStudioLayout(SvgWorkspacePreset.Essentials)); menu.Items.Add(reset);
        menu.ShowAt(ContextSelectionLabel);
    }
}
