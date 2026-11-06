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
        var targetPath = Path.GetFullPath(path);
        var xml = GetSvg();
        if (Document.BaseUri is { } originalUri)
            xml = SvgResourcePathService.Rebase(xml, originalUri, new Uri(targetPath));
        File.WriteAllText(targetPath, xml);
        AcceptSave(xml, targetPath);
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

    /// <summary>
    /// Accepts a successfully written SVG snapshot, relocating current and historical
    /// resource references without creating an edit. Edits made during the write remain dirty.
    /// Same-URI saves preserve the live DOM, retained picture, selection and edit contexts.
    /// </summary>
    /// <param name="savedXml">The exact snapshot written with references based at the destination.</param>
    /// <param name="fileName">The destination SVG file.</param>
    public void AcceptSave(string savedXml, string fileName)
    {
        ThrowIfDisposed();
        if (Document is null)
            throw new InvalidOperationException("No document is open.");
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("A destination file is required.", nameof(fileName));

        var targetPath = Path.GetFullPath(fileName);
        var targetUri = new Uri(targetPath);
        var originalUri = Document.BaseUri;
        var relocated = new Dictionary<string, string>(StringComparer.Ordinal);
        string Relocate(string xml)
        {
            if (originalUri is null)
                return xml;
            if (!relocated.TryGetValue(xml, out var result))
                relocated.Add(xml, result = SvgResourcePathService.Rebase(xml, originalUri, targetUri));
            return result;
        }

        // Parse everything that may fail before adopting the new URI or history. The saved
        // snapshot can precede the current document when an asynchronous write is in flight.
        var savedDocument = ParseDocument(savedXml, targetUri);
        var savedCheckpoint = savedDocument.GetXML();
        if (originalUri is not null && originalUri.Equals(targetUri))
        {
            // Saving is a checkpoint, not a document edit. Rebuilding an unchanged
            // location invalidates nonmodal drafts and node-edit handles needlessly.
            // Keep the live source (which may include newer edits) and accept only the
            // written snapshot as the savepoint. History, renderer and revision stay put.
            _currentXml = GetSvg();
            _savedXml = savedCheckpoint;
            Session.CurrentFile = targetPath;
            NotifyChanged();
            return;
        }
        var document = ParseDocument(Relocate(GetSvg()), targetUri);
        var currentXml = document.GetXML();
        var editXml = _editXml is null ? null : Relocate(_editXml);
        if (_savedXml != "\0" && _savedXml.Length != 0)
            Relocate(_savedXml);
        var selected = CaptureSelection();
        var animationTime = Renderer.AnimationTime;
        var renderer = CreateRenderer(document);
        var adopted = false;
        try
        {
            renderer.SetAnimationTime(animationTime);
            Session.TransformHistory(Relocate);
            ReplaceRenderer(renderer);
            adopted = true;
            Session.Document = document;
            Session.CurrentFile = targetPath;
            _currentXml = currentXml;
            _editXml = editXml;
            _savedXml = savedCheckpoint;
            DocumentRevision++;
            Paths.Stop();
            _selection.Clear();
            foreach (var key in selected)
                if (ResolveSelectionKey(key) is SvgVisualElement element && !_selection.Contains(element))
                    _selection.Add(element);
            SyncSelection();
            RebuildOutline();
            NotifyChanged();
        }
        finally
        {
            if (!adopted)
                renderer.Dispose();
        }
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
        var position = new ShimSkiaSharp.SKPoint(point.X, point.Y);
        // The full enumeration follows paint order (back to front); the dedicated topmost
        // query traverses in reverse and stops at the first painted object under the pointer.
        var element = Renderer.HitTestTopmostElement(position) as SvgVisualElement;
        if (element is null || IsLocked(element))
            element = Renderer.HitTestElements(position).OfType<SvgVisualElement>().LastOrDefault(e => !IsLocked(e));
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
        EnsureSelectionEditable();
        var paint = ParsePaint(value);
        CurrentFill = value;
        if (_selection.Count > 0)
            Execute("Change fill", () => { foreach (var element in _selection) element.Fill = paint; });
        else
            NotifyChanged();
    }

    public void SetStroke(string value)
    {
        EnsureSelectionEditable();
        var paint = ParsePaint(value);
        CurrentStroke = value;
        if (_selection.Count > 0)
            Execute("Change stroke", () => { foreach (var element in _selection) element.Stroke = paint; });
        else
            NotifyChanged();
    }

    public void SetStrokeWidth(float value)
    {
        EnsureSelectionEditable();
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
        EnsureSelectionEditable();
        if (!IsFinite(value) || value < 0 || value > 1)
            throw new ArgumentOutOfRangeException(nameof(value));
        Execute("Change opacity", () => { foreach (var element in _selection) element.Opacity = value; });
    }

    public void SetAttribute(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("An SVG attribute name is required.", nameof(name));
        if (name == "id")
        {
            RenameSelectedId(value);
            return;
        }
        EnsureSelectionEditable();
        Execute("Change " + name, () =>
        {
            foreach (var element in _selection)
            {
                var property = element.GetType().GetProperties().FirstOrDefault(p => p.CanWrite &&
                    p.GetCustomAttributes(typeof(SvgAttributeAttribute), true).OfType<SvgAttributeAttribute>().Any(a => a.Name == name));
                if (property is null)
                    element.CustomAttributes[name] = value;
                else
                    property.SetValue(element, TypeDescriptor.GetConverter(property.PropertyType).ConvertFromInvariantString(value), null);
            }
        });
    }

    public void RenameSelectedId(string id)
    {
        if (_selection.Count != 1)
            throw new InvalidOperationException("Select exactly one object to rename its ID.");
        RenameElementId(_selection[0], id);
    }

    /// <summary>Renames any SVG element and its document-local references in one edit.</summary>
    public void RenameElementId(SvgElement element, string id)
    {
        ThrowIfDisposed();
        if (element is null)
            throw new ArgumentNullException(nameof(element));
        if (!BelongsToDocument(element))
            throw new ArgumentException("The element must belong to this document.", nameof(element));
        EnsureElementEditable(element);
        XmlConvert.VerifyNCName(id);
        var existing = Document!.GetElementById(id);
        if (existing is not null && !ReferenceEquals(existing, element))
            throw new ArgumentException("The ID must be unique in the document.", nameof(id));
        var previousId = element.ID;
        if (previousId == id)
            return;
        var selected = _selection.Select(e => ReferenceEquals(e, element) ? "#" + id : GetSelectionKey(e)).ToArray();
        Execute("Rename object", () =>
        {
            element.ID = id;
            var tree = XDocument.Parse(Document.GetXML(), LoadOptions.PreserveWhitespace);
            if (!string.IsNullOrEmpty(previousId))
                RewriteTreeReferences(tree, new Dictionary<string, string>(StringComparer.Ordinal) { [previousId] = id });
            var document = ParseDocument(tree.ToString(SaveOptions.DisableFormatting), Document.BaseUri);
            var renderer = CreateRenderer(document);
            ReplaceRenderer(renderer);
            Session.Document = document;
            Paths.Stop();
            _selection.Clear();
            foreach (var key in selected)
                if (ResolveSelectionKey(key) is SvgVisualElement visual && !_selection.Contains(visual))
                    _selection.Add(visual);
        });
    }

    public void SetText(string text)
    {
        EnsureSelectionEditable();
        Execute("Edit text", () => { foreach (var element in _selection.OfType<SvgTextBase>()) element.Text = text; });
    }

    /// <summary>Changes only supplied typography fields, retaining spans and text paths for style-only edits.</summary>
    public void SetTypography(string? text = null, string? fontFamily = null, SvgUnit? fontSize = null, SvgUnit? letterSpacing = null)
    {
        EnsureSelectionEditable();
        if (fontSize is { } size)
            ValidatePositive(size.Value, nameof(fontSize));
        if (letterSpacing is { } spacing && !IsFinite(spacing.Value))
            throw new ArgumentOutOfRangeException(nameof(letterSpacing));
        if (text is null && fontFamily is null && fontSize is null && letterSpacing is null)
            return;
        Execute("Edit typography", () =>
        {
            foreach (var element in _selection.OfType<SvgTextBase>())
            {
                if (text is not null)
                    element.Text = text;
                if (fontFamily is not null)
                    element.FontFamily = fontFamily;
                if (fontSize is { } newSize)
                    element.FontSize = newSize;
                if (letterSpacing is { } newSpacing)
                    element.LetterSpacing = newSpacing;
            }
        });
    }

    public void SetVisibility(bool visible)
    {
        EnsureSelectionEditable();
        Execute(visible ? "Show objects" : "Hide objects", () =>
        {
            foreach (var element in _selection)
            {
                element.Display = visible ? "inline" : "none";
                element.Visibility = visible ? "visible" : "hidden";
            }
        });
    }

    public void SetLocked(bool locked)
    {
        if (locked)
            EnsureSelectionEditable();
        Execute(locked ? "Lock objects" : "Unlock objects", () =>
        {
            foreach (var element in _selection)
            {
                element.CustomAttributes["data-unodraw-locked"] = locked ? "true" : "false";
                if (element.CustomAttributes.ContainsKey("data-lock"))
                    element.CustomAttributes["data-lock"] = locked ? "true" : "false";
            }
        });
    }

    /// <summary>Rejects edits that would modify a locked object or one of its descendants.</summary>
    public void EnsureSelectionEditable()
    {
        foreach (var element in _selection)
            EnsureElementEditable(element);
    }

    public static bool IsEditLocked(SvgElement element)
        => IsLocked(element) || element.Descendants().Any(HasOwnLock);

    private static void EnsureElementEditable(SvgElement element)
    {
        if (IsEditLocked(element))
            throw new InvalidOperationException("Unlock the object or its locked layer before editing it.");
    }

    private static bool HasOwnLock(SvgElement element)
        => (element.CustomAttributes.TryGetValue("data-unodraw-locked", out var value) && value == "true") ||
           (element.CustomAttributes.TryGetValue("data-lock", out var legacy) && legacy == "true");

    public static bool IsLocked(SvgElement element)
    {
        for (var current = element; current is not null; current = current.Parent)
            if (HasOwnLock(current))
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
            renderer.IgnoreAttributes = Session.Settings.FiltersDisabled ? Model.DrawAttributes.Filter : Model.DrawAttributes.None;
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
        using var source = XmlReader.Create(new StringReader(xml),
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null }, baseUri?.AbsoluteUri ?? string.Empty);
        var document = SvgService.Open(source) ?? throw new XmlException("The SVG document could not be parsed.");
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
        if (e.PropertyName == nameof(SvgEditorSettings.FiltersDisabled))
        {
            Renderer.IgnoreAttributes = Session.Settings.FiltersDisabled ? Model.DrawAttributes.Filter : Model.DrawAttributes.None;
            RefreshPicture();
        }
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
