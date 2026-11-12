using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Svg.Editor.Core;
using Svg.Editor.Skia;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private StudioToolButton? _studioHand;

    private void BuildStudioTools() => BuildEditorToolbox();

    private void BuildStudioPanelRail()
    {
        var icons = new Dictionary<string, string>
        {
            ["properties"] = "\uE713",
            ["transform"] = "\uE7C2",
            ["appearance"] = "\uE790",
            ["stroke"] = "\uE76D",
            ["align"] = "\uE8E4",
            ["geometry"] = "\uE8B0",
            ["layers"] = "\uE81E",
            ["resources"] = "\uE8B7",
            ["typography"] = "\uE8D2",
            ["effects"] = "\uE7B8",
            ["attributes"] = "\uE943",
            ["color"] = "\uE790",
            ["paragraph"] = "\uE8E4",
            ["links"] = "\uE71B",
            ["document-info"] = "\uE946",
            ["history"] = "\uE81C"
        };
        var vectorIcons = new Dictionary<string, string>
        {
            ["swatches"] = "RectangularGrid",
            ["gradient"] = "Gradient",
            ["brushes"] = "Paintbrush",
            ["symbols"] = "Symbol",
            ["transparency"] = "Blend",
            ["artboards"] = "Artboard",
            ["graphic-styles"] = "SymbolStyler",
            ["navigator"] = "Zoom"
        };
        foreach (var id in SvgWorkspaceLayout.PanelIds)
        {
            var panel = _studioPanels[id];
            var button = new StudioIconButton { Label = panel.Title, Width = 28, Height = 32 };
            if (vectorIcons.TryGetValue(id, out var symbol))
            {
                var icon = new StudioToolIcon { Symbol = symbol };
                icon.SetBinding(Control.ForegroundProperty, new Binding { Source = button, Path = new PropertyPath(nameof(Foreground)) });
                button.Content = icon;
            }
            else
                button.Content = new FontIcon { Glyph = icons.TryGetValue(id, out var glyph) ? glyph : "\uE8B7", FontSize = 14 };
            AutomationProperties.SetAutomationId(button, "studio-panel-" + id);
            button.Click += (_, _) => ApplySafely(() => OpenStudioPanel(id));
            PanelRail.Children.Add(button);
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
        Add("swappaints", "Swap fill and stroke", "Object", () => { Controller?.SwapFillAndStroke(); RefreshWorkspace(); });
        Add("defaultpaints", "Default fill and stroke", "Object", () => { Controller?.ResetPaints(); RefreshWorkspace(); });
        Add("activepaint", "Switch active fill / stroke", "Object", () => SetActiveEditorPaint(!Canvas.StrokePaintTarget));
        Add("nonepaint", "No paint", "Object", () =>
        {
            if (Controller is not { } controller)
                return;
            if (Canvas.StrokePaintTarget)
                controller.SetStroke("none");
            else
                controller.SetFill("none");
            RefreshWorkspace();
        });
        BuildEditorToolCommands();
    }

    private void ShowStudioPanelMenu()
    {
        var menu = new MenuFlyout();
        foreach (var id in SvgWorkspaceLayout.PanelIds)
        {
            var item = new MenuFlyoutItem { Text = _studioPanels[id].Title };
            item.Click += (_, _) => ApplySafely(() => OpenStudioPanel(id));
            menu.Items.Add(item);
        }
        menu.Items.Add(new MenuFlyoutSeparator());
        var reset = new MenuFlyoutItem { Text = "Reset Essentials workspace" };
        reset.Click += (_, _) => ApplySafely(() => ResetStudioLayout(SvgWorkspacePreset.Essentials));
        menu.Items.Add(reset);
        menu.ShowAt(PanelRail);
    }
}
