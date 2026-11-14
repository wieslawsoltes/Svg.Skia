using System.Text.Json.Nodes;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using SkiaSharp;
using Svg.Editor.Skia;
using Xunit;

namespace Svg.Editor.Mcp.UnitTests;

public sealed class EditorMcpControllerTests
{
    private const string Source = "<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100'><rect id='shape' x='10' y='15' width='30' height='40' fill='#ff0000'/><circle cx='100' cy='50' r='10'/></svg>";
    private static SvgEditorController Create(string source = Source) { var editor = new SvgEditorController(); editor.LoadSvg(source); return editor; }
    private static JsonObject Args(string json = "{}") => JsonNode.Parse(json)!.AsObject();
    private static JsonObject Call(EditorMcpController target, string name, string json = "{}") => target.Invoke("unodraw_" + name, Args(json))!.AsObject();

    [Fact]
    public void CatalogHasUniqueNamesObjectSchemasAndHonestReadOnlyOperations()
    {
        Assert.Equal(EditorMcpCatalog.Tools.Count, EditorMcpCatalog.Tools.Select(tool => tool.Name).Distinct().Count());
        Assert.All(EditorMcpCatalog.Tools, tool =>
        {
            Assert.StartsWith("unodraw_", tool.Name);
            Assert.Equal("object", tool.InputSchema.GetProperty("type").GetString());
            Assert.False(tool.InputSchema.GetProperty("additionalProperties").GetBoolean());
        });
        Assert.True(EditorMcpCatalog.Get("unodraw_document_get").ReadOnly);
        Assert.True(EditorMcpCatalog.Get("unodraw_document_export").ReadOnly);
        Assert.False(EditorMcpCatalog.Get("unodraw_commands_run").ReadOnly);
        Assert.False(EditorMcpCatalog.Get("unodraw_workspace_set").ReadOnly);
    }

    [Fact]
    public void DirectWorkspaceRequestsValidateCompletelyBeforeDispatch()
    {
        var arguments = Args("{\"zoom\":2,\"filters\":\"invalid\"}");
        Assert.Throws<EditorMcpException>(() => EditorMcpCatalog.ValidateArguments("unodraw_view_set", arguments));
        arguments["filters"] = false;
        Assert.Same(EditorMcpCatalog.Get("unodraw_view_set"), EditorMcpCatalog.ValidateArguments("unodraw_view_set", arguments));
        Assert.Throws<EditorMcpException>(() => EditorMcpCatalog.ValidateArguments("unodraw_commands_run", Args("{\"id\":\"tool-rectangle\",\"allowInteraction\":\"yes\"}")));
        Assert.Throws<EditorMcpException>(() => EditorMcpCatalog.ValidateArguments("unodraw_document_new", Args("{\"width\":200,\"height\":0}")));
        Assert.Throws<EditorMcpException>(() => EditorMcpCatalog.ValidateArguments("unodraw_workspace_get", Args("{\"unexpected\":true}")));
    }

    [Fact]
    public void InspectionDoesNotAssignIdsOrChangeSelectionHistoryAndSource()
    {
        using var editor = Create();
        var target = new EditorMcpController(editor);
        var source = editor.GetSvg();
        var revision = editor.DocumentRevision;
        var elements = Call(target, "element_list")["elements"]!.AsArray();
        Assert.Equal("id:shape", elements[0]!["key"]!.GetValue<string>());
        Assert.Equal("path:1", elements[1]!["key"]!.GetValue<string>());
        Call(target, "element_get", "{\"element\":\"path:1\"}");
        Call(target, "document_get");
        Call(target, "selection_get");
        Call(target, "artboard_list");
        Call(target, "guide_list");
        Assert.Equal(source, editor.GetSvg());
        Assert.Equal(revision, editor.DocumentRevision);
        Assert.Empty(editor.SelectedElements);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void ElementPaginationRetainsStableAddresses()
    {
        using var editor = Create();
        var target = new EditorMcpController(editor);
        var first = Call(target, "element_list", "{\"limit\":1}");
        Assert.Equal(1, first["nextOffset"]!.GetValue<int>());
        var second = Call(target, "element_list", "{\"offset\":1,\"limit\":1}");
        Assert.Null(second["nextOffset"]);
        Assert.Equal("path:1", second["elements"]![0]!["key"]!.GetValue<string>());
    }

    [Fact]
    public void StructuralEditsRequireRevisionAndRejectStaleReads()
    {
        using var editor = Create();
        var target = new EditorMcpController(editor);
        Assert.Throws<EditorMcpException>(() => Call(target, "element_update", "{\"element\":\"path:1\",\"attributes\":{\"fill\":\"blue\"}}"));
        var args = Args("{\"element\":\"path:1\",\"attributes\":{\"fill\":\"blue\"}}");
        args["expectedRevision"] = editor.DocumentRevision;
        target.Invoke("unodraw_element_update", args);
        Assert.Throws<EditorMcpException>(() => target.Invoke("unodraw_element_update", args));
        Assert.Equal(1, editor.Session.UndoCount);
    }

    [Fact]
    public void AttributeAndFragmentEditsParticipateInUndo()
    {
        using var editor = Create();
        var target = new EditorMcpController(editor);
        var before = editor.GetSvg();
        Call(target, "element_update", "{\"element\":\"shape\",\"attributes\":{\"fill\":\"blue\",\"opacity\":\"0.5\"}}");
        Assert.Equal(System.Drawing.Color.Blue.ToArgb(), Assert.IsType<SvgColourServer>(editor.Document!.GetElementById<SvgRectangle>("shape").Fill).Colour.ToArgb());
        Call(target, "edit", "{\"action\":\"undo\"}");
        Assert.Equal(before, editor.GetSvg());
        Call(target, "element_insert", "{\"xml\":\"<path id='newPath' d='M0 0L10 10'/>\"}");
        Assert.NotNull(editor.Document!.GetElementById("newPath"));
        editor.Undo();
        Assert.Equal(before, editor.GetSvg());
    }

    [Fact]
    public void DuplicateIdsAndDtdsAreRejectedBeforeMutation()
    {
        using var editor = Create();
        var target = new EditorMcpController(editor);
        var before = editor.GetSvg();
        Assert.Throws<EditorMcpException>(() => Call(target, "element_insert", "{\"xml\":\"<path id='shape' d='M0 0L10 10'/>\"}"));
        Assert.Throws<XmlException>(() => EditorMcpController.ValidateSvg("<!DOCTYPE svg [<!ENTITY bad SYSTEM 'file:///etc/passwd'>]><svg xmlns='http://www.w3.org/2000/svg'>&bad;</svg>"));
        Assert.Equal(before, editor.GetSvg());
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void RenameUpdatesDocumentReferences()
    {
        using var editor = Create("<svg xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink' width='100' height='100'><defs><path id='curve' d='M0 20L100 20'/></defs><text><textPath xlink:href='#curve'>hello</textPath></text></svg>");
        var target = new EditorMcpController(editor);
        Call(target, "element_rename", "{\"element\":\"curve\",\"id\":\"renamed\"}");
        Assert.Null(editor.Document!.GetElementById("curve"));
        Assert.NotNull(editor.Document.GetElementById("renamed"));
        Assert.Contains("#renamed", editor.GetSvg());
    }

    [Fact]
    public void LockedBranchesRejectElementMutation()
    {
        using var editor = Create("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><g id='locked' data-unodraw-locked='true'><rect id='shape' width='10' height='10'/></g></svg>");
        var target = new EditorMcpController(editor);
        var before = editor.GetSvg();
        Assert.Throws<EditorMcpException>(() => Call(target, "element_update", "{\"element\":\"shape\",\"attributes\":{\"fill\":\"blue\"}}"));
        Assert.Throws<EditorMcpException>(() => Call(target, "element_delete", "{\"elements\":[\"locked\"]}"));
        Assert.Throws<EditorMcpException>(() => Call(target, "element_insert", "{\"parent\":\"locked\",\"xml\":\"<circle r='5'/>\"}"));
        Assert.Equal(before, editor.GetSvg());
    }

    [Fact]
    public void BatchCommitsMultipleChangesAsOneUndoStep()
    {
        using var editor = Create();
        var target = new EditorMcpController(editor);
        var before = editor.GetSvg();
        var args = Args("{\"operations\":[{\"name\":\"unodraw_element_update\",\"arguments\":{\"element\":\"shape\",\"attributes\":{\"fill\":\"blue\"}}},{\"name\":\"unodraw_element_insert\",\"arguments\":{\"xml\":\"<path id='extra' d='M0 0L10 10'/>\"}}]}");
        args["expectedRevision"] = editor.DocumentRevision;
        target.Invoke("unodraw_batch", args);
        Assert.NotNull(editor.Document!.GetElementById("extra"));
        Assert.Equal(System.Drawing.Color.Blue.ToArgb(), Assert.IsType<SvgColourServer>(editor.Document.GetElementById<SvgRectangle>("shape").Fill).Colour.ToArgb());
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(before, editor.GetSvg());
    }

    [Fact]
    public void FailedBatchRestoresSourceSelectionAndHistory()
    {
        using var editor = Create();
        var target = new EditorMcpController(editor);
        Call(target, "selection_set", "{\"elements\":[\"shape\"]}");
        var before = editor.GetSvg();
        var revision = editor.DocumentRevision;
        var args = Args("{\"operations\":[{\"name\":\"unodraw_element_update\",\"arguments\":{\"element\":\"shape\",\"attributes\":{\"fill\":\"blue\"}}},{\"name\":\"unodraw_element_update\",\"arguments\":{\"element\":\"missing\",\"attributes\":{\"fill\":\"green\"}}}]}");
        args["expectedRevision"] = revision;
        Assert.Throws<EditorMcpException>(() => target.Invoke("unodraw_batch", args));
        Assert.Equal(before, editor.GetSvg());
        Assert.Equal("shape", Assert.Single(editor.SelectedElements).ID);
        Assert.False(editor.CanUndo);
        Assert.False(editor.IsEditing);
    }

    [Fact]
    public void BatchPreservesExplicitStalePreconditionAndValidatesNestedSchema()
    {
        using var editor = Create();
        var target = new EditorMcpController(editor);
        var before = editor.GetSvg();
        var args = Args("{\"operations\":[{\"name\":\"unodraw_element_update\",\"arguments\":{\"element\":\"shape\",\"expectedRevision\":999,\"attributes\":{\"fill\":\"blue\"}}}]}");
        args["expectedRevision"] = editor.DocumentRevision;
        Assert.Throws<EditorMcpException>(() => target.Invoke("unodraw_batch", args));
        args["operations"]![0]!["arguments"]!.AsObject().Remove("expectedRevision");
        args["operations"]![0]!["arguments"]!["unrecognized"] = true;
        Assert.Throws<EditorMcpException>(() => target.Invoke("unodraw_batch", args));
        Assert.Equal(before, editor.GetSvg());
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void CancellationBeforeBatchDoesNotChangeDocument()
    {
        using var editor = Create();
        var target = new EditorMcpController(editor);
        var before = editor.GetSvg();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => target.Invoke("unodraw_element_delete", Args("{\"elements\":[\"shape\"]}"), cancellation.Token));
        Assert.Equal(before, editor.GetSvg());
    }

    [Fact]
    public void SelectionPaintTransformAndUndoUseNativeController()
    {
        using var editor = Create();
        var target = new EditorMcpController(editor);
        Call(target, "selection_set", "{\"elements\":[\"shape\"]}");
        Call(target, "paint", "{\"fill\":\"#00ff00\",\"stroke\":\"#000000\",\"strokeWidth\":2,\"opacity\":0.75}");
        Assert.Equal(1, editor.Session.UndoCount);
        var afterPaint = editor.GetSvg();
        Call(target, "transform", "{\"dx\":15,\"dy\":20}");
        Assert.NotEqual(afterPaint, editor.GetSvg());
        editor.Undo();
        Assert.Equal(afterPaint, editor.GetSvg());
    }

    [Fact]
    public void GradientUsesNativeResourcesAndUndo()
    {
        using var editor = Create();
        var target = new EditorMcpController(editor);
        Call(target, "selection_set", "{\"elements\":[\"shape\"]}");
        var before = editor.GetSvg();
        Call(target, "gradient", "{\"kind\":\"Linear\",\"angle\":45,\"stops\":[{\"offset\":0,\"color\":\"#ff0000\"},{\"offset\":1,\"color\":\"#0000ff\"}]}");
        Assert.Contains("linearGradient", editor.GetSvg());
        Assert.Single(editor.Document!.Descendants().OfType<SvgLinearGradientServer>());
        editor.Undo();
        Assert.Equal(before, editor.GetSvg());
    }

    [Fact]
    public void ResourceImportRemapsCollisionWithoutBreakingOriginal()
    {
        using var editor = Create();
        var target = new EditorMcpController(editor);
        var result = Call(target, "resource_import", "{\"xml\":\"<linearGradient id='shape'><stop offset='0' stop-color='red'/><stop offset='1' stop-color='blue'/></linearGradient>\"}");
        Assert.NotEqual("shape", result["id"]!.GetValue<string>());
        Assert.IsType<SvgRectangle>(editor.Document!.GetElementById("shape"));
        Assert.Single(editor.Document.Descendants().OfType<SvgLinearGradientServer>());
    }

    [Fact]
    public void NativeImageExportKeepsDimensionsAndDoesNotChangeClipboardOrHistory()
    {
        using var editor = Create();
        var target = new EditorMcpController(editor);
        Call(target, "selection_set", "{\"elements\":[\"shape\"]}");
        editor.Session.SetClipboard(null, "preserved clipboard");
        var before = editor.GetSvg();
        var clipboardEvents = 0;
        editor.Session.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(editor.Session.Clipboard)) clipboardEvents++; };
        var result = Call(target, "document_export", "{\"format\":\"png\"}");
        using var bitmap = SKBitmap.Decode(Convert.FromBase64String(result["base64"]!.GetValue<string>()));
        Assert.Equal(200, bitmap.Width);
        Assert.Equal(100, bitmap.Height);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(20, 25));
        Call(target, "document_export", "{\"format\":\"svg\",\"selectionOnly\":true}");
        Assert.Equal("preserved clipboard", editor.Session.Clipboard.Xml);
        Assert.Equal(0, clipboardEvents);
        Assert.Equal(before, editor.GetSvg());
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void OversizedRasterExportFailsBeforeAllocation()
    {
        using var editor = Create("<svg xmlns='http://www.w3.org/2000/svg' width='100000' height='100000'><rect width='10' height='10'/></svg>");
        Assert.Throws<EditorMcpException>(() => Call(new EditorMcpController(editor), "document_export", "{\"format\":\"png\"}"));
    }

    [Fact]
    public void GuidesAndArtboardsUseNativePersistence()
    {
        using var editor = Create();
        var target = new EditorMcpController(editor);
        Call(target, "guide_edit", "{\"action\":\"add\",\"origin\":[30,0],\"direction\":[0,1]}");
        Assert.Single(editor.GetGuides());
        Call(target, "artboard_edit", "{\"action\":\"add\",\"name\":\"Second\",\"bounds\":[250,0,200,100]}");
        Assert.Contains(editor.GetArtboards(), board => board.Name == "Second");
        var copy = new SvgEditorController();
        using (copy)
        { copy.LoadSvg(editor.GetSvg()); Assert.Single(copy.GetGuides()); Assert.Contains(copy.GetArtboards(), board => board.Name == "Second"); }
    }

    [Fact]
    public void TimelineTrackKeyAndBakeAreUndoable()
    {
        using var editor = Create();
        var target = new EditorMcpController(editor);
        Call(target, "timeline_edit", "{\"action\":\"create\",\"element\":\"shape\",\"attribute\":\"opacity\",\"from\":\"0\",\"to\":\"1\",\"duration\":\"2s\"}");
        Assert.Single(SvgTimelineService.ReadTracks(editor.Document!));
        var args = Args("{\"action\":\"key\",\"track\":0,\"offset\":0.5,\"value\":\"0.25\"}");
        args["expectedRevision"] = editor.DocumentRevision;
        target.Invoke("unodraw_timeline_edit", args);
        Assert.Contains(SvgTimelineService.ReadTracks(editor.Document!)[0].Keys, key => key.Offset == 0.5 && key.Value == "0.25");
        var animated = editor.GetSvg();
        Call(target, "timeline_edit", "{\"action\":\"bake\",\"seconds\":1}");
        Assert.Empty(SvgTimelineService.ReadTracks(editor.Document!));
        editor.Undo();
        Assert.Equal(animated, editor.GetSvg());
    }

    [Fact]
    public void LayerStateUsesInheritedLocksAndRetainsSelection()
    {
        using var editor = Create();
        var target = new EditorMcpController(editor);
        Call(target, "selection_set", "{\"elements\":[\"shape\"]}");
        Call(target, "layer", "{\"action\":\"lock\",\"element\":\"shape\",\"locked\":true}");
        Assert.True(SvgEditorController.IsLocked(Assert.Single(editor.SelectedElements)));
        Call(target, "layer", "{\"action\":\"lock\",\"element\":\"shape\",\"locked\":false}");
        Assert.False(SvgEditorController.IsLocked(Assert.Single(editor.SelectedElements)));
        Call(target, "layer", "{\"action\":\"visibility\",\"element\":\"shape\",\"visible\":false}");
        Assert.Equal("none", Assert.Single(editor.SelectedElements).Display);
    }

    [Fact]
    public void MeasurementRespectsRootViewBoxWithoutChangingHistory()
    {
        using var editor = Create("<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100' viewBox='0 0 100 50'><rect width='20' height='20'/></svg>");
        var measurement = Call(new EditorMcpController(editor), "measure", "{\"start\":[0,0],\"end\":[60,80]}");
        Assert.Equal(50, measurement["distance"]!.GetValue<double>(), 5);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void PrintSetupIsUndoableAndExportsNativePdfAndPlates()
    {
        using var editor = Create();
        var target = new EditorMcpController(editor);
        var before = editor.GetSvg();
        var xml = XElement.Parse(Call(target, "print_get")["xml"]!.GetValue<string>());
        xml.SetAttributeValue("width", 40);
        xml.SetAttributeValue("height", 40);
        xml.SetAttributeValue("margin", 2);
        xml.SetAttributeValue("bleed", 0);
        xml.SetAttributeValue("dpi", 72);
        var args = new JsonObject { ["xml"] = xml.ToString() };
        target.Invoke("unodraw_print_set", args);
        var pdf = Convert.FromBase64String(Call(target, "print_export")["base64"]!.GetValue<string>());
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        var plates = Convert.FromBase64String(Call(target, "print_export", "{\"format\":\"separations\"}")["base64"]!.GetValue<string>());
        using var stream = new MemoryStream(plates);
        using var zip = new ZipArchive(stream);
        Assert.NotNull(zip.GetEntry("print-job.xml"));
        Assert.NotNull(zip.GetEntry("artwork.svg"));
        Assert.Equal(4, zip.Entries.Count(entry => entry.FullName.EndsWith(".png", StringComparison.Ordinal)));
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(before, editor.GetSvg());
    }

    [Fact]
    public void MeshCreateAndRoundTripUseNativeMetadata()
    {
        using var editor = Create();
        var target = new EditorMcpController(editor);
        var mesh = Call(target, "mesh", "{\"action\":\"create\",\"bounds\":[0,0,100,100],\"rows\":2,\"columns\":2}");
        var result = target.Invoke("unodraw_mesh", new JsonObject { ["action"] = "get", ["element"] = mesh["element"]!.DeepClone() })!.AsObject();
        Assert.Equal(mesh["xml"]!.GetValue<string>(), result["xml"]!.GetValue<string>());
        Assert.Single(editor.SelectedElements);
        Assert.Equal(1, editor.Session.UndoCount);
    }

    [Theory]
    [InlineData("rows")]
    [InlineData("columns")]
    public void MeshGridDimensionsRespectNativeLimitBeforeMutation(string dimension)
    {
        using var editor = Create();
        var target = new EditorMcpController(editor);
        var before = editor.GetSvg();
        var revision = editor.DocumentRevision;
        var args = Args("{\"action\":\"create\",\"bounds\":[0,0,100,100],\"rows\":2,\"columns\":2}");
        args[dimension] = 16;
        EditorMcpCatalog.ValidateArguments("unodraw_mesh", args);
        args[dimension] = 17;
        Assert.Throws<EditorMcpException>(() => target.Invoke("unodraw_mesh", args));
        Assert.Equal(before, editor.GetSvg());
        Assert.Equal(revision, editor.DocumentRevision);
        Assert.Empty(editor.SelectedElements);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void ExplicitNonGroupMeshTargetDoesNotCreateArtwork()
    {
        using var editor = Create();
        var target = new EditorMcpController(editor);
        var shape = editor.Document!.GetElementById<SvgRectangle>("shape");
        editor.Select(shape);
        var before = editor.GetSvg();
        var revision = editor.DocumentRevision;
        var xml = SvgGradientMeshService.Serialize(SvgGradientMeshService.CreateGrid(SKRect.Create(0, 0, 100, 100)));
        var args = new JsonObject { ["action"] = "set", ["element"] = "shape", ["xml"] = xml };
        Assert.Throws<EditorMcpException>(() => target.Invoke("unodraw_mesh", args));
        Assert.Equal(before, editor.GetSvg());
        Assert.Equal(revision, editor.DocumentRevision);
        Assert.Same(shape, Assert.Single(editor.SelectedElements));
        Assert.False(editor.CanUndo);

        // Omitting a target deliberately permits creation beside non-mesh selection.
        args.Remove("element");
        target.Invoke("unodraw_mesh", args);
        Assert.IsType<SvgGroup>(Assert.Single(editor.SelectedElements));
        Assert.Same(shape, editor.Document.GetElementById("shape"));
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(before, editor.GetSvg());
    }

    [Fact]
    public void ExplicitIncompatibleStoryTargetDoesNotCreateArtwork()
    {
        using var editor = Create();
        var target = new EditorMcpController(editor);
        var shape = editor.Document!.GetElementById<SvgRectangle>("shape");
        editor.Select(shape);
        var before = editor.GetSvg();
        var revision = editor.DocumentRevision;
        var story = new SvgTextStory { Text = "Linked text", DefaultStyle = new SvgStoryTextStyle { FontFamily = "DejaVu Sans", FontSize = 12 } };
        story.Frames.Add(new SvgTextFrameDefinition { Width = 120, Height = 80 });
        var args = new JsonObject { ["element"] = "shape", ["xml"] = SvgTextStoryService.Serialize(story) };
        Assert.Throws<EditorMcpException>(() => target.Invoke("unodraw_text_story", args));
        Assert.Equal(before, editor.GetSvg());
        Assert.Equal(revision, editor.DocumentRevision);
        Assert.Same(shape, Assert.Single(editor.SelectedElements));
        Assert.False(editor.CanUndo);

        args.Remove("element");
        var created = target.Invoke("unodraw_text_story", args)!.AsObject();
        var group = Assert.IsType<SvgGroup>(Assert.Single(editor.SelectedElements));
        Assert.Equal("Linked text", SvgTextStoryService.Read(group).Text);
        Assert.Same(shape, editor.Document.GetElementById("shape"));
        Assert.Equal(1, editor.Session.UndoCount);

        // A compatible explicit target edits that story instead of creating another.
        args["element"] = created["element"]!.DeepClone();
        story.Text = "Updated text";
        args["xml"] = SvgTextStoryService.Serialize(story);
        target.Invoke("unodraw_text_story", args);
        Assert.Same(group, Assert.Single(editor.SelectedElements));
        Assert.Equal("Updated text", SvgTextStoryService.Read(group).Text);
        Assert.Single(editor.Document.Descendants().OfType<SvgGroup>(), value => value.CustomAttributes.ContainsKey(SvgTextStoryService.StoryAttribute));
        Assert.Equal(2, editor.Session.UndoCount);
        editor.Undo();
        editor.Undo();
        Assert.Equal(before, editor.GetSvg());
    }

    [Fact]
    public void SymbolSprayUsesImportedNativeSymbolResource()
    {
        using var editor = Create();
        var target = new EditorMcpController(editor);
        var resource = Call(target, "resource_import", "{\"xml\":\"<symbol id='stamp' viewBox='0 0 10 10'><circle cx='5' cy='5' r='4' fill='red'/></symbol>\"}");
        var args = Args("{\"action\":\"spray\",\"points\":[[60,50]],\"density\":2,\"seed\":7}");
        args["id"] = resource["id"]!.DeepClone();
        target.Invoke("unodraw_symbol", args);
        Assert.Equal(2, editor.Document!.Descendants().OfType<SvgUse>().Count());
    }

    [Theory]
    [InlineData("{\"unexpected\":true}")]
    [InlineData("{\"limit\":1001}")]
    [InlineData("{\"offset\":-1}")]
    public void InvalidArgumentsAreRejectedByProductionSchema(string json)
    {
        using var editor = Create();
        Assert.Throws<EditorMcpException>(() => Call(new EditorMcpController(editor), "element_list", json));
    }
}
