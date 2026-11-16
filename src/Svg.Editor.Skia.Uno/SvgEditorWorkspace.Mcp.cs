using System.Collections.Specialized;
using System.Text.Json.Nodes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Svg.Editor.Mcp;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    // Native commands, tool changes and panel layout changes share this context
    // signal. Merely refreshing a resource never invokes a rendering operation.
    private event Action? McpContextChanged;

    /// <summary>Creates a disposable MCP adapter. All requests are serialized and dispatched to this workspace's UI thread.</summary>
    public IEditorMcpTarget CreateMcpTarget(bool readOnly = false) => new WorkspaceMcpTarget(this, readOnly);

    private sealed class WorkspaceMcpTarget : IEditorMcpTarget, IDisposable
    {
        private readonly SvgEditorWorkspace _owner;
        private readonly bool _readOnly;
        private readonly SemaphoreSlim _requests = new(1, 1);
        private readonly CancellationTokenSource _lifetime = new();
        private readonly Dictionary<EditorDocument, long> _observed = new();
        private bool _disposed;

        public WorkspaceMcpTarget(SvgEditorWorkspace owner, bool readOnly)
        {
            _owner = owner;
            _readOnly = readOnly;
            owner.Documents.CollectionChanged += DocumentsChanged;
            owner.WorkspaceTitleChanged += WorkspaceChanged;
            owner.McpContextChanged += ContextChanged;
            owner.Canvas.ViewportChanged += ViewChanged;
            owner.ActualThemeChanged += ThemeChanged;
            ObserveDocuments();
        }

        public event Action<string>? ResourceChanged;

        private void DocumentsChanged(object? sender, NotifyCollectionChangedEventArgs args) { ObserveDocuments(); Changed("unodraw://documents"); }
        private void WorkspaceChanged(object? sender, string title)
        {
            ContextChanged();
            Changed("unodraw://selection");
            Changed("unodraw://artboards");
            Changed("unodraw://guides");
        }
        private void ContextChanged()
        {
            Changed("unodraw://workspace");
            Changed("unodraw://tools");
            Changed("unodraw://commands");
            Changed("unodraw://view");
        }
        private void ViewChanged(object? sender, EventArgs args) => Changed("unodraw://view");
        private void ThemeChanged(FrameworkElement sender, object args) => Changed("unodraw://view");
        private void ObserveDocuments()
        {
            foreach (var document in _observed.Keys.Where(document => !_owner.Documents.Contains(document)).ToArray())
            { document.Controller.Changed -= DocumentChanged; _observed.Remove(document); }
            foreach (var document in _owner.Documents.Where(document => !_observed.ContainsKey(document)))
            { _observed.Add(document, document.Controller.DocumentRevision); document.Controller.Changed += DocumentChanged; }
        }
        private void DocumentChanged(object? sender, EventArgs args)
        {
            var document = _observed.Keys.FirstOrDefault(value => ReferenceEquals(value.Controller, sender));
            if (document is not null && _observed[document] != document.Controller.DocumentRevision)
            {
                _observed[document] = document.Controller.DocumentRevision;
                Changed("unodraw://document/" + document.RecoveryId + "/source");
                Changed("unodraw://document/" + document.RecoveryId + "/elements");
                if (ReferenceEquals(document, _owner._current))
                    Changed("unodraw://guides");
            }
            Changed("unodraw://documents");
            if (ReferenceEquals(document, _owner._current))
            { Changed("unodraw://selection"); Changed("unodraw://commands"); Changed("unodraw://artboards"); }
        }
        private void Changed(string uri)
        {
            if (_disposed)
                return;
            // A disconnected transport must never break a local editor transaction.
            foreach (var handler in ResourceChanged?.GetInvocationList().Cast<Action<string>>() ?? Enumerable.Empty<Action<string>>())
                try
                { handler(uri); }
                catch (Exception) { }
        }

        public async ValueTask<JsonNode?> InvokeAsync(string operation, JsonObject arguments, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var tool = EditorMcpCatalog.ValidateArguments(operation, arguments);
            if (_readOnly && !tool.ReadOnly)
                throw new EditorMcpException("This connection is read-only.");
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
            await _requests.WaitAsync(linked.Token).ConfigureAwait(false);
            try
            {
                var completion = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
                var state = 0;
                using var registration = linked.Token.Register(() =>
                { if (Interlocked.CompareExchange(ref state, 2, 0) == 0) completion.TrySetCanceled(linked.Token); });
                if (!_owner.DispatcherQueue.TryEnqueue(async () =>
                {
                    if (Interlocked.CompareExchange(ref state, 1, 0) != 0)
                        return;
                    try
                    { completion.TrySetResult(await _owner.InvokeMcpOnUiAsync(operation, arguments, tool.ReadOnly, linked.Token)); }
                    catch (OperationCanceledException) { completion.TrySetCanceled(linked.Token); }
                    catch (Exception exception) { completion.TrySetException(exception); }
                }))
                    completion.TrySetException(new EditorMcpException("The workspace dispatcher is unavailable."));
                // Once an operation starts, retain the serialization gate until it finishes.
                // A canceled client must not race an in-flight native dialog or editor transaction.
                return await completion.Task.ConfigureAwait(false);
            }
            finally { _requests.Release(); }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _lifetime.Cancel();
            void Detach()
            {
                _owner.Documents.CollectionChanged -= DocumentsChanged;
                _owner.WorkspaceTitleChanged -= WorkspaceChanged;
                _owner.McpContextChanged -= ContextChanged;
                _owner.Canvas.ViewportChanged -= ViewChanged;
                _owner.ActualThemeChanged -= ThemeChanged;
                foreach (var document in _observed.Keys)
                    document.Controller.Changed -= DocumentChanged;
                _observed.Clear();
                ResourceChanged = null;
            }
            if (_owner.DispatcherQueue.HasThreadAccess)
                Detach();
            else
                _owner.DispatcherQueue.TryEnqueue(Detach);
        }
    }

    private EditorDocument McpDocument(EditorMcpArguments a)
    {
        if (!a.Has("documentId"))
            return _current ?? throw new EditorMcpException("No document is open.");
        if (!Guid.TryParse(a.Text("documentId"), out var id))
            throw new EditorMcpException("Invalid documentId.");
        return Documents.FirstOrDefault(document => document.RecoveryId == id) ?? throw new EditorMcpException("The requested document is no longer open.");
    }

    private JsonObject McpDocumentInfo(EditorDocument document, bool includeSource = false)
    {
        var state = new EditorMcpController(document.Controller).DocumentState(includeSource);
        state["documentId"] = document.RecoveryId.ToString();
        state["name"] = document.Name;
        state["active"] = ReferenceEquals(document, _current);
        return state;
    }

    private JsonObject McpDocuments() => new() { ["documents"] = new JsonArray(Documents.Select(document => (JsonNode?)McpDocumentInfo(document)).ToArray()) };

    private async Task<JsonNode?> InvokeMcpOnUiAsync(string operation, JsonObject arguments, bool readOnly, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_disposed || _closing)
            throw new EditorMcpException("The workspace is closing.");
        if (!readOnly && _dialogVisible)
            throw new EditorMcpException("Finish the open dialog before sending editor changes.");
        var a = new EditorMcpArguments(arguments);
        if (!readOnly)
        {
            CancelRulerGuide();
            RestoreEditorNavigation();
            CommitInspectorEdits();
            if (operation != "unodraw_commands_run" || a.Text("id") != "finishpath")
                Canvas.CancelGesture();
        }
        if (a.Has("expectedRevision") && McpDocument(a).Controller.DocumentRevision != EditorMcpArguments.Integer64(arguments["expectedRevision"]!))
            throw new EditorMcpException("The document changed. Read its current revision and retry deliberately.");
        if (a.Has("documentId") && operation is "unodraw_commands_list" or "unodraw_commands_run" or "unodraw_tools_list" or "unodraw_tools_select"
            or "unodraw_tools_options" or "unodraw_view_get" or "unodraw_view_set" or "unodraw_workspace_get" or "unodraw_workspace_set" or "unodraw_shortcuts_get" or "unodraw_shortcuts_set")
            if (!ReferenceEquals(McpDocument(a), _current))
                throw new EditorMcpException("UI operations target the active document. Activate the requested document first.");
        JsonNode? result;
        switch (operation)
        {
            case "unodraw_document_list":
                return McpDocuments();
            case "unodraw_document_get":
                return McpDocumentInfo(McpDocument(a), true);
            case "unodraw_document_new":
                var newName = a.Has("name") ? McpName(a.Text("name")) : null;
                NewDocument(a.Float("width", 1200, 1, 1000000), a.Float("height", 800, 1, 1000000));
                if (newName is not null)
                    _current!.Name = newName;
                RefreshWorkspace();
                return McpDocumentInfo(_current!);
            case "unodraw_document_open":
                var svg = a.Text("svg", required: true, maximum: EditorMcpController.MaximumSourceCharacters);
                EditorMcpController.ValidateSvg(svg);
                var imported = await OpenMcpDocumentAsync(svg, McpName(a.Text("name", "Imported.svg")), cancellationToken);
                return McpDocumentInfo(imported);
            case "unodraw_document_activate":
                var active = McpDocument(a);
                ActivateDocument(active);
                DocumentTabs.SelectedItem = DocumentTabs.TabItems.OfType<TabViewItem>().First(tab => ReferenceEquals(tab.Tag, active));
                return McpDocumentInfo(active);
            case "unodraw_document_close":
                CloseMcpDocument(McpDocument(a), a.Bool("discardChanges"));
                return McpDocuments();
            case "unodraw_commands_list":
                return McpCommands();
            case "unodraw_commands_run":
                return await RunMcpCommandAsync(a, cancellationToken);
            case "unodraw_tools_list":
                return McpTools();
            case "unodraw_tools_select":
                result = SelectMcpTool(a);
                break;
            case "unodraw_tools_options":
                result = McpToolOptions(a);
                break;
            case "unodraw_view_get":
                return McpView();
            case "unodraw_view_set":
                result = SetMcpView(a);
                break;
            case "unodraw_workspace_get":
                return McpWorkspace();
            case "unodraw_workspace_set":
                result = SetMcpWorkspace(a);
                break;
            case "unodraw_shortcuts_get":
                return McpShortcuts();
            case "unodraw_shortcuts_set":
                result = SetMcpShortcuts(a);
                break;
            default:
                var document = McpDocument(a);
                result = new EditorMcpController(document.Controller).Invoke(operation, arguments, cancellationToken);
                if (result is JsonObject value)
                    value["documentId"] = document.RecoveryId.ToString();
                if (operation == "unodraw_timeline_edit" && a.Text("action") == "time" && ReferenceEquals(document, _current))
                {
                    _animationTimer.Stop();
                    _animationClock.Reset();
                    _animationPlaybackOrigin = document.Controller.Renderer.AnimationTime;
                    // Scrubbing changes renderer time without a document transaction.
                    Canvas.Invalidate();
                }
                break;
        }
        // Controller and canvas properties already distinguish document paints
        // from selection/tool interaction paints; retain that native scheduling.
        if (!readOnly && !_disposed)
            RefreshWorkspace();
        return result;
    }

    private static string McpName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 256 || value.Any(char.IsControl) || value.IndexOfAny(['/', '\\']) >= 0)
            throw new EditorMcpException("Document names must be 1–256 printable characters without path separators.");
        return value;
    }

    private async Task<EditorDocument> OpenMcpDocumentAsync(string svg, string name, CancellationToken cancellationToken)
    {
        var document = new EditorDocument { Name = name };
        try
        {
            await Task.Run(() => document.Controller.LoadSvg(svg), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (_disposed || _closing || _dialogVisible)
                throw new EditorMcpException("The workspace is no longer ready to open a document.");
            AddDocument(document);
            return document;
        }
        catch { document.Dispose(); throw; }
    }

    private void CloseMcpDocument(EditorDocument document, bool discardChanges)
    {
        if (document.Controller.IsDirty && !discardChanges)
            throw new EditorMcpException("The document has unsaved changes. Export it first or explicitly set discardChanges=true.");
        if (!document.SaveGate.Wait(0))
            throw new EditorMcpException("A save is in progress. Wait for it to finish before closing the tab.");
        try
        {
            DeleteRecovery(document);
            var next = ReferenceEquals(_current, document) ? Documents.FirstOrDefault(item => !ReferenceEquals(item, document)) : _current;
            document.Controller.Changed -= Controller_Changed;
            if (ReferenceEquals(_current, document))
            { _current = null; _inspectedElement = null; Canvas.Controller = null; }
            var tab = DocumentTabs.TabItems.OfType<TabViewItem>().First(item => ReferenceEquals(item.Tag, document));
            Documents.Remove(document);
            DocumentTabs.TabItems.Remove(tab);
            if (next is not null)
            {
                DocumentTabs.SelectedItem = DocumentTabs.TabItems.OfType<TabViewItem>().First(item => ReferenceEquals(item.Tag, next));
                ActivateDocument(next);
            }
            document.Dispose();
        }
        finally { document.SaveGate.Release(); }
        if (Documents.Count == 0)
            NewDocument();
        RefreshWorkspace();
    }
}
