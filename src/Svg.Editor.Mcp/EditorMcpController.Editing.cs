using System.Text.Json.Nodes;
using SkiaSharp;
using Svg.Editor.Skia;
using Svg.Editor.Svg;

namespace Svg.Editor.Mcp;

public sealed partial class EditorMcpController
{
    private void RequireSelection(int count = 1)
    { if (_controller.SelectedElements.Count < count) throw new EditorMcpException($"Select at least {count} object(s) first."); }

    private JsonObject Edit(EditorMcpArguments a)
    {
        var action = a.Text("action", required: true);
        if (action is not ("undo" or "redo" or "paste" or "unlockAll" or "showAll"))
            RequireSelection();
        switch (action)
        {
            case "undo":
                if (!_controller.CanUndo)
                    throw new EditorMcpException("There is no edit to undo.");
                _controller.Undo();
                break;
            case "redo":
                if (!_controller.CanRedo)
                    throw new EditorMcpException("There is no edit to redo.");
                _controller.Redo();
                break;
            case "delete":
                _controller.DeleteSelection();
                break;
            case "duplicate":
                _controller.DuplicateSelection(a.Float("dx", 16), a.Float("dy", 16));
                break;
            case "group":
                _controller.GroupSelection();
                break;
            case "ungroup":
                _controller.UngroupSelection();
                break;
            case "front":
                _controller.BringToFront();
                break;
            case "back":
                _controller.SendToBack();
                break;
            case "forward":
                _controller.BringForward();
                break;
            case "backward":
                _controller.SendBackward();
                break;
            case "flipHorizontal":
                _controller.FlipHorizontal();
                break;
            case "flipVertical":
                _controller.FlipVertical();
                break;
            case "repeatTransform":
                if (!_controller.CanRepeatTransform)
                    throw new EditorMcpException("No repeatable transform is available.");
                _controller.RepeatTransform();
                break;
            case "unlockAll":
                _controller.UnlockAll();
                break;
            case "showAll":
                _controller.ShowAll();
                break;
            case "convertPath":
                _controller.ConvertSelectionToPaths();
                break;
            case "simplify":
                _controller.SimplifySelection();
                break;
            case "clip":
                RequireSelection(2);
                _controller.CreateClippingMask();
                break;
            case "unclip":
                _controller.SetAttribute("clip-path", "none");
                break;
            case "alphaMask":
                RequireSelection(2);
                _controller.CreateOpacityMask(false);
                break;
            case "luminanceMask":
                RequireSelection(2);
                _controller.CreateOpacityMask(true);
                break;
            case "releaseMask":
                _controller.ReleaseOpacityMasks();
                break;
            case "union":
                RequireSelection(2);
                _controller.ApplyBoolean(SKPathOp.Union);
                break;
            case "subtract":
                RequireSelection(2);
                _controller.ApplyBoolean(SKPathOp.Difference);
                break;
            case "intersect":
                RequireSelection(2);
                _controller.ApplyBoolean(SKPathOp.Intersect);
                break;
            case "exclude":
                RequireSelection(2);
                _controller.ApplyBoolean(SKPathOp.Xor);
                break;
            case "divide":
                RequireSelection(2);
                _controller.DivideSelection();
                break;
            case "copy":
                return new JsonObject { ["svg"] = _controller.CopySelection(), ["revision"] = _controller.DocumentRevision };
            case "paste":
                var svg = a.Has("svg") ? a.Text("svg", maximum: MaximumSourceCharacters) : null;
                if (svg is not null)
                    ValidateSvg(svg);
                _controller.Paste(svg, a.Float("dx", 16), a.Float("dy", 16));
                break;
            case "createSymbol":
                _controller.CreateSymbol(a.Text("id", required: true));
                break;
            default:
                throw new EditorMcpException("Unknown edit action.");
        }
        return DocumentState();
    }

    private JsonObject Batch(EditorMcpArguments a, CancellationToken cancellationToken)
    {
        var operations = a.Array("operations", maximum: 100);
        if (operations.Count == 0)
            throw new EditorMcpException("A batch needs at least one operation.");
        if (!a.Has("expectedRevision"))
            throw new EditorMcpException("An atomic batch requires expectedRevision.");
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "unodraw_element_insert", "unodraw_element_update", "unodraw_element_delete", "unodraw_element_rename", "unodraw_element_reparent",
            "unodraw_selection_set", "unodraw_transform", "unodraw_draw", "unodraw_paint", "unodraw_text", "unodraw_resource_import"
        };
        var calls = operations.Select(node =>
        {
            var call = node as JsonObject ?? throw new EditorMcpException("Every batch operation must be an object.");
            var name = call["name"]?.GetValue<string>() ?? throw new EditorMcpException("A batch operation needs a name.");
            if (!allowed.Contains(name))
                throw new EditorMcpException("Operation is not supported inside an atomic batch: " + name);
            var args = call["arguments"]?.DeepClone() as JsonObject ?? throw new EditorMcpException("A batch operation needs arguments.");
            if (args.ContainsKey("documentId"))
                throw new EditorMcpException("Batch operations must stay in the target document.");
            if (args["expectedRevision"] is { } requested && EditorMcpArguments.NumericValue(requested) != _controller.DocumentRevision)
                throw new EditorMcpException("A batch operation has a stale expectedRevision.");
            args["expectedRevision"] = _controller.DocumentRevision;
            return (name, args);
        }).ToArray();
        if (_controller.IsEditing)
            throw new EditorMcpException("Finish the current edit before starting a batch.");
        var fill = _controller.CurrentFill;
        var stroke = _controller.CurrentStroke;
        var width = _controller.CurrentStrokeWidth;
        var results = new JsonArray();
        _controller.BeginEdit(a.Text("label", "MCP: batch edit"));
        try
        {
            foreach (var (name, args) in calls)
            { cancellationToken.ThrowIfCancellationRequested(); results.Add(Invoke(name, args, cancellationToken)); }
            _controller.CommitEdit();
        }
        catch
        {
            _controller.CancelEdit();
            _controller.CurrentFill = fill;
            _controller.CurrentStroke = stroke;
            _controller.CurrentStrokeWidth = width;
            _controller.Tools.CurrentStrokeWidth = width;
            throw;
        }
        return new JsonObject { ["results"] = results, ["revision"] = _controller.DocumentRevision };
    }

    private JsonObject Transform(EditorMcpArguments a)
    {
        RequireSelection();
        var dx = a.Float("dx");
        var dy = a.Float("dy");
        var degrees = a.Float("degrees");
        SKMatrix? matrix = null;
        if (a.Has("matrix"))
        {
            var values = a.Array("matrix", maximum: 6);
            if (values.Count != 6)
                throw new EditorMcpException("An affine matrix needs six values.");
            var parts = values.Select(node => (float)EditorMcpArguments.NumericValue(node)).ToArray();
            if (parts.Any(value => !float.IsFinite(value) || Math.Abs(value) > 1000000))
                throw new EditorMcpException("Matrix components must be finite and bounded.");
            matrix = new SKMatrix(parts[0], parts[2], parts[4], parts[1], parts[3], parts[5], 0, 0, 1);
        }
        _controller.Execute("MCP: transform selection", () =>
        {
            if (dx != 0 || dy != 0)
                _controller.MoveSelection(dx, dy);
            if (degrees != 0)
                _controller.RotateSelection(degrees);
            if (matrix is { } affine)
                _controller.TransformSelection(affine);
        });
        return SelectionState();
    }

    private JsonObject Arrange(EditorMcpArguments a)
    {
        RequireSelection();
        var key = a.Has("keyObject") ? Target(a, "keyObject") as SvgVisualElement ?? throw new EditorMcpException("The key object must be visual.") : null;
        switch (a.Text("action", required: true))
        {
            case "align":
                _controller.AlignSelection(a.Enum("alignment", SvgAlignment.Left), a.Enum("target", SvgAlignmentTarget.Selection), key);
                break;
            case "distribute":
                _controller.DistributeSelectionCenters(a.Enum("axis", SvgArrangementAxis.Horizontal));
                break;
            case "space":
                _controller.SpaceSelection(a.Enum("axis", SvgArrangementAxis.Horizontal), a.Has("gap") ? a.Number("gap") : null, key);
                break;
            case "center":
                _controller.CenterSelectionOnArtboard();
                break;
            default:
                throw new EditorMcpException("Unknown arrangement action.");
        }
        return SelectionState();
    }

    private JsonObject Draw(EditorMcpArguments a)
    {
        var shape = a.Text("shape", required: true);
        var start = a.Point("start");
        var end = a.Point("end");
        if (shape is "Rect" or "Ellipse" or "Circle" or "Line" or "Text")
            _controller.CreateElement(System.Enum.Parse<ToolService.Tool>(shape), start, end, true);
        else
        {
            var mode = a.Enum("shape", SvgEditorInteractionMode.RoundedRectangle);
            if (mode is not (SvgEditorInteractionMode.RoundedRectangle or SvgEditorInteractionMode.RegularPolygon or SvgEditorInteractionMode.Star or SvgEditorInteractionMode.Spiral or SvgEditorInteractionMode.RectangularGrid or SvgEditorInteractionMode.PolarGrid))
                throw new EditorMcpException("Unknown drawing shape.");
            _controller.AddEditorShape(mode, start, end, a.Int("divisions", 6, 3, 128), a.Float("cornerRadius", 0.15f, 0, 0.5f), a.Float("innerRadius", 0.45f, 0.01f, 0.99f));
        }
        return SelectionState();
    }

    private JsonObject Paint(EditorMcpArguments a)
    {
        var fill = a.Has("fill") ? a.Text("fill") : null;
        var stroke = a.Has("stroke") ? a.Text("stroke") : null;
        var width = a.Has("strokeWidth") ? a.Float("strokeWidth", minimum: 0) : (float?)null;
        var opacity = a.Has("opacity") ? a.Float("opacity", minimum: 0, maximum: 1) : (float?)null;
        var attributes = a.Object("attributes");
        if (attributes?.ContainsKey("id") == true)
            throw new EditorMcpException("Use element_rename to change IDs.");
        var oldFill = _controller.CurrentFill;
        var oldStroke = _controller.CurrentStroke;
        var oldWidth = _controller.CurrentStrokeWidth;
        try
        {
            _controller.Execute("MCP: change appearance", () =>
            {
                if (fill is not null)
                    _controller.SetFill(fill);
                if (stroke is not null)
                    _controller.SetStroke(stroke);
                if (width is { } newWidth)
                    _controller.SetStrokeWidth(newWidth);
                if (opacity is { } newOpacity)
                { RequireSelection(); _controller.SetOpacity(newOpacity); }
                if (attributes is not null)
                {
                    RequireSelection();
                    foreach (var (name, value) in attributes)
                        _controller.SetAttribute(name, value?.GetValue<string>() ?? throw new EditorMcpException("Appearance attributes require string values."));
                }
            });
        }
        catch { _controller.CurrentFill = oldFill; _controller.CurrentStroke = oldStroke; _controller.CurrentStrokeWidth = oldWidth; _controller.Tools.CurrentStrokeWidth = oldWidth; throw; }
        return DocumentState();
    }

    private JsonObject Gradient(EditorMcpArguments a)
    {
        RequireSelection();
        var stops = a.Array("stops", maximum: 256);
        if (stops.Count < 2)
            throw new EditorMcpException("A gradient requires at least two stops.");
        var ramp = new SvgGradientRamp(stops.Select(node =>
        {
            var stop = new EditorMcpArguments(node as JsonObject ?? throw new EditorMcpException("Invalid gradient stop."));
            if (!SKColor.TryParse(stop.Text("color", required: true), out var color))
                throw new EditorMcpException("Invalid gradient stop color.");
            return (stop.Number("offset", minimum: 0, maximum: 1), color, stop.Number("opacity", 1, 0, 1));
        }));
        var edit = _controller.BeginGradientEdit(a.Bool("stroke"));
        edit.Ramp = ramp;
        edit.Spread = a.Enum("spread", SvgGradientSpread.Pad);
        edit.UseObjectBounds(a.Enum("kind", SvgGradientKind.Linear), a.Number("angle"));
        _controller.ApplyGradientEdit(edit, a.Bool("editShared"));
        return DocumentState();
    }

    private JsonObject Filter(EditorMcpArguments a)
    {
        RequireSelection();
        if (a.Bool("remove"))
            _controller.RemoveSelectionFilters();
        else
        {
            var graph = SvgFilterGraph.Parse(a.Text("xml", required: true, maximum: 1048576));
            graph.ThrowIfInvalid();
            var edit = _controller.BeginFilterEdit();
            edit.Graph = graph;
            _controller.ApplyFilterEdit(edit, a.Bool("editShared"));
        }
        return DocumentState();
    }

    private JsonObject Path(EditorMcpArguments a, CancellationToken cancellationToken)
    {
        var action = a.Text("action", required: true);
        if (action != "blob")
            RequireSelection();
        var diameter = a.Float("diameter", 20, 0.01f, 10000);
        var tolerance = a.Float("tolerance", 8, 0.01f, 1000);
        switch (action)
        {
            case "offset":
                _controller.OffsetSelection(a.Float("distance"));
                break;
            case "addAnchor":
                _controller.AddAnchorAt(a.Point("point"), tolerance);
                break;
            case "deleteAnchor":
                _controller.DeleteAnchorAt(a.Point("point"), tolerance);
                break;
            case "convertAnchor":
                using (var preview = _controller.PrepareAnchorConversion(a.Point("point"), a.Has("tangent") ? a.Point("tangent") : null, tolerance))
                { if (preview is null) throw new EditorMcpException("No anchor was found at the requested point."); _controller.ApplyAnchorConversion(preview); }
                break;
            case "cut":
                _controller.CutPathAt(a.Point("point"), tolerance);
                break;
            case "knife":
                _controller.KnifeAlong(a.Points("points"));
                break;
            case "erase":
                _controller.EraseAlong(a.Points("points"), diameter);
                break;
            case "smooth":
            case "pathErase":
            case "join":
                using (var preview = _controller.PreparePathRefinement(action == "smooth" ? SvgPathRefinementKind.Smooth : action == "join" ? SvgPathRefinementKind.Join : SvgPathRefinementKind.Erase,
                    a.Points("points"), diameter / 2, a.Float("strength", 0.5f, 0, 1)))
                    _controller.ApplyPathRefinement(preview);
                break;
            case "blob":
                _controller.AddBlobStroke(a.Points("points"), diameter, a.Bool("merge", true));
                break;
            case "deform":
                using (var preview = _controller.PrepareDeformation(a.Enum("kind", SvgDeformationKind.Warp), a.Points("points"),
                    new SvgDeformationOptions { Radius = diameter / 2, Strength = a.Float("strength", 0.35f, 0, 1) }, cancellationToken))
                    _controller.ApplyDeformation(preview);
                break;
            case "shapeBuilder":
                using (var draft = _controller.BeginShapeBuilder())
                {
                    var indices = a.Has("regions") ? a.Array("regions").Select(node => node!.GetValue<int>()).ToArray()
                        : a.Points("points").Select(draft.HitTest).Where(index => index >= 0).Distinct().ToArray();
                    if (indices.Length == 0)
                        throw new EditorMcpException("No shape-builder regions were selected.");
                    _controller.ApplyShapeBuilder(draft, indices, a.Bool("subtract"));
                }
                break;
            case "nudgeNode":
                if (Target(a) is not SvgVisualElement visual)
                    throw new EditorMcpException("A visual path is required.");
                _controller.NudgeNode(visual, a.Int("nodeIndex", minimum: 0, maximum: 32768), a.Point("delta"));
                break;
            default:
                throw new EditorMcpException("Unknown path action.");
        }
        return DocumentState();
    }
}
