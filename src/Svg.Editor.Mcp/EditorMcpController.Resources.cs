using System.Text.Json.Nodes;
using System.Xml.Linq;
using SkiaSharp;
using Svg.Editor.Skia;
using Svg.Editor.Svg;
using Svg.Skia;

namespace Svg.Editor.Mcp;

public sealed partial class EditorMcpController
{
    private JsonObject Layer(EditorMcpArguments a)
    {
        var action = a.Text("action", required: true);
        if (action == "create")
        {
            var group = new SvgGroup();
            group.CustomAttributes["data-unodraw-layer"] = "true";
            group.CustomAttributes["data-name"] = a.Text("name", "New layer");
            _controller.AddElement(group, "MCP: create layer");
            return DescribeElement(group);
        }
        var element = Target(a);
        switch (action)
        {
            case "rename":
                Editable(element);
                var name = a.Text("name", required: true);
                _controller.Execute("MCP: rename layer", () => element.CustomAttributes["data-name"] = name);
                break;
            case "visibility":
                if (!a.Has("visible"))
                    throw new EditorMcpException("Supply visible=true or false.");
                if (element is not SvgVisualElement visual)
                    throw new EditorMcpException("Visibility requires a visual element.");
                _controller.SetHierarchyVisibility(visual, a.Bool("visible"));
                break;
            case "lock":
                if (!a.Has("locked"))
                    throw new EditorMcpException("Supply locked=true or false.");
                _controller.SetHierarchyLocked(element, a.Bool("locked"));
                break;
            default:
                throw new EditorMcpException("Unknown layer action.");
        }
        return DescribeElement(element);
    }

    private JsonObject Artboards() => new()
    {
        ["active"] = _controller.ActiveArtboardId,
        ["artboards"] = new JsonArray(_controller.GetArtboardsInRootCoordinates().Select(board => (JsonNode?)new JsonObject
        { ["id"] = board.Id, ["name"] = board.Name, ["bounds"] = Rect(board.Bounds), ["implicit"] = board.IsImplicit }).ToArray())
    };

    private JsonObject Artboard(EditorMcpArguments a)
    {
        switch (a.Text("action", required: true))
        {
            case "add":
                _controller.AddArtboardInRootCoordinates(a.Bounds("bounds"), a.Has("name") ? a.Text("name") : null);
                break;
            case "activate":
                _controller.ActivateArtboard(a.Text("id", required: true));
                break;
            case "resize":
                _controller.SetArtboardBoundsInRootCoordinates(a.Text("id", required: true), a.Bounds("bounds"));
                break;
            case "rename":
                _controller.RenameArtboard(a.Text("id", required: true), a.Text("name", required: true));
                break;
            case "duplicate":
                _controller.DuplicateArtboard(a.Text("id", required: true), a.Has("offset") ? a.Point("offset") : new SKPoint(16, 16));
                break;
            case "remove":
                _controller.RemoveArtboard(a.Text("id", required: true));
                break;
            case "reorder":
                _controller.ReorderArtboard(a.Text("id", required: true), a.Int("index", minimum: 0, maximum: 999));
                break;
            default:
                throw new EditorMcpException("Unknown artboard action.");
        }
        return Artboards();
    }

    private JsonObject Guides() => new()
    {
        ["guides"] = new JsonArray(_controller.GetGuides().Select(guide => (JsonNode?)new JsonObject
        { ["id"] = guide.Id, ["origin"] = Point(guide.Origin), ["direction"] = Point(guide.Direction) }).ToArray())
    };

    private JsonObject Guide(EditorMcpArguments a)
    {
        switch (a.Text("action", required: true))
        {
            case "add":
                _controller.AddGuide(a.Point("origin"), a.Point("direction"));
                break;
            case "move":
                _controller.MoveGuide(a.Text("id", required: true), a.Point("origin"));
                break;
            case "remove":
                _controller.RemoveGuide(a.Text("id", required: true));
                break;
            case "clear":
                _controller.ClearGuides();
                break;
            case "selectionBounds":
                RequireSelection();
                _controller.AddSelectionBoundsGuides();
                break;
            default:
                throw new EditorMcpException("Unknown guide action.");
        }
        return Guides();
    }

    private JsonObject Resources()
    {
        var tree = ParseXml(_controller.GetSvg());
        var elements = Document.Descendants().Where(element => element.Parent is SvgDefinitionList || SvgElementInfoName(element) == "style").ToArray();
        return new JsonObject
        {
            ["resources"] = new JsonArray(elements.Select(element =>
            {
                var info = DescribeElement(element);
                info["xml"] = XmlElement(tree.Root!, element).ToString(SaveOptions.DisableFormatting);
                return (JsonNode?)info;
            }).ToArray()),
            ["brushes"] = SvgBrushLibraryService.Export(SvgBrushLibraryService.Load(Document))
        };
    }

    private static string SvgElementInfoName(SvgElement element) => element is SvgUnknownElement unknown ? unknown.Name : global::Svg.Editor.Core.SvgElementInfo.GetElementName(element.GetType());

    private SvgElement ParseResource(string xml)
    {
        var wrapped = "<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\">" + xml + "</svg>";
        var tree = ParseXml(wrapped);
        if (tree.Root!.Elements().Count() != 1)
            throw new EditorMcpException("Supply exactly one SVG resource element.");
        ValidateUniqueIds(tree.Root);
        var source = new SvgDocumentService().FromSvg(wrapped) ?? throw new EditorMcpException("The resource could not be parsed.");
        return source.Children.Single();
    }

    private JsonObject ImportResource(EditorMcpArguments a)
    {
        var resource = ParseResource(a.Text("xml", required: true, maximum: 1048576));
        var adopted = _controller.AddResource(resource, "MCP: import SVG resource");
        return DescribeElement(adopted, true);
    }

    private JsonObject Text(EditorMcpArguments a)
    {
        switch (a.Text("action", "set"))
        {
            case "onPath":
                RequireSelection();
                _controller.AddTextOnSelectedPath(a.Text("text", required: true, maximum: 1048576));
                break;
            case "area":
                _controller.AddTextArea(a.Bounds("bounds"), a.Text("text", required: true, maximum: 1048576));
                break;
            case "set":
                RequireSelection();
                if (_controller.SelectedElements.Any(element => element is not SvgTextBase))
                    throw new EditorMcpException("Select only text objects for typography edits.");
                _controller.SetTypography(a.Has("text") ? a.Text("text", maximum: 1048576) : null, a.Has("fontFamily") ? a.Text("fontFamily") : null,
                    a.Has("fontSize") ? new SvgUnit(a.Float("fontSize", minimum: 0.01f, maximum: 100000)) : null,
                    a.Has("letterSpacing") ? new SvgUnit(a.Float("letterSpacing")) : null);
                break;
            default:
                throw new EditorMcpException("Unknown text action.");
        }
        return SelectionState();
    }

    private JsonObject TextStory(EditorMcpArguments a)
    {
        var element = a.Has("element") ? Resolve(a.Text("element"), a.Has("xml") ? a : null) : _controller.SelectedElements.FirstOrDefault();
        var existing = SvgTextStoryService.FindStory(element);
        if (a.Has("xml"))
        {
            if (a.Has("element") && existing is null)
                throw new EditorMcpException("The requested element does not belong to a linked text story. Omit element to create a story.");
            if (existing is not null)
                Editable(existing);
            var xml = a.Text("xml", required: true, maximum: 1048576);
            ParseXml(xml);
            var story = SvgTextStoryService.Deserialize(xml);
            existing = SvgTextStoryService.Apply(_controller, story, existing);
        }
        if (existing is null)
            throw new EditorMcpException("Select a linked text story or supply complete story XML.");
        return new JsonObject { ["element"] = Key(existing), ["xml"] = SvgTextStoryService.Serialize(SvgTextStoryService.Read(existing)) };
    }

    private JsonObject Brush(EditorMcpArguments a)
    {
        var action = a.Text("action", "list");
        if (action == "import")
        {
            var xml = a.Text("xml", required: true, maximum: 1048576);
            ParseXml(xml);
            var brushes = SvgBrushLibraryService.Import(xml);
            _controller.Execute("MCP: import brush library", () => { foreach (var brush in brushes) SvgBrushLibraryService.Save(Document, brush); });
        }
        else if (action == "remove")
            _controller.Execute("MCP: remove brush", () => SvgBrushLibraryService.Remove(Document, a.Text("id", required: true)));
        else if (action == "apply")
        {
            RequireSelection();
            _controller.EnsureSelectionEditable();
            var brush = SvgBrushLibraryService.Load(Document).SingleOrDefault(value => value.Id == a.Text("id", required: true)) ?? throw new EditorMcpException("The brush does not exist.");
            if (_controller.SelectedElements.Any(element => element is not SvgPath))
                throw new EditorMcpException("Convert the selection to paths before applying a brush.");
            _controller.Execute("MCP: apply brush", () => { foreach (var path in _controller.SelectedElements.Cast<SvgPath>()) SvgBrushLibraryService.Apply(path, brush); });
        }
        else if (action != "list")
            throw new EditorMcpException("Unknown brush action.");
        var all = SvgBrushLibraryService.Load(Document);
        return new JsonObject
        {
            ["xml"] = SvgBrushLibraryService.Export(all),
            ["brushes"] = new JsonArray(all.Select(brush => (JsonNode?)new JsonObject
            { ["id"] = brush.Id, ["name"] = brush.Name, ["kind"] = brush.Kind.ToString() }).ToArray())
        };
    }

    private JsonObject Mesh(EditorMcpArguments a)
    {
        var action = a.Text("action", "get");
        var target = a.Has("element") ? Resolve(a.Text("element"), action == "get" ? null : a) : _controller.SelectedElements.FirstOrDefault();
        var group = target as SvgGroup;
        if (action == "create")
        {
            var mesh = SvgGradientMeshService.CreateGrid(a.Bounds("bounds"), a.Int("rows", 2, 2, 16), a.Int("columns", 2, 2, 16));
            group = SvgGradientMeshService.Create(mesh);
            _controller.AddElement(group, "MCP: create gradient mesh");
        }
        else if (action == "set")
        {
            if (a.Has("element") && group is null)
                throw new EditorMcpException("The requested mesh element must be a compatible group. Omit element to create a mesh.");
            var xml = a.Text("xml", required: true, maximum: 1048576);
            ParseXml(xml);
            var mesh = SvgGradientMeshService.Deserialize(xml);
            if (group is not null)
            { Editable(group); _controller.Execute("MCP: edit gradient mesh", () => SvgGradientMeshService.Apply(group, mesh)); }
            else
            { group = SvgGradientMeshService.Create(mesh); _controller.AddElement(group, "MCP: create gradient mesh"); }
        }
        else if (action != "get")
            throw new EditorMcpException("Unknown mesh action.");
        if (group is null)
            throw new EditorMcpException("Select a gradient mesh group.");
        return new JsonObject { ["element"] = Key(group), ["xml"] = SvgGradientMeshService.Serialize(SvgGradientMeshService.GetMesh(group)) };
    }

    private JsonObject Symbol(EditorMcpArguments a)
    {
        var settings = new SvgSymbolBrushOptions
        {
            Radius = a.Float("radius", 40, 1, 10000),
            Size = a.Float("size", 24, 1, 10000),
            Density = a.Int("density", 3, 1, 64),
            Strength = a.Float("strength", 0.35f, 0, 1),
            Seed = a.Int("seed", 1, int.MinValue, int.MaxValue),
            GraphicStyleName = a.Has("style") ? a.Text("style") : null
        };
        var points = a.Points("points");
        if (a.Text("action", required: true) == "spray")
            _controller.SpraySymbols(a.Text("id", required: true), points, settings);
        else
        {
            using var preview = _controller.PrepareSymbolBrush(a.Enum("action", SvgSymbolBrushKind.Shift), points, settings, a.Bool("reverse"));
            _controller.ApplySymbolBrush(preview);
        }
        return DocumentState();
    }

    private JsonObject Timeline()
    {
        var tracks = SvgTimelineService.ReadTracks(Document);
        var tree = ParseXml(_controller.GetSvg());
        return new JsonObject
        {
            ["seconds"] = _controller.Renderer.AnimationTime.TotalSeconds,
            ["tracks"] = new JsonArray(tracks.Select((track, index) => (JsonNode?)new JsonObject
            {
                ["index"] = index,
                ["target"] = Key(track.Target),
                ["kind"] = track.Kind.ToString(),
                ["attribute"] = track.AttributeName,
                ["begin"] = track.Begin,
                ["duration"] = track.Duration,
                ["repeatCount"] = track.RepeatCount,
                ["source"] = track.Source is { } source ? Key(source) : null,
                ["xml"] = track.Source is { } animation ? XmlElement(tree.Root!, animation).ToString(SaveOptions.DisableFormatting) : null,
                ["keys"] = new JsonArray(track.Keys.Select(key => (JsonNode?)new JsonObject { ["offset"] = key.Offset, ["value"] = key.Value, ["spline"] = key.Spline }).ToArray())
            }).ToArray())
        };
    }

    private JsonObject TimelineEdit(EditorMcpArguments a)
    {
        var action = a.Text("action", required: true);
        if (action == "time")
            _controller.Renderer.SetAnimationTime(TimeSpan.FromSeconds(a.Number("seconds", minimum: 0, maximum: 86400)));
        else if (action == "bake")
        {
            var baked = SvgTimelineService.BakeFrame(Document, TimeSpan.FromSeconds(a.Number("seconds", minimum: 0, maximum: 86400)));
            _controller.ReplaceSvg(baked.GetXML(), "MCP: bake animation frame");
        }
        else if (action == "create")
        {
            var element = Target(a);
            Editable(element);
            var track = SvgTimelineService.CreateTrack(element, a.Text("attribute", "opacity"), a.Text("from", "0"), a.Text("to", "1"));
            track.Duration = a.Text("duration", "2s");
            track.RepeatCount = a.Text("repeatCount", "1");
            SvgTimelineService.Validate(track);
            _controller.Execute("MCP: add animation track", () => SvgTimelineService.Apply(Document, track));
        }
        else
        {
            if (!a.Has("expectedRevision"))
                throw new EditorMcpException("Track indexes require expectedRevision.");
            var tracks = SvgTimelineService.ReadTracks(Document);
            var index = a.Int("track", minimum: 0, maximum: 10000);
            if (index >= tracks.Count)
                throw new EditorMcpException("Unknown animation track index.");
            var track = tracks[index];
            Editable(track.Target);
            if (action == "delete")
                _controller.Execute("MCP: remove animation track", () => SvgTimelineService.Delete(Document, track));
            else if (action == "key")
            {
                SvgTimelineService.SetKey(track, a.Number("offset", minimum: 0, maximum: 1), a.Text("value", required: true), a.Text("spline", "0 0 1 1"));
                _controller.Execute("MCP: edit animation key", () => SvgTimelineService.Apply(Document, track));
            }
            else
                throw new EditorMcpException("Unknown timeline action.");
        }
        return Timeline();
    }

    private JsonObject Preflight() => new()
    {
        ["issues"] = new JsonArray(SvgPreflightService.Inspect(_controller, SvgPrintSettings.FromDocument(Document))
        .Select(issue => (JsonNode?)new JsonObject { ["code"] = issue.Code, ["severity"] = issue.Severity.ToString(), ["message"] = issue.Message, ["elementId"] = issue.ElementId }).ToArray())
    };

    private JsonObject Measure(EditorMcpArguments a)
    {
        var value = _controller.MeasureInDocumentCoordinates(a.Point("start"), a.Point("end"));
        return new JsonObject
        {
            ["start"] = Point(value.Start),
            ["end"] = Point(value.End),
            ["deltaX"] = value.DeltaX,
            ["deltaY"] = value.DeltaY,
            ["distance"] = value.Distance,
            ["angleDegrees"] = value.AngleDegrees
        };
    }

    private JsonObject PrintSettings()
    {
        var settings = SvgPrintSettings.FromDocument(Document);
        var pages = SvgPrintLayoutService.CreatePages(_controller.Renderer.Picture?.CullRect ?? SKRect.Empty, settings);
        return new JsonObject
        {
            ["xml"] = settings.Serialize(),
            ["namedInks"] = new JsonArray(Document.Descendants()
            .Where(element => element.CustomAttributes.ContainsKey(SvgSpotSeparationService.SpotNameAttribute))
            .Select(element => element.CustomAttributes[SvgSpotSeparationService.SpotNameAttribute]).Distinct(StringComparer.Ordinal)
            .Select(name => (JsonNode?)JsonValue.Create(name)).ToArray()),
            ["pages"] = new JsonArray(pages.Select(page => (JsonNode?)new JsonObject
            {
                ["number"] = page.Number,
                ["column"] = page.Column,
                ["row"] = page.Row,
                ["sizePoints"] = new JsonArray(page.MediaSize.Width, page.MediaSize.Height),
                ["trimBox"] = Rect(page.TrimBox),
                ["bleedBox"] = Rect(page.BleedBox)
            }).ToArray())
        };
    }

    private JsonObject SetPrintSettings(EditorMcpArguments a)
    {
        var xml = a.Text("xml", required: true, maximum: 16384);
        ParseXml(xml, 16384);
        var temporary = new SvgDocument();
        temporary.CustomAttributes["data-unodraw-print"] = xml;
        var settings = SvgPrintSettings.FromDocument(temporary);
        settings.Apply(_controller);
        return PrintSettings();
    }

    private JsonObject PrintExport(EditorMcpArguments a)
    {
        var settings = SvgPrintSettings.FromDocument(Document);
        SvgIccCmykProfile? profile = null;
        if (a.Has("profileBase64"))
        {
            byte[] bytes;
            try
            { bytes = Convert.FromBase64String(a.Text("profileBase64", maximum: 8388608)); }
            catch (FormatException) { throw new EditorMcpException("The ICC profile must be valid base64."); }
            profile = new SvgIccCmykProfile(bytes, a.Int("renderingIntent", 1, 0, 3));
        }
        using var picture = SvgRenderSnapshotService.Capture(_controller.Renderer);
        using var output = new MemoryStream();
        var format = a.Text("format", "pdf");
        var hasSpots = Document.Descendants().Any(element => element.CustomAttributes.ContainsKey(SvgSpotSeparationService.SpotNameAttribute));
        if (format == "pdf")
        {
            if (hasSpots && !a.Bool("convertSpotsToProcess"))
                throw new EditorMcpException("PDF requires convertSpotsToProcess=true for named inks; use separations to retain separate ink plates.");
            SvgPrintService.ExportPdf(picture, output, settings, profile);
        }
        else if (format == "separations" && hasSpots)
            SvgPrintService.ExportSpotSeparations(Document, output, settings, profile);
        else if (format == "separations")
            SvgPrintService.ExportSeparations(picture, output, settings, profile, _controller.GetSvg());
        else
            throw new EditorMcpException("Unknown print export format.");
        if (output.Length > MaximumExportBytes)
            throw new EditorMcpException("Print export exceeds 16 MiB. Reduce page count/resolution or export a smaller document.");
        return new JsonObject { ["mimeType"] = format == "pdf" ? "application/pdf" : "application/zip", ["base64"] = Convert.ToBase64String(output.ToArray()), ["bytes"] = output.Length };
    }
}
