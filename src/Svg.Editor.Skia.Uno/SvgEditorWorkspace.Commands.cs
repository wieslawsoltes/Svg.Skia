using Microsoft.UI.Xaml;
using SkiaSharp;
using Svg.Editor.Core;
using Svg.Editor.Svg;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private void BuildCommands()
    {
        void Add(string id, string label, string group, Action execute)
            => _commands.Add(new EditorCommand(id, label, group, EditorShortcutText(id), () => { execute(); return Task.CompletedTask; }));
        void AddAsync(string id, string label, string group, Func<Task> execute)
            => _commands.Add(new EditorCommand(id, label, group, EditorShortcutText(id), execute));

        AddAsync("new", "New document…", "File", NewDocumentDialogAsync);
        AddAsync("open", "Open SVG…", "File", OpenPickerAsync);
        AddAsync("save", "Save", "File", async () => { if (_current is { } d) await SaveAsync(d); });
        AddAsync("saveas", "Save as…", "File", async () => { if (_current is { } d) await SaveAsync(d, true); });
        AddAsync("savecompressed", "Save compressed SVG…", "File", async () => { if (_current is { } d) await SaveAsync(d, true, ".svgz"); });
        AddAsync("export", "Export image or PDF…", "File", ExportAsync);
        AddAsync("exportselection", "Export selection…", "File", () => ExportAsync(true));
        AddAsync("print", "Print and page setup…", "File", PrintDialogAsync);
        AddAsync("preflight", "Preflight document…", "File", PreflightDialogAsync);
        AddAsync("image", "Place image…", "File", PlaceImageAsync);
        AddAsync("source", "Edit SVG source…", "File", EditSourceAsync);

        Add("undo", "Undo", "Edit", () => Controller?.Undo());
        Add("redo", "Redo", "Edit", () => Controller?.Redo());
        AddAsync("cut", "Cut", "Edit", () => CopyAsync(true));
        AddAsync("copy", "Copy", "Edit", () => CopyAsync(false));
        AddAsync("paste", "Paste", "Edit", PasteAsync);
        Add("duplicate", "Duplicate", "Edit", () => Controller?.DuplicateSelection());
        Add("delete", "Delete", "Edit", () =>
        {
            if (Canvas.Tool == SvgEditorToolKind.PathSelect && Canvas.ActiveNodeIndex >= 0)
                Canvas.DeleteActiveNode();
            else
                Controller?.DeleteSelection();
        });
        Add("selectall", "Select all", "Edit", () => Controller?.SelectAll());
        Add("deselect", "Clear selection", "Edit", () => { Canvas.CancelGesture(); Controller?.ClearSelection(); });
        AddAsync("text", "Edit text…", "Edit", EditTextAsync);
        AddAsync("textframes", "Paragraphs and linked text frames…", "Edit", TextFramesDialogAsync);
        AddAsync("insert", "Insert SVG element…", "Edit", InsertElementAsync);

        Add("group", "Group", "Object", () => Controller?.GroupSelection());
        Add("ungroup", "Ungroup", "Object", () => Controller?.UngroupSelection());
        Add("forward", "Bring forward", "Object", () => Controller?.BringForward());
        Add("backward", "Send backward", "Object", () => Controller?.SendBackward());
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
        AddAsync("artboard", "Document setup…", "File", ArtboardDialogAsync);
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
        AddAsync("divide", "Divide…", "Path", DivideSelectionAsync);
        Add("convertpath", "Convert to paths", "Path", () => Controller?.ConvertSelectionToPaths());
        Add("simplify", "Simplify path", "Path", () => Controller?.SimplifySelection());
        AddAsync("offset", "Offset path…", "Path", OffsetPathAsync);
        AddAsync("strokeprofile", "Variable width stroke…", "Path", StrokeProfileDialogAsync);
        AddAsync("brushes", "Brush library…", "Path", BrushLibraryDialogAsync);
        AddAsync("blend", "Blend paths…", "Path", BlendPathsAsync);
        Add("smooth", "Smooth active node", "Path", () => EditNode(true));
        Add("corner", "Corner active node", "Path", () => EditNode(false));
        Add("finishpath", "Finish path", "Path", Canvas.FinishPath);

        foreach (var alignment in Enum.GetValues<AlignService.AlignType>())
            Add($"align{alignment}", $"Align {alignment}", "Arrange", () => Controller?.Align(alignment));
        foreach (var direction in Enum.GetValues<AlignService.DistributeType>())
            Add($"distribute{direction}", $"Distribute {direction}", "Arrange", () => Controller?.Distribute(direction));

        Add("fit", "Fit artboard", "View", Canvas.FitActiveArtboard);
        Add("layers", "Layers and objects…", "View", () => ShowWorkspacePanel(OutlinePanel, "Layers and objects"));
        Add("inspector", "Object inspector…", "View", () => ShowWorkspacePanel(InspectorPanel, "Object inspector"));
        Add("fitselection", "Fit selection", "View", Canvas.FitSelection);
        Add("zoomin", "Zoom in", "View", Canvas.ZoomIn);
        Add("zoomout", "Zoom out", "View", Canvas.ZoomOut);
        Add("zoom100", "Actual size", "View", Canvas.ResetZoom);
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
            c.Session.Settings.WireframeEnabled = !c.Renderer.Wireframe;
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
        Add("zen", "Focus canvas", "View", () => { _zenMode = !_zenMode; UpdateResponsiveLayout(); });
        AddAsync("commands", "Search commands…", "View", ShowCommandsAsync);
        AddAsync("help", "Keyboard shortcuts…", "View", ShowHelpAsync);
        BuildProductCommands();
    }

    partial void BuildProductCommands();
}
