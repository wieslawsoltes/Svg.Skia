using System.Text.Json;
using System.Text.Json.Nodes;

namespace Svg.Editor.Mcp;

/// <summary>The explicit, platform-independent automation contract for UnoDraw.</summary>
public static class EditorMcpCatalog
{
    public static IReadOnlyList<EditorMcpTool> Tools { get; } = Build();

    public static EditorMcpTool Get(string name) => Tools.FirstOrDefault(tool => tool.Name == name)
        ?? throw new ArgumentException("Unknown UnoDraw operation: " + name, nameof(name));

    /// <summary>Validates a request before dispatch, including direct browser and embedded-target calls.</summary>
    public static EditorMcpTool ValidateArguments(string name, JsonObject arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var tool = Get(name);
        EditorMcpSchemaValidator.Validate(tool.InputSchema, JsonSerializer.SerializeToElement(arguments, EditorMcpJsonContext.Default.JsonObject));
        return tool;
    }

    private static IReadOnlyList<EditorMcpTool> Build()
    {
        var result = new List<EditorMcpTool>();
        // Grouped tools advertise the most consequential action they can perform.
        // Undo support does not make replacing/removing existing artwork nondestructive.
        var changesExisting = new HashSet<string>(StringComparer.Ordinal)
        {
            "element_update", "element_reparent", "edit", "batch", "transform", "arrange", "path", "paint", "gradient", "filter",
            "artboard_edit", "guide_edit", "text", "text_story", "brush", "mesh", "symbol", "timeline_edit", "print_set",
            "commands_run", "workspace_set", "shortcuts_set"
        };
        void Add(string name, string description, bool readOnly = false, bool destructive = false,
            string[]? required = null, params (string Name, JsonObject Schema)[] properties)
        {
            var fields = new JsonObject
            {
                ["documentId"] = Text("Open document GUID; omitted means the active document."),
                ["expectedRevision"] = Number("Reject stale edits. Required when mutating structural path: element addresses.", 0, long.MaxValue, true)
            };
            foreach (var (key, value) in properties)
                fields[key] = value;
            var schema = new JsonObject { ["type"] = "object", ["properties"] = fields, ["additionalProperties"] = false };
            if (required is { Length: > 0 })
                schema["required"] = Strings(required);
            result.Add(new EditorMcpTool("unodraw_" + name, description, JsonSerializer.SerializeToElement(schema, EditorMcpJsonContext.Default.JsonObject), readOnly, destructive || changesExisting.Contains(name)));
        }
        Add("document_list", "List open documents with stable documentId, name, revision, active and dirty state.", true);
        Add("document_get", "Read the complete editable SVG source, current revision, selection and document metadata.", true);
        Add("document_new", "Create and activate a new SVG document; dimensions are root user units.", properties: [
            ("width", Number("Width", 1, 1000000)), ("height", Number("Height", 1, 1000000)), ("name", Text("Tab name"))]);
        Add("document_open", "Open supplied SVG source as a new tab. Linked resources follow the editor renderer's normal resource policy; self-contained SVG avoids external dependencies.", required: ["svg"], properties: [
            ("svg", Text("Complete SVG document", 4194304)), ("name", Text("Tab name"))]);
        Add("document_activate", "Activate the document identified by documentId.", required: ["documentId"]);
        Add("document_close", "Close a tab. Dirty documents require discardChanges=true; otherwise the tool rejects the close.", destructive: true, properties: [
            ("discardChanges", Boolean("Explicitly discard unsaved changes"))]);
        Add("document_replace", "Atomically replace SVG source as one undoable edit, retaining document identity.", destructive: true, required: ["svg"], properties: [
            ("svg", Text("Complete SVG document", 4194304)), ("label", Text("Undo label"))]);
        Add("document_export", "Export full document, selected artwork, or one artboard. Returns SVG text, native MCP image content for raster images, or an embedded binary resource for PDF/XPS/SVGZ. Preserves native rendering quality; does not mark the document saved.", true, properties: [
            ("format", Choice("Output format", "svg", "svgz", "png", "jpeg", "webp", "pdf", "xps")),
            ("selectionOnly", Boolean("Export selection")), ("artboardId", Text("Export this artboard")),
            ("scale", Number("Raster/vector output scale", 0.01, 16)), ("transparent", Boolean("Transparent image background; JPEG is white"))]);
        Add("element_list", "List document elements with IDs, structural keys, types, parent and lock/visibility metadata. No IDs are generated. offset/limit paginate up to 1000 nodes.", true, properties: [
            ("parent", Element()), ("recursive", Boolean("Include descendants, default true")), ("type", Text("SVG element local name filter")),
            ("offset", Number("Zero-based offset", 0, int.MaxValue, true)), ("limit", Number("Page size", 1, 1000, true))]);
        Add("element_get", "Read one element's complete XML and attributes. Use returned key for subsequent edits.", true, required: ["element"], properties: [("element", Element())]);
        Add("element_insert", "Insert SVG fragment into a container as one undoable edit. Use SVG namespace; duplicate IDs are rejected. Supports all SVG element types.", required: ["xml"], properties: [
            ("xml", Text("One or more SVG elements", 1048576)), ("parent", Element()), ("index", Number("Child insertion index; -1 appends", -1, int.MaxValue, true))]);
        Add("element_update", "Change element attributes, text, or complete element XML atomically. Null attributes remove them. Use element_rename to change IDs and rewrite references. Honors locked branches.", required: ["element"], properties: [
            ("element", Element()), ("attributes", Map("SVG attribute names to string or null", true)), ("text", Text("Replace child content with text", 1048576)),
            ("xml", Text("Replacement element XML; retain the same ID", 1048576))]);
        Add("element_delete", "Delete the addressed branches atomically, respecting locks.", destructive: true, required: ["elements"], properties: [("elements", Elements())]);
        Add("element_reparent", "Move/reorder branches while preserving world position and references using the editor hierarchy controller.", required: ["elements", "parent"], properties: [
            ("elements", Elements()), ("parent", Element()), ("index", Number("Position after removing moving branches; -1 appends", -1, int.MaxValue, true)),
            ("preserveWorldPosition", Boolean("Compensate parent transform, default true"))]);
        Add("element_rename", "Rename an SVG ID and update document-local references in a single undo step.", required: ["element", "id"], properties: [("element", Element()), ("id", Text("Unique XML ID"))]);
        Add("layer", "Create a native editor layer, change its display name, or set an element's own visibility/lock while retaining selection. Inherited locks must be changed on the parent.", required: ["action"], properties: [
            ("action", Choice("Action", "create", "rename", "visibility", "lock")), ("element", Element()), ("name", Text("Layer display name")),
            ("visible", Boolean("Visibility")), ("locked", Boolean("Own lock"))]);
        Add("selection_get", "Read selected element keys, rendered bounds and revision.", true);
        Add("selection_set", "Select by explicit element keys, point, rectangle, lasso, appearance, all, inverse, or same-property matching. Coordinates are picture units.", properties: [
            ("mode", Choice("Selection mode", "elements", "point", "rectangle", "lasso", "all", "none", "inverse", "matching", "appearance")),
            ("elements", Elements()), ("point", Point()), ("bounds", Bounds()), ("points", Points()), ("additive", Boolean("Retain selection")),
            ("allowLocked", Boolean("Permit selecting locked objects for inspection")), ("match", Choice("Matching property", "Fill", "Stroke", "StrokeWidth", "Opacity", "Appearance", "ObjectType"))]);
        Add("edit", "Run a noninteractive native editing operation on the selection. All artwork changes use existing history and lock rules.", required: ["action"], properties: [
            ("action", Choice("Action", "undo", "redo", "delete", "duplicate", "group", "ungroup", "front", "back", "forward", "backward", "flipHorizontal", "flipVertical", "repeatTransform", "unlockAll", "showAll", "convertPath", "simplify", "clip", "unclip", "alphaMask", "luminanceMask", "releaseMask", "union", "subtract", "intersect", "exclude", "divide", "copy", "paste", "createSymbol")),
            ("svg", Text("SVG to paste; omitted uses internal editor clipboard", 4194304)), ("id", Text("New symbol ID")),
            ("dx", Number("Duplicate/paste offset X")), ("dy", Number("Duplicate/paste offset Y"))]);
        Add("batch", "Apply up to 100 noninteractive document operations atomically as one undo step. No document/UI/history/export or advanced prepared-geometry operations. Any failure restores SVG and selection.", required: ["operations", "expectedRevision"], properties: [
            ("label", Text("Undo label")), ("operations", new JsonObject { ["type"] = "array", ["minItems"] = 1, ["maxItems"] = 100,
                ["items"] = new JsonObject { ["type"] = "object", ["required"] = Strings("name", "arguments"), ["additionalProperties"] = false,
                    ["properties"] = new JsonObject { ["name"] = Text("Catalog tool name"), ["arguments"] = new JsonObject { ["type"] = "object" } } } })]);
        Add("transform", "Move, rotate, or apply an affine SVG matrix [a,b,c,d,e,f] to the selection in picture coordinates.", properties: [
            ("dx", Number("Translation X")), ("dy", Number("Translation Y")), ("degrees", Number("Rotation around selection center")),
            ("matrix", new JsonObject { ["type"] = "array", ["minItems"] = 6, ["maxItems"] = 6, ["items"] = Number("Matrix component") })]);
        Add("arrange", "Align, distribute centers, equalize/set gaps, or center selection on the active artboard.", required: ["action"], properties: [
            ("action", Choice("Action", "align", "distribute", "space", "center")), ("alignment", Choice("Edge", "Left", "HorizontalCenter", "Right", "Top", "VerticalCenter", "Bottom")),
            ("target", Choice("Reference", "Selection", "Artboard", "KeyObject")), ("axis", Choice("Axis", "Horizontal", "Vertical")), ("gap", Number("Gap")), ("keyObject", Element())]);
        Add("draw", "Create native rectangle, ellipse, circle, line, text or procedural editor shapes using current paint; arbitrary paths/images use element_insert.", required: ["shape", "start", "end"], properties: [
            ("shape", Choice("Shape", "Rect", "Ellipse", "Circle", "Line", "Text", "RoundedRectangle", "RegularPolygon", "Star", "Spiral", "RectangularGrid", "PolarGrid")),
            ("start", Point()), ("end", Point()), ("divisions", Number("Sides/grid divisions", 3, 128, true)),
            ("cornerRadius", Number("Rounded corner ratio", 0, 0.5)), ("innerRadius", Number("Star inner radius ratio", 0.01, 0.99))]);
        Add("path", "Edit selected paths with native offset, anchor, scissors, knife, eraser, smooth/join, blob, deformation or shape-builder algorithms.", required: ["action"], properties: [
            ("action", Choice("Operation", "offset", "addAnchor", "deleteAnchor", "convertAnchor", "cut", "knife", "erase", "smooth", "pathErase", "join", "blob", "deform", "shapeBuilder", "nudgeNode")),
            ("point", Point()), ("tangent", Point()), ("points", Points()), ("distance", Number("Offset distance")), ("diameter", Number("Brush diameter", 0.01, 10000)),
            ("strength", Number("Brush strength", 0, 1)), ("tolerance", Number("Hit tolerance", 0.01, 1000)), ("kind", Choice("Deformation", "Warp", "Twirl", "Pucker", "Bloat", "Scallop", "Crystallize", "Wrinkle")),
            ("regions", new JsonObject { ["type"] = "array", ["maxItems"] = 4096, ["items"] = Number("Region index", 0, 4095, true) }),
            ("subtract", Boolean("Subtract shape-builder regions")), ("merge", Boolean("Merge blob with artwork, default true")), ("element", Element()),
            ("nodeIndex", Number("Path point index", 0, 32768, true)), ("delta", Point())]);
        Add("paint", "Set selection fill, stroke, stroke width, opacity and SVG appearance attributes. With no selection fill/stroke become current drawing paints.", properties: [
            ("fill", Text("SVG paint")), ("stroke", Text("SVG paint")), ("strokeWidth", Number("Stroke width", 0, 1000000)), ("opacity", Number("Opacity", 0, 1)),
            ("attributes", Map("Additional SVG appearance attributes"))]);
        Add("gradient", "Apply native linear/radial gradient stops to fill or stroke, with copy-on-write or explicit shared-resource editing.", required: ["stops"], properties: [
            ("kind", Choice("Kind", "Linear", "Radial")), ("stroke", Boolean("Target stroke")), ("angle", Number("Angle degrees")),
            ("spread", Choice("Spread method", "Pad", "Reflect", "Repeat")), ("editShared", Boolean("Edit the shared gradient resource")),
            ("stops", new JsonObject { ["type"] = "array", ["minItems"] = 2, ["maxItems"] = 256, ["items"] = new JsonObject { ["type"] = "object",
                ["required"] = Strings("offset", "color"), ["additionalProperties"] = false, ["properties"] = new JsonObject { ["offset"] = Number("Offset", 0, 1), ["color"] = Text("CSS hexadecimal color"), ["opacity"] = Number("Opacity", 0, 1) } } })]);
        Add("filter", "Apply a validated complete SVG filter graph to the selection or remove selection filters.", properties: [
            ("xml", Text("SVG filter element containing all primitives", 1048576)), ("remove", Boolean("Remove filters")), ("editShared", Boolean("Edit shared filter"))]);
        Add("artboard_list", "List named artboards and root-coordinate bounds, including active artboard.", true);
        Add("artboard_edit", "Create, activate, resize, rename, duplicate, remove or reorder native editor artboards.", required: ["action"], properties: [
            ("action", Choice("Action", "add", "activate", "resize", "rename", "duplicate", "remove", "reorder")), ("id", Text("Artboard ID")), ("name", Text("Artboard name")),
            ("bounds", Bounds()), ("offset", Point()), ("index", Number("New index", 0, 999, true))]);
        Add("guide_list", "List nonprinting native guides in picture coordinates.", true);
        Add("guide_edit", "Add, move, remove or clear guides, or create guides from selection bounds.", required: ["action"], properties: [
            ("action", Choice("Action", "add", "move", "remove", "clear", "selectionBounds")), ("id", Text("Guide ID")), ("origin", Point()), ("direction", Point())]);
        Add("resource_list", "Inspect all defs resources, document CSS, brushes, symbols, gradients, patterns and masks using element keys and XML.", true);
        Add("resource_import", "Import one SVG resource using existing collision-free ID/reference remapping. Returns the actual adopted resource ID.", required: ["xml"], properties: [("xml", Text("One SVG resource element", 1048576))]);
        Add("text", "Edit selected text and typography, or create text on the selected path / in a bounded text area.", properties: [
            ("action", Choice("Action", "set", "onPath", "area")), ("text", Text("Text content", 1048576)), ("fontFamily", Text("Font family")),
            ("fontSize", Number("Font size", 0.01, 100000)), ("letterSpacing", Number("Letter spacing")), ("bounds", Bounds())]);
        Add("text_story", "Read or replace complete paragraph/linked-frame story XML using the native text-story serializer. Set xml to apply; omit to read. An explicit element must belong to a text story. Without an explicit element, XML creates a story when none is selected.", properties: [
            ("element", Element()), ("xml", Text("Native text story XML", 1048576))]);
        Add("brush", "Read, import, remove or apply the native brush library, including variable-width profiles and linked artwork updates.", properties: [
            ("action", Choice("Action", "list", "import", "remove", "apply")), ("xml", Text("Native brush library XML", 1048576)), ("id", Text("Brush ID"))]);
        Add("mesh", "Read or apply native gradient-mesh XML, or create a mesh grid. Mesh data retains editable control points and rendering quality. Setting an explicit element requires a compatible group; omit the element to create a mesh when no group is selected.", properties: [
            ("action", Choice("Action", "get", "set", "create")), ("element", Element()), ("xml", Text("Native gradient mesh XML", 1048576)), ("bounds", Bounds()),
            ("rows", Number("Grid rows", 2, 16, true)), ("columns", Number("Grid columns", 2, 16, true))]);
        Add("symbol", "Spray symbol instances or apply native shift, scrunch, size, spin, stain, screen and style brushes.", required: ["action", "points"], properties: [
            ("action", Choice("Action", "spray", "Shift", "Scrunch", "Size", "Spin", "Stain", "Screen", "Style")), ("id", Text("Symbol ID for spray")),
            ("points", Points()), ("radius", Number("Radius", 1, 10000)), ("size", Number("Instance size", 1, 10000)), ("density", Number("Density", 1, 64, true)),
            ("strength", Number("Strength", 0, 1)), ("seed", Number("Random seed", int.MinValue, int.MaxValue, true)), ("reverse", Boolean("Reverse brush effect")), ("style", Text("Graphic style name"))]);
        Add("timeline_get", "Read native animation tracks and current preview time. XML editing covers every SVG animation attribute.", true);
        Add("timeline_edit", "Set preview time, bake a frame, create/delete tracks, or set keys through the native animation service.", required: ["action"], properties: [
            ("action", Choice("Action", "time", "bake", "create", "delete", "key")), ("seconds", Number("Time", 0, 86400)), ("element", Element()),
            ("track", Number("Track index", 0, 10000, true)), ("attribute", Text("Animated attribute")), ("from", Text("Start value")), ("to", Text("End value")),
            ("duration", Text("SVG clock value")), ("repeatCount", Text("Repeat count or indefinite")), ("offset", Number("Normalized key offset", 0, 1)), ("value", Text("Key value")), ("spline", Text("Cubic key spline"))]);
        Add("preflight", "Inspect missing references, duplicate IDs, fonts, images, text overflow and print readiness with native preflight.", true);
        Add("measure", "Measure a picture-coordinate segment in root SVG units, accounting for viewBox transforms. Does not change selection or artwork.", true,
            required: ["start", "end"], properties: [("start", Point()), ("end", Point())]);
        Add("print_get", "Read complete native print settings XML and physical page layout.", true);
        Add("print_set", "Validate and apply native print settings XML as an undoable document edit. Includes physical sizes, bleed, margins, tiling, marks, RGB/CMYK and ink limits.",
            required: ["xml"], properties: [("xml", Text("Native <print version='1' .../> settings from print_get", 16384))]);
        Add("print_export", "Export physical-page PDF or CMYK/spot separation ZIP using native print settings and quality. Optional base64 ICC profile enables profiled CMYK conversion. Returns binary data; does not print to a device.", true,
            properties: [("format", Choice("Output", "pdf", "separations")), ("profileBase64", Text("ICC output profile bytes", 8388608)),
                ("renderingIntent", Number("ICC intent", 0, 3, true)), ("convertSpotsToProcess", Boolean("Explicitly convert named inks to process colors for PDF"))]);
        Add("commands_list", "Discover every registered editor command, labels, groups, shortcuts, enabled/checked state and interaction requirements.", true);
        Add("commands_run", "Invoke a registered native UI command. Dialog/picker commands require allowInteraction=true and finish when the user completes the interaction.", required: ["id"], properties: [
            ("id", Text("Command ID from commands_list")), ("allowInteraction", Boolean("Allow native dialogs and pickers"))]);
        Add("tools_list", "Discover all editor tools, current tool, modes, and action-based tool behavior.", true);
        Add("tools_select", "Activate a discovered tool. Action-based tools use commands_run with explicit interaction; symbol spray requires a symbol ID.", required: ["id"], properties: [
            ("id", Text("Tool ID from tools_list")), ("symbolId", Text("Symbol for spray/placement"))]);
        Add("tools_options", "Read or set typed options for brushes, pencil, eraser, shape divisions and paint targeting. Omitted options are unchanged.", properties: [
            ("options", new JsonObject { ["type"] = "object", ["description"] = "Keys returned by this tool; number/boolean values" })]);
        Add("view_get", "Read viewport, zoom, grid, guides, transparency, theme and renderer flags.", true);
        Add("view_set", "Set viewport and display settings without changing SVG artwork.", properties: [
            ("zoom", Number("Zoom ratio", 0.01, 128)), ("center", Point()), ("fit", Choice("Fit target", "document", "artboard", "selection")),
            ("grid", Boolean("Grid")), ("snapGrid", Boolean("Grid snapping")), ("gridSize", Number("Grid spacing", 0.01, 100000)),
            ("guides", Boolean("Guides")), ("guidesLocked", Boolean("Lock guides")), ("snapGuides", Boolean("Guide snapping")), ("smartGuides", Boolean("Smart guides")),
            ("transparency", Boolean("Transparency checkerboard")), ("wireframe", Boolean("Outlines")), ("filters", Boolean("Render filters")), ("theme", Choice("Theme", "Dark", "Light", "Default"))]);
        Add("workspace_get", "Read complete serialized workspace layout and available panels/presets.", true);
        Add("workspace_set", "Restore native workspace layout XML, apply a preset, or open a panel.", properties: [
            ("xml", Text("Workspace layout XML", 1048576)), ("preset", Text("Preset from workspace_get")), ("panel", Text("Panel ID from workspace_get"))]);
        Add("shortcuts_get", "Read the complete keyboard profile XML and resolved command shortcuts.", true);
        Add("shortcuts_set", "Validate and persist a native keyboard shortcut profile, or reset to defaults.", properties: [
            ("xml", Text("Keyboard profile XML", 1048576)), ("reset", Boolean("Restore default shortcuts"))]);
        return result.AsReadOnly();
    }

    private static JsonObject Text(string description, int maximum = 4096) => new() { ["type"] = "string", ["description"] = description, ["maxLength"] = maximum };
    private static JsonObject Boolean(string description) => new() { ["type"] = "boolean", ["description"] = description };
    private static JsonObject Number(string description, double minimum = -1000000, double maximum = 1000000, bool integer = false)
        => new() { ["type"] = integer ? "integer" : "number", ["description"] = description, ["minimum"] = minimum, ["maximum"] = maximum };
    private static JsonObject Choice(string description, params string[] values) => new() { ["type"] = "string", ["description"] = description, ["enum"] = Strings(values) };
    private static JsonArray Strings(params string[] values) => new(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
    private static JsonObject Element() => Text("Unique SVG ID, id:<ID>, root, or path:<zero-based child indexes separated by />. Path edits require expectedRevision.");
    private static JsonObject Elements() => new() { ["type"] = "array", ["maxItems"] = 1000, ["items"] = Element() };
    private static JsonObject Point() => new() { ["type"] = "array", ["minItems"] = 2, ["maxItems"] = 2, ["items"] = Number("Coordinate") };
    private static JsonObject Points() => new() { ["type"] = "array", ["minItems"] = 1, ["maxItems"] = 4096, ["items"] = Point() };
    private static JsonObject Bounds() => new() { ["type"] = "array", ["description"] = "[x,y,width,height] in root/picture units as described by the tool", ["minItems"] = 4, ["maxItems"] = 4, ["items"] = Number("Coordinate/dimension") };
    private static JsonObject Map(string description, bool nullable = false) => new()
    {
        ["type"] = "object",
        ["description"] = description,
        ["maxProperties"] = 128,
        ["additionalProperties"] = nullable ? new JsonObject { ["type"] = Strings("string", "null") } : Text("Value", 1048576)
    };
}
