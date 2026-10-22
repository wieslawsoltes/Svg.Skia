using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Svg.Editor.Core;
using Svg.Editor.Svg;
using Svg.Model.Services;
using Svg.Skia;
using SK = SkiaSharp;

namespace Svg.Editor.Skia;

/// <summary>
/// Framework-independent SVG editor. Document transactions own history and rendering;
/// selection and viewport changes never rebuild the retained picture.
/// </summary>
public sealed partial class SvgEditorController : IDisposable
{
    private readonly List<SvgVisualElement> _selection = new();
    private readonly IReadOnlyList<SvgVisualElement> _readOnlySelection;
    private string _currentXml = string.Empty;
    private string _savedXml = string.Empty;
    private string? _editXml;
    private string[]? _editSelection;
    private string _editLabel = string.Empty;
    private bool _editMutated;
    private bool _disposed;
    private int _nextId;

    public SvgEditorController()
    {
        _readOnlySelection = _selection.AsReadOnly();
        Session.Settings.PropertyChanged += OnSettingsChanged;
    }

    public SvgEditorSession Session { get; } = new();
    public SKSvg Renderer { get; private set; } = new();
    public SvgDocument? Document => Session.Document;
    public IReadOnlyList<SvgVisualElement> SelectedElements => _readOnlySelection;
    public ToolService Tools { get; } = new();
    public SelectionService Selection { get; } = new();
    public PathService Paths { get; } = new();
    public bool IsDirty => _editMutated || !string.Equals(_currentXml, _savedXml, StringComparison.Ordinal);
    public bool IsEditing => _editXml is not null;
    public bool CanUndo => Session.UndoCount > 0;
    public bool CanRedo => Session.RedoCount > 0;
    public string LastEditLabel { get; private set; } = string.Empty;
    public long RenderRevision { get; private set; }
    public long DocumentRevision { get; private set; }
    public string CurrentFill { get; set; } = "#7C6CFF";
    public string CurrentStroke { get; set; } = "none";
    public float CurrentStrokeWidth { get; set; } = 2f;
    public event EventHandler? Changed;

    public SK.SKRect? SelectionBounds
    {
        get
        {
            SK.SKRect? bounds = null;
            foreach (var element in _selection)
            {
                var node = GetSceneNode(element);
                if (node is null)
                    continue;
                var b = node.TransformedBounds;
                var rectangle = new SK.SKRect(b.Left, b.Top, b.Right, b.Bottom);
                bounds = bounds is { } old ? SK.SKRect.Union(old, rectangle) : rectangle;
            }
            return bounds;
        }
    }

    public SvgSceneNode? GetSceneNode(SvgElement element)
        => Renderer.TryGetRetainedSceneNode(element, out var node) ? node : null;

    public void NewDocument(float width = 1200, float height = 800)
    {
        ValidatePositive(width, nameof(width));
        ValidatePositive(height, nameof(height));
        LoadDocument(new SvgDocument
        {
            Width = width,
            Height = height,
            ViewBox = new SvgViewBox(0, 0, width, height)
        });
    }

    /// <summary>Parses and renders before replacing the current document.</summary>
    public void LoadSvg(string xml, string? fileName = null)
        => LoadDocument(ParseDocument(xml, FileBaseUri(fileName)), fileName);

    public void LoadDocument(SvgDocument document, string? fileName = null)
    {
        ThrowIfDisposed();
        if (document is null)
            throw new ArgumentNullException(nameof(document));
        if (IsEditing)
            throw new InvalidOperationException("Complete or cancel the current edit before loading a document.");

        var renderer = CreateRenderer(document);
        var xml = document.GetXML();
        ReplaceRenderer(renderer);
        Session.Document = document;
        Session.CurrentFile = fileName;
        Session.ClearHistory();
        _selection.Clear();
        Session.SetSelectedElementIds(Array.Empty<string>());
        Paths.Stop();
        _currentXml = _savedXml = xml;
        _editMutated = false;
        LastEditLabel = string.Empty;
        DocumentRevision++;
        RebuildOutline();
        NotifyChanged();
    }

    public string GetSvg() => Document?.GetXML() ?? string.Empty;

    public void Save(string path)
    {
        if (Document is null)
            throw new InvalidOperationException("No document is open.");
        if (IsEditing)
            CommitEdit();
        Document.Write(path);
        MarkSaved(path);
    }

    public void MarkSaved(string? fileName = null)
    {
        if (IsEditing)
            CommitEdit();
        _currentXml = _savedXml = GetSvg();
        if (fileName is not null)
            Session.CurrentFile = fileName;
        NotifyChanged();
    }

    /// <summary>Replaces SVG source as one undoable command, preserving file and savepoint.</summary>
    public void ReplaceSvg(string xml, string label = "Edit SVG source")
    {
        ThrowIfDisposed();
        var document = ParseDocument(xml, Document?.BaseUri);
        var renderer = CreateRenderer(document);
        var selected = CaptureSelection();
        var adopted = false;
        try
        {
            Execute(label, () =>
            {
                ReplaceRenderer(renderer);
                adopted = true;
                Session.Document = document;
                Paths.Stop();
                _selection.Clear();
                foreach (var key in selected)
                    if (ResolveSelectionKey(key) is SvgVisualElement element)
                        _selection.Add(element);
            });
        }
        finally
        {
            if (!adopted)
                renderer.Dispose();
        }
    }

    /// <summary>Marks a recovered document as requiring an explicit save.</summary>
    public void MarkUnsaved()
    {
        _savedXml = "\0";
        NotifyChanged();
    }

    public void BeginEdit(string label)
    {
        ThrowIfDisposed();
        if (Document is null)
            throw new InvalidOperationException("No document is open.");
        if (IsEditing)
            throw new InvalidOperationException("An editor transaction is already active.");
        _editXml = GetSvg();
        _editSelection = CaptureSelection();
        _editLabel = label ?? string.Empty;
        _editMutated = false;
    }

    public void CommitEdit()
    {
        if (_editXml is not { } before)
            return;

        var after = GetSvg();
        if (!string.Equals(before, after, StringComparison.Ordinal))
        {
            try
            {
                RefreshPicture();
            }
            catch
            {
                CancelEdit();
                throw;
            }
            Session.PushUndoState(before);
            LastEditLabel = _editLabel;
            DocumentRevision++;
        }
        _currentXml = after;
        _editXml = null;
        _editSelection = null;
        _editMutated = false;
        RemoveDetachedSelection();
        RebuildOutline();
        NotifyChanged();
    }

    public void CancelEdit()
    {
        if (_editXml is not { } before)
            return;
        var selected = _editSelection ?? Array.Empty<string>();
        _editXml = null;
        _editSelection = null;
        _editMutated = false;
        RestoreDocument(before, selected);
    }

    /// <summary>Executes atomically, or joins the current gesture transaction.</summary>
    public void Execute(string label, Action mutation)
    {
        if (mutation is null)
            throw new ArgumentNullException(nameof(mutation));
        var ownsTransaction = !IsEditing;
        if (ownsTransaction)
            BeginEdit(label);
        try
        {
            mutation();
            _editMutated = true;
            if (ownsTransaction)
                CommitEdit();
            else
                Refresh();
        }
        catch
        {
            CancelEdit();
            throw;
        }
    }

    /// <summary>Rebuilds a live edit preview without serializing another history state.</summary>
    public void Refresh()
    {
        ThrowIfDisposed();
        RefreshPicture();
        if (IsEditing)
            _editMutated = true;
        else
            _currentXml = GetSvg();
        NotifyChanged();
    }

    public void Undo()
    {
        if (IsEditing)
            CancelEdit();
        if (Session.TryUndo(GetSvg(), out var xml) && xml is not null)
            RestoreDocument(xml, CaptureSelection());
    }

    public void Redo()
    {
        if (IsEditing)
            CancelEdit();
        if (Session.TryRedo(GetSvg(), out var xml) && xml is not null)
            RestoreDocument(xml, CaptureSelection());
    }

    public void Select(SvgVisualElement? element, bool additive = false, bool allowLocked = false)
    {
        if (!additive)
            _selection.Clear();
        if (element is not null && BelongsToDocument(element) && (allowLocked || !IsLocked(element)))
        {
            if (additive && _selection.Contains(element))
                _selection.Remove(element);
            else if (!_selection.Contains(element))
                _selection.Add(element);
        }
        SyncSelection();
        NotifyChanged();
    }

    public void SelectAt(SK.SKPoint point, bool additive = false)
    {
        var element = Renderer.HitTestElements(new ShimSkiaSharp.SKPoint(point.X, point.Y))
            .OfType<SvgVisualElement>().FirstOrDefault(e => !IsLocked(e));
        Select(element, additive);
    }

    public void SelectInRect(SK.SKRect rectangle, bool additive = false)
    {
        if (!additive)
            _selection.Clear();
        var hits = Renderer.HitTestElements(new ShimSkiaSharp.SKRect(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom))
            .OfType<SvgVisualElement>().Where(e => !IsLocked(e));
        foreach (var element in hits)
        {
            if (!_selection.Contains(element))
                _selection.Add(element);
        }
        SyncSelection();
        NotifyChanged();
    }

    public void SelectAll()
    {
        _selection.Clear();
        if (Document is { } document)
            _selection.AddRange(document.Children.OfType<SvgVisualElement>().Where(e => !IsLocked(e) && e.Display != "none"));
        SyncSelection();
        NotifyChanged();
    }

    public void ClearSelection() => Select(null);

    public void SetFill(string value)
    {
        var paint = ParsePaint(value);
        CurrentFill = value;
        if (_selection.Count > 0)
            Execute("Change fill", () => { foreach (var element in _selection) element.Fill = paint; });
        else
            NotifyChanged();
    }

    public void SetStroke(string value)
    {
        var paint = ParsePaint(value);
        CurrentStroke = value;
        if (_selection.Count > 0)
            Execute("Change stroke", () => { foreach (var element in _selection) element.Stroke = paint; });
        else
            NotifyChanged();
    }

    public void SetStrokeWidth(float value)
    {
        if (!IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(nameof(value));
        CurrentStrokeWidth = value;
        Tools.CurrentStrokeWidth = value;
        if (_selection.Count > 0)
            Execute("Change stroke width", () => { foreach (var element in _selection) element.StrokeWidth = value; });
        else
            NotifyChanged();
    }

    public void SetOpacity(float value)
    {
        if (!IsFinite(value) || value < 0 || value > 1)
            throw new ArgumentOutOfRangeException(nameof(value));
        Execute("Change opacity", () => { foreach (var element in _selection) element.Opacity = value; });
    }

    public void SetAttribute(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("An SVG attribute name is required.", nameof(name));
        Execute("Change " + name, () =>
        {
            foreach (var element in _selection)
            {
                if (name == "id")
                {
                    if (_selection.Count != 1 || Document!.Descendants().Any(e => !ReferenceEquals(e, element) && e.ID == value))
                        throw new ArgumentException("The ID must be unique in the document.", nameof(value));
                    XmlConvert.VerifyNCName(value);
                }
                var property = element.GetType().GetProperties().FirstOrDefault(p => p.CanWrite &&
                    p.GetCustomAttributes(typeof(SvgAttributeAttribute), true).OfType<SvgAttributeAttribute>().Any(a => a.Name == name));
                if (property is null)
                    element.CustomAttributes[name] = value;
                else
                    property.SetValue(element, TypeDescriptor.GetConverter(property.PropertyType).ConvertFromInvariantString(value), null);
            }
        });
    }

    public void SetText(string text)
        => Execute("Edit text", () => { foreach (var element in _selection.OfType<SvgTextBase>()) element.Text = text; });

    public void SetVisibility(bool visible)
        => Execute(visible ? "Show objects" : "Hide objects", () =>
        {
            foreach (var element in _selection)
            {
                element.Display = visible ? "inline" : "none";
                element.Visibility = visible ? "visible" : "hidden";
            }
        });

    public void SetLocked(bool locked)
        => Execute(locked ? "Lock objects" : "Unlock objects", () =>
        {
            foreach (var element in _selection)
            {
                element.CustomAttributes["data-unodraw-locked"] = locked ? "true" : "false";
                if (element.CustomAttributes.ContainsKey("data-lock"))
                    element.CustomAttributes["data-lock"] = locked ? "true" : "false";
            }
        });

    public static bool IsLocked(SvgElement element)
    {
        for (var current = element; current is not null; current = current.Parent)
            if ((current.CustomAttributes.TryGetValue("data-unodraw-locked", out var value) && value == "true") ||
                (current.CustomAttributes.TryGetValue("data-lock", out var legacy) && legacy == "true"))
                return true;
        return false;
    }

    private void RefreshPicture()
    {
        if (Document is null)
            return;
        Renderer.RefreshFromSourceDocument();
        RenderRevision++;
    }

    private SKSvg CreateRenderer(SvgDocument document)
    {
        var renderer = new SKSvg();
        try
        {
            renderer.Wireframe = Session.Settings.WireframeEnabled;
            renderer.FromSvgDocument(document);
            return renderer;
        }
        catch
        {
            renderer.Dispose();
            throw;
        }
    }

    private void ReplaceRenderer(SKSvg renderer)
    {
        var previous = Renderer;
        Renderer = renderer;
        previous.Dispose();
        RenderRevision++;
    }

    private void RestoreDocument(string xml, string[] selection)
    {
        var document = ParseDocument(xml, Document?.BaseUri);
        var renderer = CreateRenderer(document);
        ReplaceRenderer(renderer);
        Session.Document = document;
        _currentXml = document.GetXML();
        DocumentRevision++;
        Paths.Stop();
        _selection.Clear();
        foreach (var key in selection)
            if (ResolveSelectionKey(key) is SvgVisualElement element && !_selection.Contains(element))
                _selection.Add(element);
        SyncSelection();
        RebuildOutline();
        NotifyChanged();
    }

    private static SvgDocument ParseDocument(string xml, Uri? baseUri)
    {
        if (string.IsNullOrWhiteSpace(xml))
            throw new ArgumentException("SVG source cannot be empty.", nameof(xml));
        using (var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null }))
        {
            var tree = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            if (tree.Root is not { } root || root.Name.LocalName != "svg" ||
                (root.Name.NamespaceName.Length != 0 && root.Name.NamespaceName != "http://www.w3.org/2000/svg"))
                throw new XmlException("The document root must be an SVG element.");
        }
        var document = SvgService.FromSvg(xml) ?? throw new XmlException("The SVG document could not be parsed.");
        if (baseUri is not null)
            document.BaseUri = baseUri;
        return document;
    }

    private static Uri? FileBaseUri(string? fileName)
        => !string.IsNullOrWhiteSpace(fileName) && Path.IsPathRooted(fileName) ? new Uri(Path.GetFullPath(fileName)) : null;

    private void RebuildOutline()
    {
        Session.Nodes.Clear();
        if (Document is null)
            return;
        foreach (var element in Document.Children)
            Session.Nodes.Add(BuildNode(element, null));
    }

    private static SvgNode BuildNode(SvgElement element, SvgNode? parent)
    {
        var node = new SvgNode(element, parent);
        foreach (var child in element.Children)
            node.Children.Add(BuildNode(child, node));
        return node;
    }

    private void SyncSelection() => Session.SetSelectedElementIds(_selection.Select(e => e.ID));

    private void RemoveDetachedSelection()
    {
        _selection.RemoveAll(e => !BelongsToDocument(e));
        SyncSelection();
    }

    private bool BelongsToDocument(SvgElement element)
    {
        for (var current = element; current is not null; current = current.Parent)
            if (ReferenceEquals(current, Document))
                return true;
        return false;
    }

    private string[] CaptureSelection() => _selection.Select(GetSelectionKey).ToArray();

    private string GetSelectionKey(SvgElement element)
    {
        if (!string.IsNullOrEmpty(element.ID))
            return "#" + element.ID;
        var indices = new List<int>();
        for (var current = element; current.Parent is { } parent; current = parent)
            indices.Add(parent.Children.IndexOf(current));
        indices.Reverse();
        return string.Join("/", indices);
    }

    private SvgElement? ResolveSelectionKey(string key)
    {
        if (Document is null)
            return null;
        if (key.StartsWith("#", StringComparison.Ordinal))
            return Document.Descendants().FirstOrDefault(e => e.ID == key.Substring(1));
        SvgElement current = Document;
        foreach (var part in key.Split('/'))
        {
            if (!int.TryParse(part, out var index) || index < 0 || index >= current.Children.Count)
                return null;
            current = current.Children[index];
        }
        return current;
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        Selection.SnapToGrid = Session.Settings.SnapToGrid;
        Selection.GridSize = Session.Settings.GridSize;
        if (e.PropertyName == nameof(SvgEditorSettings.WireframeEnabled))
            Renderer.Wireframe = Session.Settings.WireframeEnabled;
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        Session.WorkspaceTitle = (IsDirty ? "● " : string.Empty) +
            (string.IsNullOrEmpty(Session.CurrentFile) ? "Untitled.svg" : Path.GetFileName(Session.CurrentFile)) + " — UnoDraw";
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static SvgPaintServer ParsePaint(string value)
        => (SvgPaintServer?)TypeDescriptor.GetConverter(typeof(SvgPaintServer)).ConvertFromInvariantString(value)
            ?? throw new ArgumentException("A valid SVG paint value is required.", nameof(value));

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static void ValidatePositive(float value, string name)
    {
        if (!IsFinite(value) || value <= 0)
            throw new ArgumentOutOfRangeException(name);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SvgEditorController));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Session.Settings.PropertyChanged -= OnSettingsChanged;
        Paths.Stop();
        Renderer.Dispose();
    }
}
