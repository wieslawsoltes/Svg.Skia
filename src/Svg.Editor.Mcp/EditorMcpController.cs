using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using SkiaSharp;
using Svg.Editor.Core;
using Svg.Editor.Skia;
using Svg.Editor.Svg;
using Svg.Skia;

namespace Svg.Editor.Mcp;

/// <summary>Explicit automation operations over the existing SVG controller, with no GUI dependency.</summary>
public sealed partial class EditorMcpController
{
    public const int MaximumSourceCharacters = 4 * 1024 * 1024;
    public const int MaximumExportBytes = 16 * 1024 * 1024;
    private readonly SvgEditorController _controller;
    private Dictionary<string, int>? _idCounts;

    public EditorMcpController(SvgEditorController controller) => _controller = controller ?? throw new ArgumentNullException(nameof(controller));
    public SvgEditorController Controller => _controller;
    private SvgDocument Document => _controller.Document ?? throw new EditorMcpException("No document is open.");

    public JsonNode? Invoke(string operation, JsonObject arguments, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EditorMcpCatalog.ValidateArguments(operation, arguments);
        if (arguments["expectedRevision"] is { } revision && EditorMcpArguments.Integer64(revision) != _controller.DocumentRevision)
            throw new EditorMcpException("The document changed. Read its current revision and retry deliberately.");
        _idCounts = null;
        var a = new EditorMcpArguments(arguments);
        JsonNode? result = operation switch
        {
            "unodraw_document_get" => DocumentState(true),
            "unodraw_document_replace" => Replace(a),
            "unodraw_document_export" => Export(a),
            "unodraw_element_list" => ListElements(a),
            "unodraw_element_get" => DescribeElement(Resolve(a.Text("element", required: true)), true),
            "unodraw_element_insert" => Insert(a),
            "unodraw_element_update" => Update(a),
            "unodraw_element_delete" => Delete(a),
            "unodraw_element_reparent" => Reparent(a),
            "unodraw_element_rename" => Rename(a),
            "unodraw_layer" => Layer(a),
            "unodraw_selection_get" => SelectionState(),
            "unodraw_selection_set" => Select(a),
            "unodraw_edit" => Edit(a),
            "unodraw_batch" => Batch(a, cancellationToken),
            "unodraw_transform" => Transform(a),
            "unodraw_arrange" => Arrange(a),
            "unodraw_draw" => Draw(a),
            "unodraw_path" => Path(a, cancellationToken),
            "unodraw_paint" => Paint(a),
            "unodraw_gradient" => Gradient(a),
            "unodraw_filter" => Filter(a),
            "unodraw_artboard_list" => Artboards(),
            "unodraw_artboard_edit" => Artboard(a),
            "unodraw_guide_list" => Guides(),
            "unodraw_guide_edit" => Guide(a),
            "unodraw_resource_list" => Resources(),
            "unodraw_resource_import" => ImportResource(a),
            "unodraw_text" => Text(a),
            "unodraw_text_story" => TextStory(a),
            "unodraw_brush" => Brush(a),
            "unodraw_mesh" => Mesh(a),
            "unodraw_symbol" => Symbol(a),
            "unodraw_timeline_get" => Timeline(),
            "unodraw_timeline_edit" => TimelineEdit(a),
            "unodraw_preflight" => Preflight(),
            "unodraw_measure" => Measure(a),
            "unodraw_print_get" => PrintSettings(),
            "unodraw_print_set" => SetPrintSettings(a),
            "unodraw_print_export" => PrintExport(a),
            _ => throw new EditorMcpException("This operation requires a live UnoDraw workspace: " + operation)
        };
        if (result is JsonObject value && !value.ContainsKey("revision"))
            value["revision"] = _controller.DocumentRevision;
        return result;
    }

    public JsonObject DocumentState(bool includeSource = false)
    {
        var value = new JsonObject
        {
            ["revision"] = _controller.DocumentRevision,
            ["dirty"] = _controller.IsDirty,
            ["canUndo"] = _controller.CanUndo,
            ["canRedo"] = _controller.CanRedo,
            ["width"] = Document.Width.ToString(),
            ["height"] = Document.Height.ToString(),
            ["viewBox"] = Document.ViewBox.ToString(),
            ["selection"] = new JsonArray(_controller.SelectedElements.Select(element => (JsonNode?)JsonValue.Create(Key(element))).ToArray())
        };
        if (includeSource)
            value["svg"] = _controller.GetSvg();
        return value;
    }

    public JsonObject SelectionState() => new()
    {
        ["revision"] = _controller.DocumentRevision,
        ["elements"] = new JsonArray(_controller.SelectedElements.Select(element => (JsonNode?)DescribeElement(element)).ToArray()),
        ["bounds"] = _controller.SelectionBounds is { } bounds ? Rect(bounds) : null
    };

    public static void ValidateSvg(string xml)
    {
        var tree = ParseXml(xml, MaximumSourceCharacters);
        if (tree.Root is not { Name.LocalName: "svg" } root || root.Name.NamespaceName != "http://www.w3.org/2000/svg")
            throw new EditorMcpException("Supply a complete SVG document with the SVG namespace.");
        ValidateUniqueIds(root);
    }

    private static XDocument ParseXml(string xml, int limit = MaximumSourceCharacters)
    {
        if (string.IsNullOrWhiteSpace(xml) || xml.Length > limit)
            throw new EditorMcpException($"XML must contain between 1 and {limit} characters.");
        using var input = new StringReader(xml);
        // SVG.NET emits the standard SVG DOCTYPE. Ignore declarations entirely,
        // like SvgEditorController.ParseDocument; never load DTDs or expand entities.
        using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, MaxCharactersInDocument = limit });
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }

    private static void ValidateUniqueIds(XElement root)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in root.DescendantsAndSelf().Attributes("id"))
        {
            XmlConvert.VerifyNCName(id.Value);
            if (!ids.Add(id.Value))
                throw new EditorMcpException("Duplicate SVG ID: " + id.Value);
        }
    }

    private JsonObject Replace(EditorMcpArguments a)
    {
        var svg = a.Text("svg", required: true, maximum: MaximumSourceCharacters);
        ValidateSvg(svg);
        _controller.ReplaceSvg(svg, a.Text("label", "MCP: replace SVG source"));
        return DocumentState();
    }

    private string PathOf(SvgElement element)
    {
        if (ReferenceEquals(element, Document))
            return string.Empty;
        var path = new Stack<int>();
        for (var current = element; !ReferenceEquals(current, Document); current = current.Parent)
        {
            if (current.Parent is not { } parent)
                throw new EditorMcpException("The element is no longer attached to this document.");
            path.Push(parent.Children.IndexOf(current));
        }
        return string.Join("/", path);
    }

    private string Key(SvgElement element)
    {
        if (ReferenceEquals(element, Document))
            return "root";
        _idCounts ??= new[] { (SvgElement)Document }.Concat(Document.Descendants()).Where(item => !string.IsNullOrEmpty(item.ID))
            .GroupBy(item => item.ID, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        return !string.IsNullOrEmpty(element.ID) && _idCounts.TryGetValue(element.ID, out var count) && count == 1
            ? "id:" + element.ID : "path:" + PathOf(element);
    }

    private SvgElement Resolve(string key, EditorMcpArguments? mutation = null)
    {
        if (key == "root")
            return Document;
        if (key.StartsWith("path:", StringComparison.Ordinal))
        {
            if (mutation is not null && !mutation.Has("expectedRevision"))
                throw new EditorMcpException("Structural element paths require expectedRevision for mutations.");
            SvgElement element = Document;
            foreach (var part in key[5..].Split('/'))
            {
                if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var index) || index < 0 || index >= element.Children.Count)
                    throw new EditorMcpException("The structural element path does not exist: " + key);
                element = element.Children[index];
            }
            return element;
        }
        var id = key.StartsWith("id:", StringComparison.Ordinal) ? key[3..] : key;
        var matches = new[] { (SvgElement)Document }.Concat(Document.Descendants()).Where(element => element.ID == id).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : throw new EditorMcpException(matches.Length == 0 ? "Unknown SVG element: " + key : "Ambiguous duplicate SVG ID: " + id);
    }

    private SvgElement Target(EditorMcpArguments a, string name = "element") => Resolve(a.Text(name, required: true), a);
    private static void Editable(SvgElement element)
    { if (SvgEditorController.IsEditLocked(element)) throw new EditorMcpException("Unlock the object or its locked descendants before editing it."); }
    private static JsonArray Rect(SKRect value) => new(value.Left, value.Top, value.Width, value.Height);
    private static JsonArray Point(SKPoint value) => new(value.X, value.Y);
    private XElement XmlElement(XElement root, SvgElement element)
    {
        var node = root;
        var path = PathOf(element);
        if (path.Length > 0)
        foreach (var part in path.Split('/'))
            node = node.Elements().ElementAt(int.Parse(part, CultureInfo.InvariantCulture));
        return node;
    }

    private JsonObject DescribeElement(SvgElement element, bool includeXml = false)
    {
        var value = new JsonObject
        {
            ["key"] = Key(element),
            ["id"] = element.ID,
            ["type"] = SvgElementInfoName(element),
            ["parent"] = element.Parent is { } parent ? Key(parent) : null,
            ["children"] = element.Children.Count,
            ["locked"] = SvgEditorController.IsLocked(element),
            ["editLocked"] = SvgEditorController.IsEditLocked(element)
        };
        if (element is SvgVisualElement visual)
            value["visible"] = visual.Display != "none" && visual.Visibility != "hidden";
        if (includeXml)
        {
            var node = XmlElement(ParseXml(_controller.GetSvg()).Root!, element);
            value["xml"] = node.ToString(SaveOptions.DisableFormatting);
            value["attributes"] = new JsonObject(node.Attributes().Select(attribute => KeyValuePair.Create<string, JsonNode?>(attribute.Name.ToString(), JsonValue.Create(attribute.Value))));
        }
        return value;
    }

    private JsonObject ListElements(EditorMcpArguments a)
    {
        var parent = a.Has("parent") ? Resolve(a.Text("parent")) : Document;
        var source = a.Bool("recursive", true) ? parent.Descendants() : parent.Children;
        var type = a.Text("type");
        if (type.Length > 0)
            source = source.Where(element => SvgElementInfoName(element) == type);
        var offset = a.Int("offset", 0, 0, int.MaxValue);
        var limit = a.Int("limit", 200, 1, 1000);
        var page = source.Skip(offset).Take(limit + 1).ToArray();
        return new JsonObject
        {
            ["elements"] = new JsonArray(page.Take(limit).Select(element => (JsonNode?)DescribeElement(element)).ToArray()),
            ["offset"] = offset,
            ["nextOffset"] = page.Length > limit ? offset + limit : null,
            ["revision"] = _controller.DocumentRevision
        };
    }

    private JsonObject Insert(EditorMcpArguments a)
    {
        var parent = a.Has("parent") ? Target(a, "parent") : Document;
        if (SvgEditorController.IsLocked(parent))
            throw new EditorMcpException("Unlock the destination before inserting content.");
        var tree = ParseXml(_controller.GetSvg());
        var destination = XmlElement(tree.Root!, parent);
        var fragment = ParseXml("<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\">" + a.Text("xml", required: true, maximum: 1048576) + "</svg>");
        var nodes = fragment.Root!.Elements().ToArray();
        if (nodes.Length == 0)
            throw new EditorMcpException("The fragment contains no SVG elements.");
        var index = a.Int("index", -1, -1, destination.Elements().Count());
        if (index == -1 || index == destination.Elements().Count())
            destination.Add(nodes);
        else
            destination.Elements().ElementAt(index).AddBeforeSelf(nodes);
        ValidateUniqueIds(tree.Root!);
        _controller.ReplaceSvg(tree.ToString(SaveOptions.DisableFormatting), "MCP: insert SVG elements");
        return DocumentState();
    }

    private JsonObject Update(EditorMcpArguments a)
    {
        var element = Target(a);
        Editable(element);
        var tree = ParseXml(_controller.GetSvg());
        var node = XmlElement(tree.Root!, element);
        if (a.Has("xml"))
        {
            var replacement = ParseXml(a.Text("xml", required: true, maximum: 1048576)).Root!;
            if ((string?)replacement.Attribute("id") != (string?)node.Attribute("id"))
                throw new EditorMcpException("Use element_rename to change IDs and preserve references.");
            node.ReplaceWith(replacement);
            node = replacement;
        }
        if (a.Object("attributes") is { } attributes)
            foreach (var (name, value) in attributes)
            {
                if (name == "id")
                    throw new EditorMcpException("Use element_rename to change IDs and preserve references.");
                var attribute = AttributeName(name);
                var text = value?.GetValue<string>();
                // SVG.NET writes presentation properties into inline style. Update
                // the existing declaration too, otherwise an old fill/font/etc.
                // overrides the newly written presentation attribute on reload.
                if (attribute.NamespaceName.Length == 0 && name != "style" && (string?)node.Attribute("style") is { } style
                    && SvgInlineStyleService.ContainsProperty(style, name))
                {
                    var updated = SvgInlineStyleService.SetProperty(style, name, text);
                    node.SetAttributeValue("style", updated.Length == 0 ? null : updated);
                }
                node.SetAttributeValue(attribute, text);
            }
        if (a.Has("text"))
            node.Value = a.Text("text", maximum: 1048576);
        ValidateUniqueIds(tree.Root!);
        _controller.ReplaceSvg(tree.ToString(SaveOptions.DisableFormatting), "MCP: update SVG element");
        return DocumentState();
    }

    private static XName AttributeName(string name)
    {
        if (name.StartsWith("xlink:", StringComparison.Ordinal))
            return XName.Get(name[6..], "http://www.w3.org/1999/xlink");
        if (name.StartsWith("xml:", StringComparison.Ordinal))
            return XNamespace.Xml + name[4..];
        return XName.Get(name);
    }

    private JsonObject Delete(EditorMcpArguments a)
    {
        var elements = a.Strings("elements", required: true).Select(key => Resolve(key, a)).Distinct().ToArray();
        foreach (var element in elements)
        { if (ReferenceEquals(element, Document)) throw new EditorMcpException("Use document_close to close a document."); Editable(element); }
        var tree = ParseXml(_controller.GetSvg());
        var nodes = elements.Select(element => XmlElement(tree.Root!, element)).ToArray();
        foreach (var node in nodes)
            node.Remove();
        _controller.ReplaceSvg(tree.ToString(SaveOptions.DisableFormatting), "MCP: delete elements");
        return DocumentState();
    }

    private JsonObject Reparent(EditorMcpArguments a)
    {
        _controller.MoveHierarchyElements(a.Strings("elements", true).Select(key => Resolve(key, a)).ToArray(), Target(a, "parent"),
            a.Int("index", -1, -1, int.MaxValue), a.Bool("preserveWorldPosition", true));
        return DocumentState();
    }

    private JsonObject Rename(EditorMcpArguments a)
    { _controller.RenameElementId(Target(a), a.Text("id", required: true)); return DocumentState(); }

    private JsonObject Select(EditorMcpArguments a)
    {
        var additive = a.Bool("additive");
        switch (a.Text("mode", "elements"))
        {
            case "elements":
                var elements = a.Strings("elements", true).Select(key => Resolve(key)).ToArray();
                if (elements.Any(element => element is not SvgVisualElement))
                    throw new EditorMcpException("Selection requires visual SVG elements.");
                if (!additive)
                    _controller.ClearSelection();
                foreach (var element in elements.Cast<SvgVisualElement>())
                    _controller.Select(element, true, a.Bool("allowLocked"));
                break;
            case "point":
                _controller.SelectAt(a.Point("point"), additive);
                break;
            case "rectangle":
                _controller.SelectInRect(a.Bounds("bounds"), additive);
                break;
            case "lasso":
                _controller.SelectInLasso(a.Points("points"), additive ? SvgEditorSelectionBehavior.Add : SvgEditorSelectionBehavior.Replace);
                break;
            case "all":
                _controller.SelectAll();
                break;
            case "none":
                _controller.ClearSelection();
                break;
            case "inverse":
                _controller.InvertSelection();
                break;
            case "matching":
                _controller.SelectMatching(a.Enum<SvgSelectionMatch>("match", SvgSelectionMatch.Fill));
                break;
            case "appearance":
                _controller.SelectByAppearanceAt(a.Point("point"), behavior: additive ? SvgEditorSelectionBehavior.Add : SvgEditorSelectionBehavior.Replace);
                break;
            default:
                throw new EditorMcpException("Unknown selection mode.");
        }
        return SelectionState();
    }

    private JsonObject Export(EditorMcpArguments a)
    {
        if (a.Bool("selectionOnly") && a.Has("artboardId"))
            throw new EditorMcpException("Choose selection or artboard export, not both.");
        if (a.Bool("selectionOnly") && _controller.SelectedElements.Count == 0)
            throw new EditorMcpException("Select artwork to export.");
        var xml = a.Bool("selectionOnly") ? _controller.GetSelectionSvg()
            : a.Has("artboardId") ? _controller.GetArtboardSvg(a.Text("artboardId")) : _controller.GetSvg();
        var format = a.Text("format", "svg");
        if (format == "svg")
            return new JsonObject { ["mimeType"] = "image/svg+xml", ["text"] = xml };
        using var output = new MemoryStream();
        string mime;
        if (format == "svgz")
        {
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal, true))
            { var bytes = Encoding.UTF8.GetBytes(xml); gzip.Write(bytes); }
            mime = "image/svg+xml";
        }
        else
        {
            using var renderer = new SKSvg();
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(xml));
            renderer.Load(input, null, Document.BaseUri);
            renderer.SetAnimationTime(_controller.Renderer.AnimationTime);
            using var picture = SvgRenderSnapshotService.Capture(renderer);
            var scale = a.Float("scale", 1, 0.01f, 16);
            var bounds = picture.CullRect;
            if ((double)bounds.Width * scale * bounds.Height * scale > 40000000)
                throw new EditorMcpException("Export exceeds 40 million pixels. Choose a smaller region or scale.");
            var background = a.Bool("transparent", true) && format != "jpeg" ? SKColors.Transparent : SKColors.White;
            bool success;
            if (format == "pdf")
            { success = picture.ToPdf(output, SKColors.White, scale, scale); mime = "application/pdf"; }
            else if (format == "xps")
            { success = picture.ToXps(output, SKColors.White, scale, scale); mime = "application/vnd.ms-xpsdocument"; }
            else
            {
                var encoding = format switch { "png" => SKEncodedImageFormat.Png, "jpeg" => SKEncodedImageFormat.Jpeg, "webp" => SKEncodedImageFormat.Webp, _ => throw new EditorMcpException("Unknown export format.") };
                using var colorSpace = SKColorSpace.CreateSrgb();
                success = picture.ToImage(output, background, encoding, 95, scale, scale, SKColorType.Rgba8888, SKAlphaType.Premul, colorSpace);
                mime = "image/" + format;
            }
            if (!success)
                throw new EditorMcpException("The export format is unavailable on this platform.");
        }
        if (output.Length > MaximumExportBytes)
            throw new EditorMcpException("Export exceeds 16 MiB. Choose a smaller region, a vector format or a lower scale.");
        return new JsonObject { ["mimeType"] = mime, ["encoding"] = format == "svgz" ? "gzip" : "base64", ["base64"] = Convert.ToBase64String(output.ToArray()), ["bytes"] = output.Length };
    }
}

/// <summary>Bounded, explicit argument conversion shared with the live workspace adapter.</summary>
public sealed class EditorMcpArguments(JsonObject value)
{
    public JsonObject Value { get; } = value;
    public bool Has(string name) => Value[name] is not null;
    public string Text(string name, string fallback = "", bool required = false, int maximum = 4096)
    {
        if (Value[name] is null)
            return required ? throw new EditorMcpException("Missing argument: " + name) : fallback;
        if (Value[name] is not JsonValue node || !node.TryGetValue<string>(out var text) || text.Length > maximum || required && string.IsNullOrWhiteSpace(text))
            throw new EditorMcpException("Invalid string argument: " + name);
        return text;
    }
    public bool Bool(string name, bool fallback = false) => Value[name] is null ? fallback : Value[name]!.GetValue<bool>();
    public double Number(string name, double fallback = 0, double minimum = -1000000, double maximum = 1000000)
    {
        var number = Value[name] is null ? fallback : NumericValue(Value[name]);
        if (!double.IsFinite(number) || number < minimum || number > maximum)
            throw new EditorMcpException("Number out of range: " + name);
        return number;
    }
    public float Float(string name, float fallback = 0, float minimum = -1000000, float maximum = 1000000) => (float)Number(name, fallback, minimum, maximum);
    public int Int(string name, int fallback = 0, int minimum = -1000000, int maximum = 1000000)
    { var number = Number(name, fallback, minimum, maximum); if (number != Math.Truncate(number)) throw new EditorMcpException("Expected integer: " + name); return (int)number; }
    public JsonObject? Object(string name) => Value[name] is null ? null : Value[name] as JsonObject ?? throw new EditorMcpException("Expected object: " + name);
    public JsonArray Array(string name, bool required = true, int maximum = 4096)
    {
        if (Value[name] is null && !required)
            return new JsonArray();
        return Value[name] is JsonArray values && values.Count <= maximum ? values : throw new EditorMcpException("Invalid array: " + name);
    }
    public string[] Strings(string name, bool required = false) => Array(name, required, 1000).Select(node => node?.GetValue<string>() ?? throw new EditorMcpException("Expected element key.")).ToArray();
    public T Enum<T>(string name, T fallback) where T : struct, Enum
        => !Has(name) ? fallback : System.Enum.TryParse<T>(Text(name), false, out var parsed) && System.Enum.IsDefined(parsed) ? parsed : throw new EditorMcpException("Unknown value for " + name);
    public static double NumericValue(JsonNode? node)
    {
        if (node is not JsonValue value)
            throw new EditorMcpException("Expected a JSON number.");
        if (value.TryGetValue<double>(out var number))
            return number;
        if (value.TryGetValue<float>(out var single))
            return single;
        if (value.TryGetValue<long>(out var wide))
            return wide;
        if (value.TryGetValue<int>(out var integer))
            return integer;
        if (value.TryGetValue<decimal>(out var precise))
            return (double)precise;
        throw new EditorMcpException("Expected a JSON number.");
    }
    public static long Integer64(JsonNode node)
    {
        if (node is JsonValue value && value.TryGetValue<long>(out var wide))
            return wide;
        if (node is JsonValue small && small.TryGetValue<int>(out var integer))
            return integer;
        throw new EditorMcpException("Expected a 64-bit integer.");
    }
    private static float Coordinate(JsonNode? node)
    { var value = NumericValue(node); return double.IsFinite(value) && Math.Abs(value) <= 1000000 ? (float)value : throw new EditorMcpException("Coordinates must be finite and within ±1,000,000."); }
    public SKPoint Point(string name)
    { var values = Array(name); if (values.Count != 2) throw new EditorMcpException("A point requires [x,y]: " + name); return new SKPoint(Coordinate(values[0]), Coordinate(values[1])); }
    public SKRect Bounds(string name)
    {
        var values = Array(name);
        if (values.Count != 4)
            throw new EditorMcpException("Bounds require [x,y,width,height].");
        var x = Coordinate(values[0]);
        var y = Coordinate(values[1]);
        var width = Coordinate(values[2]);
        var height = Coordinate(values[3]);
        if (width <= 0 || height <= 0)
            throw new EditorMcpException("Bounds must have positive dimensions.");
        return SKRect.Create(x, y, width, height);
    }
    public SKPoint[] Points(string name)
    {
        var values = Array(name);
        if (values.Count == 0)
            throw new EditorMcpException("Provide at least one point.");
        return values.Select(node => node is JsonArray pair && pair.Count == 2 ? new SKPoint(Coordinate(pair[0]), Coordinate(pair[1])) : throw new EditorMcpException("Each point requires [x,y].")).ToArray();
    }
}
