using System.Text.Json.Nodes;
using Microsoft.UI.Xaml;
using SkiaSharp;
using Svg.Editor.Core;
using Svg.Editor.Mcp;
using Svg.Editor.Skia;
using Windows.Storage;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private bool McpCommandNeedsInteraction(EditorCommand command)
    {
        if (command.Label.Contains('…') || command.Id is "save" or "close" or "cut" or "copy" or "paste" or "pasteinplace" or "pasteinfront" or "pastebehind" or "mcp-connection")
            return true;
        var tool = s_editorTools.FirstOrDefault(entry => entry.Id == command.Id);
        return tool?.Action is not null || tool?.Mode == SvgEditorInteractionMode.SymbolSprayer && Controller?.Document?.GetElementById(Controller.Tools.SymbolId ?? string.Empty) is not SvgSymbol;
    }

    private JsonObject McpCommands() => new()
    {
        ["commands"] = new JsonArray(_commands.Select(command => (JsonNode?)new JsonObject
        {
            ["id"] = command.Id,
            ["label"] = command.Label,
            ["group"] = command.Group,
            ["shortcut"] = command.Shortcut,
            ["enabled"] = !_disposed && !_closing && !_dialogVisible && (command.CanExecute?.Invoke() ?? true),
            ["checked"] = command.IsChecked?.Invoke(),
            ["requiresInteraction"] = McpCommandNeedsInteraction(command),
            ["availableOverMcp"] = command.Id != "mcp-connection",
            ["unavailableReason"] = command.Id == "mcp-connection" ? "Connection lifecycle controls are local to UnoDraw." : null
        }).ToArray())
    };

    private async Task<JsonObject> RunMcpCommandAsync(EditorMcpArguments a, CancellationToken cancellationToken)
    {
        var id = a.Text("id", required: true);
        if (id == "mcp-connection")
            throw new EditorMcpException("Connection lifecycle controls are local to UnoDraw; an MCP request cannot change or close its own connection.");
        var command = _commands.FirstOrDefault(item => item.Id == id) ?? throw new EditorMcpException("Unknown editor command: " + id);
        if (command.CanExecute?.Invoke() == false)
            throw new EditorMcpException("The editor command is disabled in the current state: " + id);
        if (McpCommandNeedsInteraction(command) && !a.Bool("allowInteraction"))
            throw new EditorMcpException("This command needs native interaction. Use a parameterized MCP tool or set allowInteraction=true.");
        // Tool aliases are resolved directly so nested RunCommandAsync does not
        // convert a machine-visible error into an unrelated error dialog.
        if (s_editorTools.FirstOrDefault(entry => entry.Id == id)?.Action is { } action)
            command = _commands.First(item => item.Id == action);
        var previousCancellation = s_mcpInteractionCancellation.Value;
        s_mcpInteractionCancellation.Value = cancellationToken;
        try
        {
            await command.Execute();
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally { s_mcpInteractionCancellation.Value = previousCancellation; }
        if (!_disposed)
        { RefreshWorkspace(); RefreshEditorMenus(); }
        return new JsonObject { ["command"] = id, ["completed"] = true, ["document"] = _current is { } current ? McpDocumentInfo(current) : null };
    }

    private JsonObject McpTools() => new()
    {
        ["tools"] = new JsonArray(s_editorTools.Select(entry => (JsonNode?)new JsonObject
        {
            ["id"] = entry.Id,
            ["label"] = entry.Label,
            ["tool"] = entry.Tool?.ToString(),
            ["mode"] = entry.Mode.ToString(),
            ["action"] = entry.Action,
            ["active"] = IsActiveEditorTool(entry),
            ["shortcut"] = EditorShortcutText(entry.Id)
        }).ToArray()),
        ["options"] = McpToolOptions(new EditorMcpArguments(new JsonObject()))
    };

    private JsonObject SelectMcpTool(EditorMcpArguments a)
    {
        var entry = s_editorTools.FirstOrDefault(tool => tool.Id == a.Text("id", required: true)) ?? throw new EditorMcpException("Unknown editor tool.");
        if (entry.Action is not null)
            throw new EditorMcpException("This tool opens an editor action. Use a parameterized tool or commands_run with allowInteraction=true.");
        if (a.Has("symbolId"))
        {
            var id = a.Text("symbolId");
            if (Controller?.Document?.GetElementById(id) is not SvgSymbol)
                throw new EditorMcpException("The symbol does not exist.");
            Controller.Tools.SymbolId = id;
        }
        if (entry.Mode == SvgEditorInteractionMode.SymbolSprayer && Controller?.Document?.GetElementById(Controller.Tools.SymbolId ?? string.Empty) is not SvgSymbol)
            throw new EditorMcpException("Select a symbolId before activating the symbol sprayer.");
        SelectEditorTool(entry);
        return McpTools();
    }

    private JsonObject McpToolOptions(EditorMcpArguments a)
    {
        var current = new JsonObject
        {
            ["eraserDiameter"] = Canvas.EraserDiameter,
            ["smoothDiameter"] = Canvas.SmoothDiameter,
            ["smoothStrength"] = Canvas.SmoothStrength,
            ["pathEraserDiameter"] = Canvas.PathEraserDiameter,
            ["joinDiameter"] = Canvas.JoinDiameter,
            ["blobDiameter"] = Canvas.BlobBrushDiameter,
            ["blobMerge"] = Canvas.BlobBrushMerge,
            ["pencilFidelity"] = Canvas.PencilFidelity,
            ["pencilSmooth"] = Canvas.PencilSmooth,
            ["shapeDivisions"] = Canvas.ShapeDivisions,
            ["cornerRadius"] = Canvas.RoundedRectangleRadiusRatio,
            ["innerRadius"] = Canvas.StarInnerRadiusRatio,
            ["strokeTarget"] = Canvas.StrokePaintTarget,
            ["lassoFullyContained"] = Canvas.LassoFullyContained,
            ["deformationRadius"] = Canvas.DeformationOptions.Radius,
            ["deformationStrength"] = Canvas.DeformationOptions.Strength,
            ["deformationDetail"] = Canvas.DeformationOptions.Detail,
            ["deformationFlatness"] = Canvas.DeformationOptions.Flatness,
            ["deformationDwellRate"] = Canvas.DeformationOptions.DwellRate,
            ["symbolRadius"] = Canvas.SymbolBrushOptions.Radius,
            ["symbolSize"] = Canvas.SymbolBrushOptions.Size,
            ["symbolDensity"] = Canvas.SymbolBrushOptions.Density,
            ["symbolStrength"] = Canvas.SymbolBrushOptions.Strength,
            ["symbolSizeVariation"] = Canvas.SymbolBrushOptions.SizeVariation,
            ["symbolRotationVariation"] = Canvas.SymbolBrushOptions.RotationVariation,
            ["symbolSeed"] = Canvas.SymbolBrushOptions.Seed,
            ["wandMatchFill"] = Canvas.MagicWandOptions.MatchFill,
            ["wandMatchStroke"] = Canvas.MagicWandOptions.MatchStroke,
            ["wandMatchStrokeWidth"] = Canvas.MagicWandOptions.MatchStrokeWidth,
            ["wandMatchOpacity"] = Canvas.MagicWandOptions.MatchOpacity,
            ["wandColorTolerance"] = Canvas.MagicWandOptions.ColorTolerance,
            ["wandStrokeWidthTolerance"] = Canvas.MagicWandOptions.StrokeWidthTolerance,
            ["wandOpacityTolerance"] = Canvas.MagicWandOptions.OpacityTolerance
        };
        if (a.Object("options") is not { } options)
            return current;
        var changes = new List<Action>();
        var values = new EditorMcpArguments(options);
        foreach (var (key, value) in options)
        {
            if (!current.ContainsKey(key) || value is null)
                throw new EditorMcpException("Unknown or null tool option: " + key);
            void Number(Action<float> set, float min, float max)
            { var number = values.Float(key, minimum: min, maximum: max); changes.Add(() => set(number)); }
            void Integer(Action<int> set, int min, int max)
            { var number = values.Int(key, minimum: min, maximum: max); changes.Add(() => set(number)); }
            void Boolean(Action<bool> set)
            { var flag = values.Bool(key); changes.Add(() => set(flag)); }
            switch (key)
            {
                case "eraserDiameter":
                    Number(v => Canvas.EraserDiameter = v, 0.1f, 10000);
                    break;
                case "smoothDiameter":
                    Number(v => Canvas.SmoothDiameter = v, 0.1f, 10000);
                    break;
                case "smoothStrength":
                    Number(v => Canvas.SmoothStrength = v, 0, 1);
                    break;
                case "pathEraserDiameter":
                    Number(v => Canvas.PathEraserDiameter = v, 0.1f, 10000);
                    break;
                case "joinDiameter":
                    Number(v => Canvas.JoinDiameter = v, 0.1f, 10000);
                    break;
                case "blobDiameter":
                    Number(v => Canvas.BlobBrushDiameter = v, 0.1f, 10000);
                    break;
                case "blobMerge":
                    Boolean(v => Canvas.BlobBrushMerge = v);
                    break;
                case "pencilFidelity":
                    Number(v => Canvas.PencilFidelity = v, 0, 1);
                    break;
                case "pencilSmooth":
                    Boolean(v => Canvas.PencilSmooth = v);
                    break;
                case "shapeDivisions":
                    Integer(v => Canvas.ShapeDivisions = v, 3, 128);
                    break;
                case "cornerRadius":
                    Number(v => Canvas.RoundedRectangleRadiusRatio = v, 0, 0.5f);
                    break;
                case "innerRadius":
                    Number(v => Canvas.StarInnerRadiusRatio = v, 0.01f, 0.99f);
                    break;
                case "strokeTarget":
                    Boolean(value => { SetActiveEditorPaint(value); });
                    break;
                case "lassoFullyContained":
                    Boolean(v => Canvas.LassoFullyContained = v);
                    break;
                case "deformationRadius":
                    Number(v => Canvas.DeformationOptions.Radius = v, 0.1f, 5000);
                    break;
                case "deformationStrength":
                    Number(v => Canvas.DeformationOptions.Strength = v, -1, 1);
                    break;
                case "deformationDetail":
                    Integer(v => Canvas.DeformationOptions.Detail = v, 2, 64);
                    break;
                case "deformationFlatness":
                    Number(v => Canvas.DeformationOptions.Flatness = v, 0.05f, 8);
                    break;
                case "deformationDwellRate":
                    Number(v => Canvas.DeformationOptions.DwellRate = v, 0, 20);
                    break;
                case "symbolRadius":
                    Number(v => Canvas.SymbolBrushOptions.Radius = v, 0.1f, 5000);
                    break;
                case "symbolSize":
                    Number(v => Canvas.SymbolBrushOptions.Size = v, 0.1f, 10000);
                    break;
                case "symbolDensity":
                    Integer(v => Canvas.SymbolBrushOptions.Density = v, 1, 100);
                    break;
                case "symbolStrength":
                    Number(v => Canvas.SymbolBrushOptions.Strength = v, 0.01f, 1);
                    break;
                case "symbolSizeVariation":
                    Number(v => Canvas.SymbolBrushOptions.SizeVariation = v, 0, 0.95f);
                    break;
                case "symbolRotationVariation":
                    Number(v => Canvas.SymbolBrushOptions.RotationVariation = v, 0, 180);
                    break;
                case "symbolSeed":
                    Integer(v => Canvas.SymbolBrushOptions.Seed = v, int.MinValue, int.MaxValue);
                    break;
                case "wandMatchFill":
                    Boolean(v => Canvas.MagicWandOptions.MatchFill = v);
                    break;
                case "wandMatchStroke":
                    Boolean(v => Canvas.MagicWandOptions.MatchStroke = v);
                    break;
                case "wandMatchStrokeWidth":
                    Boolean(v => Canvas.MagicWandOptions.MatchStrokeWidth = v);
                    break;
                case "wandMatchOpacity":
                    Boolean(v => Canvas.MagicWandOptions.MatchOpacity = v);
                    break;
                case "wandColorTolerance":
                    Number(v => Canvas.MagicWandOptions.ColorTolerance = v, 0, 255);
                    break;
                case "wandStrokeWidthTolerance":
                    Number(v => Canvas.MagicWandOptions.StrokeWidthTolerance = v, 0, 10000);
                    break;
                case "wandOpacityTolerance":
                    Number(v => Canvas.MagicWandOptions.OpacityTolerance = v, 0, 1);
                    break;
            }
        }
        foreach (var change in changes)
            change();
        return McpToolOptions(new EditorMcpArguments(new JsonObject()));
    }

    private JsonObject McpView() => new()
    {
        ["zoom"] = Canvas.Zoom,
        ["offset"] = new JsonArray(Canvas.Viewport.OffsetX, Canvas.Viewport.OffsetY),
        ["size"] = new JsonArray(Canvas.Viewport.Width, Canvas.Viewport.Height),
        ["grid"] = Canvas.ShowGrid,
        ["snapGrid"] = Controller?.Session.Settings.SnapToGrid,
        ["gridSize"] = Controller?.Session.Settings.GridSize,
        ["guides"] = Canvas.GuidesVisible,
        ["guidesLocked"] = Canvas.GuidesLocked,
        ["snapGuides"] = Canvas.SnapToGuides,
        ["smartGuides"] = Canvas.SmartGuides,
        ["transparency"] = Canvas.ShowTransparency,
        ["wireframe"] = Controller?.Session.Settings.WireframeEnabled,
        ["filters"] = Controller?.Session.Settings.FiltersDisabled == false,
        ["theme"] = RequestedTheme.ToString()
    };

    private JsonObject SetMcpView(EditorMcpArguments a)
    {
        var controller = Controller ?? throw new EditorMcpException("No document is open.");
        var zoom = a.Number("zoom", Canvas.Zoom, 0.01, 128);
        var gridSize = a.Number("gridSize", controller.Session.Settings.GridSize, 0.01, 100000);
        var center = a.Has("center") ? a.Point("center") : (SKPoint?)null;
        var theme = a.Enum("theme", RequestedTheme);
        if (a.Has("fit"))
            switch (a.Text("fit"))
            {
                case "document":
                    Canvas.FitDocument();
                    break;
                case "artboard":
                    Canvas.FitActiveArtboard();
                    break;
                case "selection":
                    Canvas.FitSelection();
                    break;
                default:
                    throw new EditorMcpException("Unknown viewport fit target.");
            }
        if (a.Has("zoom"))
            Canvas.Viewport.SetZoomAt(zoom, new SKPoint((float)Canvas.Viewport.Width / 2, (float)Canvas.Viewport.Height / 2));
        if (center is { } point)
        {
            var projected = Canvas.Viewport.WorldToView(point);
            Canvas.Viewport.Pan(Canvas.Viewport.Width / 2 - projected.X, Canvas.Viewport.Height / 2 - projected.Y);
        }
        if (a.Has("grid"))
            Canvas.ShowGrid = controller.Session.Settings.ShowGrid = a.Bool("grid");
        if (a.Has("snapGrid"))
        { controller.Selection.SnapToGrid = controller.Session.Settings.SnapToGrid = a.Bool("snapGrid"); SnapToggle.IsChecked = a.Bool("snapGrid"); }
        if (a.Has("gridSize"))
            controller.Session.Settings.GridSize = gridSize;
        if (a.Has("guides"))
            Canvas.GuidesVisible = a.Bool("guides");
        if (a.Has("guidesLocked"))
            Canvas.GuidesLocked = a.Bool("guidesLocked");
        if (a.Has("snapGuides"))
            Canvas.SnapToGuides = a.Bool("snapGuides");
        if (a.Has("smartGuides"))
            Canvas.SmartGuides = a.Bool("smartGuides");
        if (a.Has("transparency"))
            Canvas.ShowTransparency = a.Bool("transparency");
        if (a.Has("wireframe"))
            controller.Session.Settings.WireframeEnabled = a.Bool("wireframe");
        if (a.Has("filters"))
            controller.Session.Settings.FiltersDisabled = !a.Bool("filters");
        if (a.Has("theme"))
        { RequestedTheme = theme; Canvas.IsDarkTheme = theme == ElementTheme.Dark || theme == ElementTheme.Default && ActualTheme == ElementTheme.Dark; }
        return McpView();
    }

    private JsonObject McpWorkspace() => new()
    {
        ["xml"] = CaptureWorkspaceLayout(),
        ["panels"] = new JsonArray(_studioPanels.Select(pair => (JsonNode?)new JsonObject { ["id"] = pair.Key, ["title"] = pair.Value.Title, ["visible"] = IsStudioPanelVisible(pair.Key) }).ToArray()),
        ["presets"] = new JsonArray(Enum.GetNames<SvgWorkspacePreset>().Select(name => (JsonNode?)JsonValue.Create(name)).ToArray())
    };

    private JsonObject SetMcpWorkspace(EditorMcpArguments a)
    {
        if (new[] { "xml", "preset", "panel" }.Count(a.Has) != 1)
            throw new EditorMcpException("Supply exactly one workspace XML, preset, or panel.");
        if (a.Has("xml"))
            RestoreWorkspaceLayout(a.Text("xml", required: true, maximum: 1048576));
        else if (a.Has("preset"))
            ResetStudioLayout(a.Enum("preset", SvgWorkspacePreset.Essentials));
        else
        {
            var panel = a.Text("panel", required: true);
            if (!_studioPanels.ContainsKey(panel))
                throw new EditorMcpException("Unknown workspace panel.");
            OpenStudioPanel(panel);
        }
        return McpWorkspace();
    }

    private JsonObject McpShortcuts() => new()
    {
        ["xml"] = _editorShortcuts.Serialize(),
        ["name"] = _editorShortcuts.Name,
        ["commands"] = new JsonArray(_commands.Select(command => (JsonNode?)new JsonObject
        {
            ["id"] = command.Id,
            ["shortcut"] = command.Shortcut,
            ["customized"] = _editorShortcuts.IsCustomized(command.Id)
        }).ToArray())
    };

    private JsonObject SetMcpShortcuts(EditorMcpArguments a)
    {
        if (a.Bool("reset") && a.Has("xml"))
            throw new EditorMcpException("Choose a profile or reset, not both.");
        var profile = a.Bool("reset") ? new SvgEditorShortcutProfile() : SvgEditorShortcutProfile.Parse(a.Text("xml", required: true, maximum: SvgEditorShortcutProfile.MaximumCharacters));
        var xml = profile.Serialize();
        ApplicationData.Current.LocalSettings.Values[EditorShortcutStorageKey] = xml;
        _editorShortcuts = profile;
        _editorShortcutStorageSource = xml;
        ApplyEditorShortcutMetadata();
        RefreshEditorMenus();
        return McpShortcuts();
    }
}
