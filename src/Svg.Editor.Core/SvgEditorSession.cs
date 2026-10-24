using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Svg;

namespace Svg.Editor.Core;

public partial class SvgEditorSession : ISvgEditorSession
{
    private SvgDocument? _document;
    private string? _currentFile;
    private string _workspaceTitle = "SVG Editor";
    private string _filterText = string.Empty;
    private string _propertyFilterText = string.Empty;
    private string? _selectedElementId;
    private SvgEditorToolKind _currentTool;
    private readonly Stack<string> _undo = new();
    private readonly Stack<string> _redo = new();

    public SvgDocument? Document
    {
        get => _document;
        set
        {
            if (ReferenceEquals(_document, value))
                return;

            _document = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Document)));
        }
    }

    public string? CurrentFile
    {
        get => _currentFile;
        set
        {
            if (_currentFile == value)
                return;

            _currentFile = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentFile)));
        }
    }

    public string WorkspaceTitle
    {
        get => _workspaceTitle;
        set => SetField(ref _workspaceTitle, value, nameof(WorkspaceTitle));
    }

    public string FilterText
    {
        get => _filterText;
        set => SetField(ref _filterText, value, nameof(FilterText));
    }

    public string PropertyFilterText
    {
        get => _propertyFilterText;
        set => SetField(ref _propertyFilterText, value, nameof(PropertyFilterText));
    }

    public string? SelectedElementId
    {
        get => _selectedElementId;
        set => SetField(ref _selectedElementId, value, nameof(SelectedElementId));
    }

    public ObservableCollection<SvgNode> Nodes { get; } = new();
    public ObservableCollection<ArtboardInfo> Artboards { get; } = new();
    public ObservableCollection<string> SelectedElementIds { get; } = new();
    public ISet<string> ExpandedNodeIds { get; } = new HashSet<string>(System.StringComparer.Ordinal);
    public SvgEditorSettings Settings { get; } = new();
    public ClipboardSnapshot Clipboard { get; } = new();
    public SvgEditorToolKind CurrentTool
    {
        get => _currentTool;
        set => SetField(ref _currentTool, value, nameof(CurrentTool));
    }

    public int UndoCount => _undo.Count;
    public int RedoCount => _redo.Count;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void PushUndoState(string xml)
    {
        _undo.Push(xml);
        _redo.Clear();
        NotifyHistoryChanged();
    }

    public bool TryUndo(string currentXml, out string? xml)
    {
        if (_undo.Count == 0)
        {
            xml = null;
            return false;
        }

        _redo.Push(currentXml);
        xml = _undo.Pop();
        NotifyHistoryChanged();
        return true;
    }

    public bool TryRedo(string? currentXml, out string? xml)
    {
        if (_redo.Count == 0)
        {
            xml = null;
            return false;
        }

        if (currentXml is { Length: > 0 })
            _undo.Push(currentXml);

        xml = _redo.Pop();
        NotifyHistoryChanged();
        return true;
    }

    public void ClearHistory()
    {
        if (_undo.Count == 0 && _redo.Count == 0)
            return;

        _undo.Clear();
        _redo.Clear();
        NotifyHistoryChanged();
    }

    /// <summary>Transforms every snapshot atomically, preserving undo and redo order.</summary>
    public void TransformHistory(Func<string, string> transform)
    {
        if (transform is null)
            throw new ArgumentNullException(nameof(transform));

        // Stage both stacks before changing either. A failed URI relocation must leave
        // every history entry usable with the document's original resource base URI.
        string Convert(string xml) => transform(xml)
            ?? throw new InvalidOperationException("A history transformation returned an empty snapshot.");
        var undo = _undo.Select(Convert).Reverse().ToArray();
        var redo = _redo.Select(Convert).Reverse().ToArray();
        _undo.Clear();
        _redo.Clear();
        foreach (var xml in undo)
            _undo.Push(xml);
        foreach (var xml in redo)
            _redo.Push(xml);
    }

    public void SetClipboard(SvgElement? element, string? xml)
    {
        Clipboard.Element = element;
        Clipboard.Xml = xml;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Clipboard)));
    }

    public void ClearClipboard()
    {
        Clipboard.Clear();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Clipboard)));
    }

    public void SetSelectedElementIds(IEnumerable<string?> ids)
    {
        var normalized = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct()
            .ToList();

        if (SelectedElementIds.SequenceEqual(normalized))
        {
            SelectedElementId = normalized.FirstOrDefault();
            return;
        }

        SelectedElementIds.Clear();
        foreach (var id in normalized)
            SelectedElementIds.Add(id);

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedElementIds)));
        SelectedElementId = normalized.FirstOrDefault();
    }

    private void NotifyHistoryChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UndoCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RedoCount)));
    }

    private void SetField<T>(ref T field, T value, string propertyName)
    {
        if (Equals(field, value))
            return;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
